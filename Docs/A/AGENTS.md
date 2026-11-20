# Project Working Guidelines — A

팀원 A(남우진)의 MilTrainSimVR 작업 규약. AI 에이전트와 A 본인이 함께 따른다. 이 파일은 `Docs/A`에 둔다. 프로젝트 루트로 옮기거나 복사하지 않는다.

- 프로젝트 루트(A의 PC): `C:\Users\MBC\Desktop\VrProject_2\MilTrainSimVR`
- Unity Editor: `6000.3.13f1` (`ProjectSettings/ProjectVersion.txt`)
- A 문서 위치: `Docs/A`

## 1. 역할

- A 담당: **총기 분해·결합 교육**, **K2 모델 제작**.
- C(이주석)가 A의 K2 모델로 격발·양손 파지를 만든다. 모델의 크기·방향·부품 분리·위치 표시는 C와 맞춘 규격을 따른다 ([C 요청](../C/1019_A요청_K2모델_넣을위치와_필요사항.md)).
- B(김정범): 구급법·화생방 제독. C: 시가지 전투, 팀 XR 패키지 관리.

## 2. 문서 권한

- `Docs/A` 안의 문서는 A 작업 범위 안에서 읽기·작성·수정할 수 있다.
- `Docs/B`, `Docs/C` 등 다른 팀원 폴더는 **읽기 전용**이다. 만들기·고치기·삭제·이름 변경·이동을 하지 않는다. 회신은 `Docs/A`에 쓴다.
- 다른 팀원의 AGENTS.md 규칙은 그 팀원 작업용이다. A 작업에는 이 파일을 적용한다.
- 이 정책은 파일시스템 권한 변경이 아니다.

## 3. 팀 공통 규칙 (C의 10/19 통지 기준)

- 실행 방식: Windows PC VR + Meta Quest Link.
- 패키지: OpenXR `1.16.1`, XRI `3.4.1`, Input System `1.19.0`, ProBuilder `6.0.9`.
- 공용 폴더 `Assets/Samples`, `Assets/XR`, `Assets/XRI`, `Assets/TextMesh Pro`는 수정·저장하지 않는다. 필요하면 A 폴더로 복사해서 쓴다.
- 패키지 추가·버전 변경, XR 설정 변경은 직접 하지 않는다. `Docs/A`에 먼저 제안한다.
- Project Validation의 Optional 경고 2개는 Fix를 누르지 않는다.
- 빌드하면 Unity가 공용 파일을 스스로 고쳐 쓴다 ([C 통지 10/28](../C/1028_AB통지_빌드때_바뀌는_공용파일.md)). 아래 파일이 바뀌어 있으면 **커밋하지 말고 되돌린다**: `Assets/B/Fonts/NotoSansKR-Regular SDF.asset`, `Assets/Settings/PC_RPAsset.asset`, `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`, `Assets/Settings/DefaultVolumeProfile.asset`, `ProjectSettings/UnityConnectSettings.asset`.
- 빌드 때 새로 생기는 `Assets/XR/Settings/OpenXR Editor Settings.asset`(+ `.meta`)은 팀에서 정할 때까지 올리지 않는다 (사람마다 GUID가 달라 pull이 멈춘다).
- 빌드는 공용 빌드 목록(`ProjectSettings/EditorBuildSettings.asset`)을 바꾸지 않고 한다. 결과물은 `Builds/`(git 제외).
- 커밋은 `Assets/A`, `Docs/A`만 스테이징한다. `ProjectSettings/Packages/com.unity.probuilder/Settings.json`처럼 에디터가 저절로 바꾼 공용 설정도 넣지 않는다.

## 4. 작업 방식

- 응답은 간결한 한국어로 쓴다. 정확도, 가정, 장단점, 검증 결과는 줄이지 않는다.
- 구현 전에 가정을 밝히고, 해석이 여러 가지면 묻는다.
- 요청한 범위만 바꾼다. 관련 없는 코드·포맷은 손대지 않는다.
- 공통 기능이 필요하면 재사용 가능하게 작게 만든다. 요청되지 않은 범용 프레임워크는 만들지 않는다.
- 작업마다 확인 기준을 정하고, 확인한 뒤 다음 단계로 넘어간다.
- 커밋 메시지는 `A: ...` 형식으로 시작한다. 커밋·푸시는 요청받았을 때만 한다.

## 5. A 작업 경로와 K2C1 규격

- A의 Unity 작업물은 `Assets/A/` 아래에 둔다 (10/21 C에게 통지).
- K2C1 모델: `Assets/A/Models/K2C1/K2C1.fbx`, 프리팹 `Assets/A/Prefabs/K2C1.prefab`.
- 위치 표시용 빈 오브젝트는 `Anchor_`, 움직이는 부품은 `Part_` 접두어를 쓴다. C에게 알린 이름은 바꾸지 않는다 ([C 회신](1021_C회신_K2C1모델_위치와_규격.md)).

## 6. 미정 — 확정되면 이 파일에 반영

- 자막·안내 관리 방식을 팀 공통(B 제안: `TrainingSubtitles` static class)으로 따를지.
