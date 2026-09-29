# S2 전투 구현 — 검수 전

현재 S2는 S1 ca6c754 위의 stacked 작업이며 S4 b48aa63까지의 설정 계약을 재사용했다. S1 제품 검증이나 integration gate 통과를 뜻하지 않는다.

## 동작

- 공개 PlayerInput의 Attack/Reload/Pitch/Yaw/RoundId를 권한 월드가 처리한다. 원격 입력은 현재 라운드 ID를 명시해야 하며 hit claim을 직접 받지 않는다.
- 인간은 실제 이동하는 버블을 발사한다. 캐릭터·지형 SphereCast 명중 시 터지고, 설정 사거리/수명에서 제거된다. 밀치기 속도는 CharacterController 이동에 합성되어 지형 충돌을 따른다. 피해·처치 상태는 없다.
- 탄창, 연사 간격, R 재장전과 무제한 예비탄을 공개 snapshot/HUD로 표시한다.
- 동물의 명시적 공격 입력에서 앞 방향·근접 거리·지형 가림을 확인하고 한 번에 변신시킨다. 몸 접촉만으로는 변신하지 않는다. 초기2초/중간1초 기본 유예는 별개 설정이며 0초가 가능하다.
- 아군 몸 충돌/진영간 몸 충돌/아군 버블 밀치기는 각각 설정을 소비한다. 아군 밀치기 끄기는 버블이 인간을 통과하게 한다.
- 버블·입력은 round ID로 구분하고 새 라운드에 제거한다. host 절대 게임 시계로 종료를 먼저 판정하여 마지막 변신이 정확히 종료 시각이면 인간 승리다.
- 중간 탄생은 같은 종류·등급을 0.5초 창에서 묶고 4초 표시한다. 이 값과 근접 거리1.8m·재타격0.5초·밀치기 감쇠20m/s²는 제작 중 선택한 초기값이다.

## 검증

실행 호스트는 macOS arm64, Editor는 Unity 6000.6.3f1이다. Windows 빌드 성공은 Windows 실제 플레이 확인을 대신하지 않는다.

| 기준 commit | 검증 | 결과 및 근거 |
|---|---|---|
| f81edc5 + 이후 af081d0에 저장한 테스트 변경 | 이전 Unity PlayMode | 10/10 PASS, `/private/tmp/avh-s2-combat-full.xml`, 같은 이름 `.log`. 정확히 clean commit에서 실행한 결과가 아니므로 최신 head 증거로 사용하지 않는다. |
| c688f95 | standalone core / 엔진 어셈블리 컴파일 | 13/13 PASS / PASS, `/private/tmp/avh-s2-current-core.log`, `/private/tmp/avh-s2-current-compile.log` |
| 09abfd4 | 실제 Unity EditMode / PlayMode | 13/13 PASS / 11/11 PASS, `/private/tmp/avh-s2-final-edit.xml`, `/private/tmp/avh-s2-final-play.xml` 및 각각 `.log` |
| 09abfd4 | macOS Development 빌드 | 성공, `/private/tmp/avh-s2-final-mac-build.log` |
| 이 문서와 함께 수정한 HUD 문구 변경 | 최신 head | 위 결과 이후 변경이며 아직 이 문서 작성 시점에 엔진 재검증·양 OS 빌드 전이다. 완료 후 commit·명령·결과를 `/private/tmp/avh-s2-final-evidence.md`에 별도 고정한다. |

실제 실행 명령(각 검증은 별도 Editor 프로세스로 직렬 실행):

```sh
EDITOR=/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity
PROJECT=/private/tmp/wt-avh-core-playtest-s2/game
"$EDITOR" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode -testResults /private/tmp/avh-s2-final-edit.xml -logFile /private/tmp/avh-s2-final-edit.log
"$EDITOR" -batchmode -projectPath "$PROJECT" -runTests -testPlatform PlayMode -testResults /private/tmp/avh-s2-final-play.xml -logFile /private/tmp/avh-s2-final-play.log
"$EDITOR" -batchmode -quit -projectPath "$PROJECT" -executeMethod AvH.Editor.BuildPlaytest.Mac -logFile /private/tmp/avh-s2-final-mac-build.log
```

- TDD RED/GREEN: ee31384→eeb9322 탄창, 222b8bd→121a075 근접/유예, c2c91f1→5bb15ed 종료경계(±1e-10), ffd3964→132091a 엔진 버블 명중·밀치기.
- 현재 core 13개는 기존 세션4+설정5+전투4다. 실제 PlayMode 11개는 기존 표현/이동6+설정이동1+전투4다.
- 전투 PlayMode는 명중·밀치기, 아군통과와 몸충돌 독립, 유예중 이동·접촉무변신·근접변신, 지형·거리·수명 종료를 확인한다. 임시 설정 파일을 격리하고 정리한다.
- Windows 실제 실행·다중 클라이언트 전투·사람 재미 PA는 미검증이다. HUD 문구는 CONTEXT 승인안으로 맞췄으며 표현을 그대로 복제하는 테스트는 추가하지 않았다.

## 후속 통합

S3 봇은 동일 SubmitInput을 사용하고 S5 네트워크는 host 입력만 제출한다. 공개 ObserveBubbles의 위치/방향/수명과 플레이어 Ammo/ReloadRemaining/AttackGraceRemaining을 원격 표현에 사용할 수 있다. S4 Discovery 수정 0436ece까지 포함했다. 구매 원본은 Assets/ThirdParty Git 제외 경로에만 존재한다.
