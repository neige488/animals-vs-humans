# Animals vs Humans Unity 프로젝트 — S1 작업 중

Unity CLI로 생성한 Unity 6000.6.3f1 기본 3D 프로젝트입니다. 현재 **개발 블록아웃**이며 보유 자산 적용과 실제 Editor/빌드 검증이 끝나지 않았습니다. 봇은 슬롯만 존재하며 행동은 S3, 전투는 S2 범위입니다.

## 열기와 검증

Unity Hub에서 이 `game` 폴더를 열고 `Assets/Scenes/SampleScene.unity`를 실행합니다. 시작 화면에서 닉네임을 넣고 혼자 시작합니다. WASD·마우스·Space, Esc 메뉴입니다. 메뉴를 열어도 라운드는 진행됩니다.

저장소 루트에서 `bash scripts/test-core.sh`는 Unity에 포함된 Mono와 NUnit으로 **같은 실제 세션 상태 코드**의 공개 경계를 실행합니다. Unity 실행·물리 검증은 아닙니다. `bash scripts/compile-runtime.sh`는 설치된 Unity 어셈블리에 대해 C# 컴파일만 검사합니다.

Unity 라이선스를 활성화한 뒤 다음을 실행합니다(아래 UNITY는 설치된 Editor 실행파일의 경로):

```sh
"$UNITY" -batchmode -nographics -projectPath "$PWD/game" -runTests -testPlatform EditMode -testResults /tmp/avh-editmode.xml -logFile /tmp/avh-editmode.log
"$UNITY" -batchmode -nographics -projectPath "$PWD/game" -runTests -testPlatform PlayMode -testResults /tmp/avh-playmode.xml -logFile /tmp/avh-playmode.log
"$UNITY" -batchmode -quit -projectPath "$PWD/game" -executeMethod AvH.Editor.BuildPlaytest.Mac -logFile /tmp/avh-mac-build.log
"$UNITY" -batchmode -quit -projectPath "$PWD/game" -executeMethod AvH.Editor.BuildPlaytest.Windows -logFile /tmp/avh-windows-build.log
```

## 구매 자산 연결

원본 구매 자산은 반드시 `Assets/ThirdParty/` 아래에 두고 공개 Git에 넣지 않습니다. Unity Asset Store 구매 소유권을 확인한 계정에서 다운로드하고 패키지의 원래 경로가 공개 추적 영역으로 퍼지지 않도록 임포트 전에 검사해야 합니다.

Create → Animals vs Humans → Owned asset catalog로 `Assets/ThirdParty/Resources/OwnedAssetCatalog.asset`을 만듭니다. `Human`, `Animal`은 Polyperfect 모델 prefab, `Village`는 Synty 모델로 구성한 마을 prefab입니다. `AnimalDisplayName`에는 실제 동물 종류, `Rarity`와 `RarityColor`에는 이번 모델의 등급 글자·색상을 입력합니다. Source 필드에는 사용 팩·버전·모델 이름을 씁니다. 캐릭터 pivot은 발바닥, 크기는 미터 단위를 기대합니다. 런타임은 모델 내부 collider를 끄고 별도 CharacterController를 사용합니다.

현재 마을 blockout 충돌체와 에셋 마을은 함께 생성됩니다. 최종 지형과 blockout 일치·중복 충돌·계단 통행·쉘터 동선은 실제 에셋으로 반드시 검증·조정해야 합니다. 원본 모델이 단순히 연결됐다는 이유로 PA 통과로 처리하지 않습니다. catalog가 없으면 개발 블록아웃 안내와 임시 도형이 보입니다.

## 초기값과 한계

사용자 합의 기본값은 준비20초·추격180초·초기 동물2명·12슬롯입니다. 제작 중 선택한 값은 결과5초, 인간5m/s·동물5.6m/s, 점프1.5m·중력22m/s², 카메라5.5m·65°입니다. 아트·기본 충돌 맵과 수치는 가역적인 초안입니다. 입력은 첫 데스크톱 범위의 Unity legacy input을 사용합니다.

라이선스 복구 후 실제 Unity EditMode 3개·PlayMode 이동/점프 1개와 Windows/macOS Development 빌드 생성은 통과했습니다. 실제 Windows 실행·렌더·양쪽 쉘터 접근·낙하복귀와 제품 PA는 미검증입니다. 보유 자산 적용, UI 한글 폰트 표시 및 실제 카메라 조작도 확인해야 합니다.
