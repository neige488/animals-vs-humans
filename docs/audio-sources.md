# 소리 출처·라이선스 기록

게임이 쓰는 모든 소리의 출처다(PRD `docs/specs/game-feel.md` PA-5, CONTEXT.md 「실감 개선 정렬」).
허용 출처는 세 가지뿐이다.

- **CC0**: 저작권 표시가 필요 없는 퍼블릭 도메인(CC0 1.0) 소리. 파일을 `game/Assets/AvH/Resources/Audio/`에 커밋한다.
- **보유**: 구매한 Polyperfect 에셋의 소리. 원본은 Git에서 제외된 `Assets/ThirdParty/` 아래에 있고 저장소에 포함하지 않는다. 생성 카탈로그(`ConfigureOwnedAssets.ConfigureSounds`)로만 참조한다.
- **합성**: `AudioDirector`가 실행 중에 코드로 만드는 소리. 파일이 없다.

CC0가 아닌 소리는 사용자 확인 없이 추가하지 않는다. 파일을 불러오지 못하면 같은 큐를 합성음으로 대체하고 플레이를 계속하며, 대체한 큐를 로그와 `AudioDirector.Observe().Fallbacks`로 보고한다.

EditMode 테스트 `AudioSourcesTests`가 이 표를 다시 읽어 (1) 커밋된 모든 소리 파일이 CC0 행과 일치하는지, (2) `AudioCatalog`의 모든 큐·클립이 이 표와 일치하는지, (3) CC0·보유·합성 외의 출처가 없는지 검사한다. 표의 형식(열 순서)을 바꾸면 테스트도 함께 바꾼다.

## 소리 목록

| 파일 | 큐 | 구분 | 원본 | 출처 URL | 라이선스 | 가공 |
|---|---|---|---|---|---|---|
| `Audio/step-human-1` | step-human | CC0 | Kenney Impact Sounds / `Audio/footstep_concrete_000.ogg` | https://kenney.nl/assets/impact-sounds | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/step-human-2` | step-human | CC0 | Kenney Impact Sounds / `Audio/footstep_concrete_001.ogg` | https://kenney.nl/assets/impact-sounds | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/step-human-3` | step-human | CC0 | Kenney Impact Sounds / `Audio/footstep_concrete_002.ogg` | https://kenney.nl/assets/impact-sounds | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/step-paw-1` | step-paw | CC0 | Kenney Impact Sounds / `Audio/footstep_grass_000.ogg` | https://kenney.nl/assets/impact-sounds | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/step-paw-2` | step-paw | CC0 | Kenney Impact Sounds / `Audio/footstep_grass_001.ogg` | https://kenney.nl/assets/impact-sounds | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/step-paw-3` | step-paw | CC0 | Kenney Impact Sounds / `Audio/footstep_grass_002.ogg` | https://kenney.nl/assets/impact-sounds | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/land-heavy` | land | CC0 | Kenney Impact Sounds / `Audio/impactSoft_heavy_000.ogg` | https://kenney.nl/assets/impact-sounds | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/hit-1` | hit | CC0 | Kenney Impact Sounds / `Audio/impactPunch_heavy_000.ogg` | https://kenney.nl/assets/impact-sounds | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/hit-2` | hit | CC0 | Kenney Impact Sounds / `Audio/impactPunch_heavy_001.ogg` | https://kenney.nl/assets/impact-sounds | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/swing` | swing | CC0 | Kenney RPG Audio / `Audio/cloth1.ogg` | https://kenney.nl/assets/rpg-audio | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/reload` | reload | CC0 | Kenney RPG Audio / `Audio/metalLatch.ogg` | https://kenney.nl/assets/rpg-audio | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/dry-fire` | dry-fire | CC0 | Kenney RPG Audio / `Audio/metalClick.ogg` | https://kenney.nl/assets/rpg-audio | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/transform` | transform | CC0 | Kenney Digital Audio / `Audio/phaserUp3.ogg` | https://kenney.nl/assets/digital-audio | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/stagger` | stagger | CC0 | Kenney Digital Audio / `Audio/lowDown.ogg` | https://kenney.nl/assets/digital-audio | CC0 1.0 (팩 License.txt) | 이름만 변경, 원본 ogg 그대로 |
| `Audio/ambience-village` | ambience | CC0 | OpenGameArt "Forest Ambience" (tinyworlds) / `Forest_Ambience.mp3` | https://opengameart.org/content/forest-ambience | CC0 1.0 (페이지 라이선스 단일 CC0) | ffmpeg으로 ogg vorbis(q1, 44.1kHz 스테레오) 변환, 길이 그대로 |
| `Audio/music-prepare` | music-prepare | CC0 | OpenGameArt "Town Theme RPG" (cynicmusic) / `TownTheme.mp3` | https://opengameart.org/content/town-theme-rpg | CC0 1.0 (페이지 라이선스 단일 CC0) | ffmpeg으로 ogg vorbis(q1, 44.1kHz 스테레오) 변환, 길이 그대로 |
| `Audio/music-chase` | music-chase | CC0 | OpenGameArt "Battle Theme A" (cynicmusic) / `battleThemeA.mp3` | https://opengameart.org/content/battle-theme-a | CC0 1.0 (페이지 라이선스 단일 CC0) | ffmpeg으로 ogg vorbis(q1, 44.1kHz 스테레오) 변환, 길이 그대로 |
| `Audio/music-final` | music-final | CC0 | OpenGameArt "5 Chiptunes (Action)" (Juhani Junkala / SubspaceAudio) / `Juhani Junkala [Retro Game Music Pack] Level 3.wav` | https://opengameart.org/content/5-chiptunes-action | CC0 1.0 (페이지 라이선스 단일 CC0, 팩 INFO.txt에 CC0 명시) | ffmpeg으로 ogg vorbis(q1, 44.1kHz 스테레오) 변환, 길이 그대로 |
| `SFX_Bear_Calm` | snarl | 보유 | Polyperfect Low Poly Animated Animals 4.1.1 / `Sounds/SFX_Bear_Calm.ogg` | Unity Asset Store 구매 에셋 | 구매 에셋 라이선스(저장소 미포함) | 원본 그대로 참조, 공격 예비동작 으르렁으로 종별 음높이만 바꿔 재생 |
| `SFX_Bear_Growl_2` | growl-bear | 보유 | Polyperfect Low Poly Animated Animals 4.1.1 / `Sounds/SFX_Bear_Growl_2.ogg` | Unity Asset Store 구매 에셋 | 구매 에셋 라이선스(저장소 미포함) | 원본 그대로 참조, 불곰·멧돼지(높은 음) 울음 |
| `SFX_Wolf_Howl` | growl-wolf | 보유 | Polyperfect Low Poly Animated Animals 4.1.1 / `Sounds/SFX_Wolf_Howl.ogg` | Unity Asset Store 구매 에셋 | 구매 에셋 라이선스(저장소 미포함) | 원본 그대로 참조, 늑대·여우(높은 음) 울음 |
| `SFX_Penguin` | growl-penguin | 보유 | Polyperfect Low Poly Animated Animals 4.1.1 / `Sounds/SFX_Penguin.ogg` | Unity Asset Store 구매 에셋 | 구매 에셋 라이선스(저장소 미포함) | 원본 그대로 참조, 펭귄 울음 |
| `SFX_Ice_Squeels` | growl-rabbit | 보유 | Polyperfect Low Poly Animated Animals 4.1.1 / `Sounds/SFX_Ice_Squeels.ogg` | Unity Asset Store 구매 에셋 | 구매 에셋 라이선스(저장소 미포함) | 원본 그대로 참조, 토끼 찍찍 소리(높은 음) |
| (합성) | fire | 합성 | 기존 `PrimitiveEffects` 버블 발사음 합성 코드를 `AudioDirector`로 옮김 | - | 자체 생성 | 22050Hz 노이즈 퍼프 |
| (합성) | prop-knock | 합성 | 밀리는 소품(상자·통·항아리)이 몸·버블에 밀릴 때의 나무 두드림, `SoundSynth.Thump` 재사용 | - | 자체 생성 | 22050Hz 210Hz 짧은 둔탁음+노이즈 0.14초, 소품마다 음높이 조금 다름 |
| (합성) | pop | 합성 | 기존 `PrimitiveEffects` 버블 파열음 합성 코드를 `AudioDirector`로 옮김 | - | 자체 생성 | 22050Hz 사인 스윕+노이즈 |

## 가져오기 설정

커밋한 파일 자체는 표의 가공 내용 외에는 바꾸지 않는다. Unity 가져오기 규칙(`Editor/AudioImportRules.cs`)이 효과음은 모노로 합치고(3D 위치 재생용) 음악·환경음은 압축 스트리밍으로 가져온다. 빌드에 들어가는 형태만 바뀌고 원본 파일은 그대로다.

## 합성 대체

위 표의 CC0·보유 큐도 파일을 불러오지 못하면(파일 삭제, 구매 에셋 없는 환경, 카탈로그 미생성) `AudioDirector`가 같은 큐 이름의 합성음(발소리 노이즈 탁음, 으르렁 톱니파, 휘두름 바람 소리, 변신 상승음, 단계별 아르페지오 배경음악, 바람·새소리 환경음 등)으로 대체한다. 합성음은 자체 생성이므로 라이선스 제약이 없다.

## 다운로드 기록

- 2026-10-07 Kenney 팩 zip 3개(impact-sounds, rpg-audio, digital-audio)를 kenney.nl 자산 페이지의 다운로드 링크로 받았다. 각 팩의 `License.txt`가 "License (Creative Commons Zero, CC0)"를 명시한다. 필요한 파일만 추출하고 zip은 삭제했다.
- 2026-10-07 OpenGameArt 4개 작품 페이지의 라이선스 표시가 CC0 하나뿐임을 확인하고 원본을 받았다. 여러 라이선스가 함께 표시된 작품은 쓰지 않았다.
