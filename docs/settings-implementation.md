# S4 설정 구현 메모

현재·편집·예약·마지막 정상 저장본은 `PlaytestSession.ObserveSettings()`의 복사본으로 관찰한다. 호스트 슬롯 0의 BeginSettingsEdit → UpdateSettingsEdit → ApplySettings 명령만 변경한다. 슬롯 번호는 네트워크 수신자가 신뢰해서는 안 되며 S5의 인증된 연결 배정에서 결정해야 한다. CancelSettingsEdit 및 RestoreSettingsDefaults는 예약과 파일을 바꾸지 않는다. 라운드 시작에서 Pending이 Current가 되고 Version이 증가한다. S2/BotDirector는 Current만 소비한다.

저장 경로는 Unity persistentDataPath/playtest-settings.xml이다. 동일 디렉터리 임시 파일에 XML을 쓰고 flush한 후 원자적 File.Replace 또는 최초 File.Move를 실행한다. 저장 실패는 예약을 취소하지 않으며 RetrySettingsSave가 마지막 실패 값을 다시 저장한다. 손상 파일은 읽을 때 변경하지 않고, 추후 정상 저장 전에 .corrupt-고유번호 사본을 보존한다. 최초 파일 부재는 오류가 아니다.

제작 위임 기본값: 이동 5/5.6, 점프 각각 1.5, 중간 유예 1초, 탄창12, 재장전1.5초, 버블 반지름.35/속도18/사거리25/유지2초/발사간격.3초/밀기8. 합의 기본값: 준비20초, 추격180초, 최초 동물2, 최초 공격 유예2초, 충돌 두 종류와 아군 밀기 모두 켜기. 초기 필드 범위는 PlaytestSettings.Validate에 정의한다. 유예0은 유효, 최초 동물1~11이다.

검증: scripts/test-core.sh는 공개 세션 명령으로 예약 경계, 호스트 권한, 잘못된 입력 보존, 기본 복원 후 취소, 실제 임시파일 재시작·실패·재시도·손상을 검증한다. 저장 실패는 격리된 임시디렉터리의 .tmp 경로를 디렉터리로 만들어 실제 파일 생성 오류를 재현한다. compile-runtime.sh는 Unity 어셈블리에 대한 컴파일이며 실제 Unity 실행이 아니다. 양 OS 재실행/UI, 다음 라운드 전투·봇·네트워크 동일 버전 실제 적용은 통합 후 제품 검증이 남는다. Windows 실기 검증은 사용자 지시에 따라 후순위로 둔다.
