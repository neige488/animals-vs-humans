# S1 구현 체크포인트 — 미완·제품 검증 차단

- 프로젝트: `game/`, Unity 6000.6.3f1, `com.unity.template.3d`.
- Unity CLI `projects create ... --no-cloud`로 생성. `--vcs` 생략으로 원격·자동 커밋 없음. `--no-initial-commit`는 `--vcs` 없을 때 CLI 오류이므로 제외했다.
- Editor 생성 exit198: 활성 Unity 라이선스 없음. Hub 템플릿 캐시 접근은 sandbox 밖 허용 재시도로 통과했지만 라이선스 오류는 계속됨.
- PlayMode 시도는 sandbox UPM socket EPERM으로 실행되지 않았고 프로세스를 중단했다. 라이선스 복구와 정확한 Editor 실행 권한이 선행되어야 한다.

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
