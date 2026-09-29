# S4 설정 구현 메모

현재·편집·예약·마지막 정상 저장본은 `PlaytestSession.ObserveSettings()`의 복사본으로 관찰한다. 호스트 슬롯 0의 BeginSettingsEdit → UpdateSettingsEdit → ApplySettings 명령만 변경한다. 슬롯 번호는 네트워크 수신자가 신뢰해서는 안 되며 S5의 인증된 연결 배정에서 결정해야 한다. CancelSettingsEdit 및 RestoreSettingsDefaults는 예약과 파일을 바꾸지 않는다. 라운드 시작에서 Pending이 Current가 되고 Version이 증가한다. S2/BotDirector는 Current만 소비한다.

저장 경로는 Unity persistentDataPath/playtest-settings.xml이다. 동일 디렉터리 임시 파일에 XML을 쓰고 flush한 후 원자적 File.Replace 또는 최초 File.Move를 실행한다. 저장 실패는 예약을 취소하지 않으며 RetrySettingsSave가 마지막 실패 값을 다시 저장한다. 손상 파일은 읽을 때 변경하지 않고, 추후 정상 저장 전에 .corrupt-고유번호 사본을 보존한다. 최초 파일 부재는 오류가 아니다.

제작 위임 기본값: 이동 5/5.6, 점프 각각 1.5, 중간 유예 1초, 탄창12, 재장전1.5초, 버블 반지름.35/속도18/사거리25/유지2초/발사간격.3초/밀기8. 합의 기본값: 준비20초, 추격180초, 최초 동물2, 최초 공격 유예2초, 충돌 두 종류와 아군 밀기 모두 켜기. 초기 필드 범위는 PlaytestSettings.Validate에 정의한다. 유예0은 유효, 최초 동물1~11이다.

검증: scripts/test-core.sh는 공개 세션 명령으로 예약 경계, 호스트 권한, 잘못된 입력 보존, 기본 복원 후 취소, 실제 임시파일 재시작·실패·재시도·손상을 검증한다. 저장 실패는 격리된 임시디렉터리의 .tmp 경로를 디렉터리로 만들어 실제 파일 생성 오류를 재현한다. compile-runtime.sh는 Unity 어셈블리에 대한 컴파일이며 실제 Unity 실행이 아니다. 양 OS 재실행/UI, 다음 라운드 전투·봇·네트워크 동일 버전 실제 적용은 통합 후 제품 검증이 남는다. Windows 실기 검증은 사용자 지시에 따라 후순위로 둔다.

## 수치 허용 범위

| 항목 | 기본값 | 허용 범위 |
|---|---:|---:|
| 준비 시간 | 20초 | 1~120초 |
| 추격 시간 | 180초 | 1~3600초 |
| 최초 동물 수 | 2 | 정수1~11 |
| 최초/중간 공격 유예 | 2/1초 | 각각0~30초 |
| 인간/동물 속도 | 5/5.6 | 각각0.01~20 |
| 인간/동물 점프 높이 | 1.5/1.5 | 각각0.01~5 |
| 탄창 | 12 | 정수1~100 |
| 재장전 시간 | 1.5초 | 0.01~30초 |
| 버블 반지름 | 0.35 | 0.01~2 |
| 버블 속도/사거리 | 18/25 | 각각0.01~100 |
| 버블 유지 시간 | 2초 | 0.01~10초 |
| 발사 간격 | 0.3초 | 0.01~30초 |
| 밀치는 힘 | 8 | 0.01~30 |
| 아군 충돌/진영 간 충돌/아군 밀기 | 켜짐 | 각각 독립 켜기/끄기 |

NaN/Infinity 및 잘못된 정수·문자열 입력도 거부한다. 설정 화면에는 항목별 범위 오류가 표시된다. 이 범위는 제작 단계의 안전한 초기값이며 실제 플레이 결과에 따라 조정할 수 있다.

## 실제 Unity 검증 — 2026-09-29

- Unity 6000.6.3f1, macOS batchmode: EditMode 6/6 PASS. 공개 설정 예약·권한·검증·실제 파일 저장/실패/재시작 시나리오를 실제 Editor에서도 실행했다.
- 같은 Editor PlayMode, `AvH.Tests.SettingsMovementTests`: 1/1 PASS. 현재 라운드는 원래 이동속도 유지, 다음 라운드 버전 증가 후 호스트와 두 봇 CharacterController에 새 이동속도 적용을 확인했다. 보유 에셋 없는 블록아웃 물리 씬이며 그림·메뉴 조작을 검증한 결과는 아니다.
- 테스트는 `StartSolo`에 임시 저장 경로를 전달하여 실제 사용자 설정 파일을 건드리지 않는다. 해당 매개변수를 생략한 제품 실행은 Unity persistentDataPath를 사용한다.
- 로그/결과: `/private/tmp/avh-s4-editmode.log`, `/private/tmp/avh-s4-editmode.xml`, `/private/tmp/avh-s4-playmode.log`, `/private/tmp/avh-s4-playmode.xml`.
- RED: 74b1dd3에서 실제 Editor 컴파일이 `StartSolo` 세 인자 오버로드 부재로 실패. GREEN: 후속 격리 저장경로 지원에서 위 테스트 통과.
- 최초 sandbox 실행은 UPM 로컬 소켓 EPERM으로 종료한 뒤 같은 명령을 승인된 권한으로 재실행했다. 제품 결함과 구분한다.
- 실제 Esc UI·macOS/Windows 독립 실행 재실행·전투/네트워크 통합은 미검증이며 PA-4/13/14/15 VERIFIED가 아니다.
