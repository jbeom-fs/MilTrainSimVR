# 헤드셋 실기 결과와 XR Hands 패키지 추가

작성: C · 2026-10-22

## 1. C 헤드셋 실기 — 됐다 (10/22)

[10/19 통지](1019_AB통지_PCVR_XR패키지_설치완료_받는법.md) 6번에서 "아직 안 했다"고 한 헤드셋 테스트를 했다.

| 항목 | 값 |
|---|---|
| 연결 | Meta Quest + **Virtual Desktop** (C 는 Link 대신 VD 를 쓴다). OpenXR 런타임 `VirtualDesktopXR 1.0.10` |
| 실행 | Unity 에디터 Play, `Assets/C/Scenes/C_Sandbox.unity` (XRI Starter Assets 리그) |
| 확인한 것 | 머리·양손 컨트롤러 추적 · 72Hz 화면 · 왼쪽 스틱 이동 · 오른쪽 스틱 회전 · 그립으로 잡기 · 트리거(Activate)로 사격 · 컨트롤러 진동 |
| 프레임 (에디터 Play) | 중앙값 13.9ms (72Hz). 72Hz 를 놓친 프레임 3~5% |

- **Link 로는 아직 확인하지 않았다** (A·B 방식). Windows 빌드 측정도 아직

## 2. 헤드셋 테스트 때 주의 — XR Interaction Simulator 를 끄고 Play

- 씬에 **XR Interaction Simulator 가 켜진 채로** 헤드셋을 연결해 Play 하면, 카메라가 머리를 따라가지 않는다 — 화면이 고개를 따라 같이 돌아서 어지럽다. 시뮬레이터가 켜지는 순간 진짜 헤드셋 입력을 지우기 때문이다 (C 가 10/22 실기에서 겪었다)
- 그래서 **헤드셋으로 테스트할 땐 시뮬레이터 오브젝트를 끄고** Play 한다. 10/19 통지 4-5번(헤드셋 없이 테스트할 때 시뮬레이터를 씬에 넣기)과 같이 쓸 때 주의
- C 는 이걸 자동으로 한다: `Assets/C/Scripts/SimulatorAutoSwitch.cs` — 헤드셋 화면이 켜지면 시뮬레이터를 안 켜고, 없을 때만 켠다
- 같은 방식을 만들 때 주의: OpenXR 은 헤드셋 화면을 **씬 시작보다 몇 프레임 늦게** 켠다. `Awake` 에서 바로 확인하면 "헤드셋 없음"으로 잘못 판단한다 (C 가 처음에 이렇게 틀렸다). C 는 XR 이 켜져 있으면 최대 5초 기다렸다가 판단한다

## 3. XR Hands 패키지 추가 (이 커밋에 포함)

C 가 컨트롤러 모양 대신 **손 모델**을 보여 주려고 넣었다. 손 모델이 XR Hands 의 샘플에 들어 있다.

| 무엇 | 값 |
|---|---|
| 패키지 | XR Hands `com.unity.xr.hands` **1.7.3** — 이 에디터(6000.3.13f1)의 권장 버전 |
| 샘플 | **HandVisualizer** → `Assets/Samples/XR Hands/1.7.3/HandVisualizer/` (손 모델 `Models/LeftHand.fbx` · `RightHand.fbx`, 재질) |
| 같이 바뀐 공용 파일 | `Packages/manifest.json` · `Packages/packages-lock.json` · `Assets/XR/Settings/OpenXR Package Settings.asset` |

- `OpenXR Package Settings.asset` 은 XR Hands 가 설치될 때 **스스로** 바꿨다: OpenXR 기능 항목 4개(Hand Tracking Subsystem · Meta Hand Tracking Aim, PC·Android 탭 각각)를 **꺼진 상태로** 추가했다. 지워지거나 바뀐 줄은 없다. **손 추적(카메라로 맨손 인식)은 켜지 않았다**
- C 의 손은 컨트롤러를 쥔 채 화면에만 보인다: 그립 → 중지·약지·새끼, 트리거 → 검지, 엄지를 스틱·버튼에서 떼면 엄지가 펴진다. 만든 것: `Assets/C/Prefabs/C_ControllerHand_L.prefab` · `C_ControllerHand_R.prefab`, `Assets/C/Scripts/ControllerHandPose.cs`
- 받는 법은 10/19 통지 3번과 같다: 에디터를 닫고 `git pull` → 프로젝트 열기. Package Manager → In Project 에 **XR Hands 1.7.3** 이 보이면 끝

## 4. 부탁

- `Assets/Samples/XR Hands/` 도 공용 샘플 폴더다 — 수정·저장하지 말아 주세요 (10/19 통지 5번과 같다)
- OpenXR 의 Hand Tracking 기능을 켜야 하면 **Docs 에 먼저** 올려 주세요. 켜면 셋의 XR 설정 파일이 같이 바뀐다
- 회신은 각자 폴더(`Docs/A`, `Docs/B`)에
