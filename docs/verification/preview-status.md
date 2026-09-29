# 기본 구현 검수용 빌드 — 2026-09-29

12명·3인칭·보유 에셋 기반 기본 플레이, 버블 밀치기/근접 변신, 인간·동물 봇, 다음 라운드 설정, 비공개 LAN/기존 VPN 방과 봇 인계·재접속을 조립했다. 캐릭터별 특수능력과 뽑기는 후속 범위다.

## 검증된 입력

실행 커밋은 `727bf8b0c46a7742a80e6361726bdd3177175c03`이다. `8688118`은 봇 원본 브랜치 이력과 문서만 병합했으며 game/scripts 차이는 없다. 구매 에셋 catalog가 존재하는 Unity 6000.6.3f1 환경에서 실제 EditMode34/34·PlayMode23/23, macOS·Windows 빌드가 성공했다. Windows 전체 의존파일256개의 ZIP CRC와 SHA256도 검증했다. macOS 앱은 실제 시작 로그의 Unity·그래픽 초기화 및 예외 없음, 실행 프로세스를 확인했다. 시작 확인은 전체 실제 플레이 검증을 대신하지 않는다.

코드 독립 Final 검토는 S1 `4d8b6f1`, S2 `f669ffc`, S3 `a4c582a`, S4 `0436ece`, S5 `5e0d9cf`에서 각각 PASS했다. 이는 Slice 코드 검토이며 공식 integration의 Run-level Final 또는 제품 VERIFIED는 아니다. 조립 후 ResultSeconds 스키마를 포함해 네트워크 버전4와 지문을 함께 갱신했다.

## 로컬 산출물

프로젝트 루트의 Git 제외 `Builds/Playtest-727bf8b/`에 macOS 앱, Windows ZIP, 플레이 안내, 빌드 영수증과 실행 근거를 보관한다. 구매 원본과 파생 catalog는 공개 Git에 포함하지 않는다.

## 후속 확인

사용자의 요청대로 Windows 실제 실행은 후순위다. Windows/macOS 별도 기기 공방, 2~4인 재미·역할별 체감, 화면·연속 영상, 실제 렌더 프레임 성능 검증이 남았다. bots의 복잡한 막힘/혼잡과 원격 입력 지연은 실제 플레이에서 조정할 항목이다. 봇의 실제 버블 피격은 방어 공방으로 관측하며 이를 이동 불능으로 오인하지 않도록 회귀를 보강했다.

main과 공식 `autopilot/core-playtest`에는 구현을 완료 통합하지 않았고, Issue도 제품 검증 전에는 닫지 않는다. 이번 산출물은 검수용 개발 빌드다. 사용자 플레이 안내는 [playtest-guide.md](../playtest-guide.md)를 따른다.
