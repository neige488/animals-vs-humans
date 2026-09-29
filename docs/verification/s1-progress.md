# S1 구현 체크포인트 — 보유 에셋 적용·제품 검증 진행 중

- 프로젝트: `game/`, Unity 6000.6.3f1, `com.unity.template.3d`.
- Unity CLI `projects create ... --no-cloud`로 생성. `--vcs` 생략으로 원격·자동 커밋 없음. `--no-initial-commit`는 `--vcs` 없을 때 CLI 오류이므로 제외했다.
- 최초 실행 기록(현재 해소): Editor 생성 exit198: 활성 Unity 라이선스 없음. Hub 템플릿 캐시 접근은 sandbox 밖 허용 재시도로 통과했지만 라이선스 오류는 계속됨.
- 최초 실행 기록(현재 해소): PlayMode 시도는 sandbox UPM socket EPERM으로 실행되지 않았고 프로세스를 중단했다. 라이선스 복구와 정확한 Editor 실행 권한이 선행되어야 한다.

## 실행한 자동 검증

- `bash scripts/test-core.sh`: NUnit 3개 PASS, Unity 내장 Mono로 **실제 PlaytestSession 코드** 실행. 솔로 12명/11봇·분리 스폰, 준비/초기변신/생존/다음라운드, 종류·등급별 초기 탄생.
- TDD: 5f6f6ed→b5f036d (첫 세션), 66dba4c→07cf903 (라운드), 56b8ad8→후속 (탄생 식별). RED는 없는 public API 컴파일 실패를 확인했다.
- `bash scripts/compile-runtime.sh`: 설치된 Unity 6000.6.3f1 엔진 어셈블리 대상 C# 컴파일 PASS. Unity 엔진을 실행하지 않는다.
- 실제 Unity PlayMode 이동·점프 테스트를 추가했으나 **실행 차단**. 그 구현에 대해 GREEN이라고 주장하지 않는다.

## 구현된 초안

순수 상태 `PlaytestSession`과 실제 엔진 실행 경계 `UnityPlaytestSession.StartSolo/SubmitInput/Step/Observe`를 연결했다. CharacterController 이동·점프·맵 밖 복귀, 3인칭 camera collision, 시작/HUD/종류등급 탄생/진영/결과, 옥상 계단·구석·창고 blockout, owned asset catalog 연결 및 양 OS 빌드 엔트리다. Physics adapter만 `RecordWorldPosition`을 호출한다; 후속 네트워크 입력을 여기에 직접 매핑해서는 안 된다.

## 남은 조건

모든 S1 AC는 일부 구현 또는 미검증 상태다. 에셋 원본과 catalog 없음, Unity 렌더·물리·한글 UI·쉘터 접근·양 OS 빌드 미검증. 실제 에셋과 blockout 충돌 중복을 정리해야 한다. 봇 행동·전투·설정·멀티는 후속 slice. 어느 PA도 live VERIFIED가 아니다. GitHub Issue 완료 또는 pr-ready 전환하지 않는다.

## In-flight decisions

- Unity 기본 built-in 렌더링 템플릿 사용. 구매 팩 렌더 호환성에 따라 조정한다.
- 테스트 편의를 위해 상태 경계는 엔진 독립 C#, 물리는 Unity adapter. 독립 컴파일은 live 검증 대체가 아니다.
- 첫 데스크톱 WASD/mouse/Space는 legacy input 사용. 새로운 입력 패키지 설정 의존성을 줄이며 향후 gamepad는 후속 범위다.
- 결과5초·인간5m/s·동물5.6m/s·점프1.5m·중력22·카메라5.5m/65°는 제작 초안, 사용자 확정 수치가 아니다.
- Assets/ThirdParty는 중첩 프로젝트에서도 ignore. 실제 자산 catalog까지 private 로컬 경로에 두고 public repo에서 원본을 추적하지 않는다.


## 라이선스 복구 이후 실제 Unity 검증 (2026-09-29)

라이선스 IPC의 기존 프로세스 충돌을 root가 정리하고 GUI Editor 약관 수락 후 정상 진입했다. CLI 템플릿의 구버전 미사용 collab-proxy/inputsystem/visualscripting이 최신 Editor API와 충돌하여 제거했다(b08ba92). 아래 실행으로 앞선 테스트·빌드 차단은 해소됐다.

- Unity EditMode: 3/3 PASS, exit0. `/private/tmp/avh-s1-editmode.xml`.
- Unity PlayMode: 1/1 PASS, exit0. 실제 CharacterController에 공개 입력을 전달하여 이동·점프를 검증. `/private/tmp/avh-s1-playmode.xml`.
- 첫 PlayMode의 앞으로 이동은 앞줄 봇의 정상 몸 충돌에 막혔다. 장애물 없는 경로를 검증하려던 테스트 전제를 바로잡아 왼쪽 빈 광장으로 이동하도록 수정했다. 제품 충돌을 끄지 않았다.
- macOS Development 빌드 PASS, exit0, BuildReport success=true. `game/Builds/macOS/AnimalsVsHumans.app`.
- Windows x64 Development 빌드 PASS, exit0, BuildReport success=true. `game/Builds/Windows/AnimalsVsHumans.exe`.
- 빌드 로그: `/private/tmp/avh-s1-mac-build.log`, `/private/tmp/avh-s1-windows-build.log`.

위 결과는 실제 엔진 테스트와 빌드 생성이다. **구매 에셋 미적용, 실제 Windows 실행/양 OS 플레이, 모든 쉘터 접근, 낙하·복귀, 카메라/한글 UI·재미 검증은 여전히 미완이다.** 어떤 PA도 live VERIFIED로 바꾸지 않는다. Editor가 생성한 GUID/meta 및 프로젝트 버전·설정 migration 파일을 함께 보존한다.


## 보유 에셋 적용 (2026-09-29)

- 실제 구매 팩에서 선택한 prefab 의존성만 `Assets/ThirdParty`로 안전 배치했다. vendor C#/demos/ShaderGraph는 컴파일에 포함하지 않았다. 구매원본·파생 prefab/material/catalog 모두 Git 제외다.
- Polyperfect People 3.02 `man_casual`, Animals 4.1.1 `Fox`, Synty Adventure 1.8.2 마을·벽·바닥·계단 mesh를 사용한다. 화면 동물 이름은 여우, 등급은 일반이다.
- 캐릭터는 vendor 행동 대신 실제 이동 입력과 Animator 걷기/달리기를 연결했다. 원본 모델 콜라이더/root motion은 조작과 충돌하지 않게 분리했다.
- Synty ShaderGraph albedo를 `Generated`의 Standard 파생 머티리얼로 매핑했다. 원본 머티리얼은 보존하며 지면 머티리얼도 persistent asset이다.
- 원래 세 쉘터의 검증된 충돌 공간을 유지하고 Synty mesh로 표현했다. 외곽 마을 건물은 footprint 충돌을 가진다. 창고 입구를 막을 수 있는 장식 stall은 배치하지 않았다.
- 구매 에셋이 로드된 Unity PlayMode **2/2 PASS**: 실제 이동/점프와 공개 이동 입력으로 옥상 접근→일반 낙하→맵 밖 복귀·진영 유지. `/private/tmp/avh-s1-owned-playmode.xml`.
- 폰트는 OS 동적 fallback Apple SD Gothic Neo / Malgun Gothic / Arial을 사용한다. 실제 Mac/Win 한글 표시는 사람 확인 대상이다.
- 실제 양 OS 플레이·모든 역할/경로·렌더·재미 PA 완료는 별도 검증한다.

## Discovery 수정 — S1-DISC-001/002

- RED `9dbf864`: 실제 Unity `Physics.CheckCapsule`이 서쪽 복귀 후보 `(-12,1,0)`의 계단 관통을 재현했다. PlayMode 4개 중 해당 1개 실패, 3개 통과 (`/private/tmp/avh-s1-discovery-red.xml`).
- 복귀 후보를 계단/쉘터 밖의 빈 지상 `(-12,1,-6)`으로 변경했다. 네 후보 모두 실제 capsule clearance를 검사한다.
- 공개 시작 경계에서 randomness seed를 제어해 slot0이 동물로 선정되는 경우를 준비하고, 실제 `Step`으로 20초를 지나 Chase 진입 후 동물의 맵 밖 낙하/복귀를 재현했다. 진영·슬롯 보존을 단정한다.
- GREEN: 실제 PlayMode **4/4 PASS** (`/private/tmp/avh-s1-discovery-green.xml`). 인간의 옥상/낙하 경로와 동물 복귀 경로를 구분한다.
- S1-DISC-007: 사용 내역에서 미배치 stall을 빼고 tree를 추가했다.
- S1-DISC-015: Gitignore 보호를 최적화 실행에서 제거되는 assert 대신 명시적 예외로 바꿨다. 격리된 비ignored Git 저장소에서 일반 Python과 `python -O` 모두 쓰기 전 거부함을 확인했다.
- 나머지 frozen Minor 항목은 이 수정에서 범위를 넓히지 않았다. 실제 PA 판단은 별도다.
