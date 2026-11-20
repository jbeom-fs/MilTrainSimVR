# PC VR XR 패키지 설치 완료 — 받는 법과 각자 PC 설정

작성: C · 2026-10-19

## 1. 정해진 것

- 실행 방식: **PC VR (Meta Quest Link)**, Windows 빌드 — 10/19 팀 결정
- XR 패키지는 **C 가 한 번 설치해서 이 커밋으로 올렸다.** A·B 는 따로 설치하지 말고 **pull 만** 하면 된다 (`Packages/manifest.json` 은 셋이 같이 쓰는 파일이라 각자 설치하면 버전이 갈라진다)

## 2. 설치한 것 (이 커밋에 포함)

### 패키지

| 패키지 | 이름 | 버전 |
|---|---|---|
| OpenXR Plugin | `com.unity.xr.openxr` | **1.16.1** |
| XR Interaction Toolkit | `com.unity.xr.interaction.toolkit` | **3.4.1** |
| ProBuilder | `com.unity.probuilder` | **6.0.9** |
| (위 패키지가 같이 설치) | XR Plugin Management `4.5.4` · XR Core Utils `2.5.3` · XR Legacy Input Helpers `2.1.13` | |

원래 있던 것: Input System `1.19.0` · AI Navigation `2.0.12` · Shader Graph `17.3.0` (URP 에 포함)

### 샘플·리소스

| 무엇 | 위치 | 용도 |
|---|---|---|
| XRI 샘플 **Starter Assets** | `Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/` | VR 리그 프리팹 `Prefabs/XR Origin (XR Rig).prefab`, 데모 씬 `DemoScene.unity` |
| XRI 샘플 **XR Interaction Simulator** | `Assets/Samples/XR Interaction Toolkit/3.4.1/XR Interaction Simulator/` | 헤드셋 없이 키보드·마우스로 테스트. 구버전 XR Device Simulator 는 XRI 가 Legacy 로 표시해서 넣지 않았다 |
| TMP Essential Resources | `Assets/TextMesh Pro/` | Starter Assets 데모 씬의 글자 표시에 필요 |

### XR 설정 (Edit → Project Settings)

| 항목 | 값 |
|---|---|
| XR Plug-in Management → **PC (모니터 아이콘) 탭** | **OpenXR 켬** (Android 탭은 손대지 않았다) |
| OpenXR → Enabled Interaction Profiles | **Oculus Touch Controller Profile** |
| OpenXR → Render Mode | Single Pass Instanced (기본값 그대로) |
| Player → Run In Background | **켬** — Project Validation 요구 사항. 헤드셋 메뉴를 열어 포커스를 잃어도 화면이 머리에 붙어 따라오지 않게 한다 |
| XRI Interaction Layer 31 | `Teleport` — Starter Assets 요구 사항 |

**Project Validation: 오류 0.** `[Optional]` 경고 2개(PoseControl · StickControl 전환)는 **일부러 남겨 뒀다.** 각자 Fix 를 누르면 셋의 설정이 갈라지므로 누르지 말아 주세요.

## 3. 받는 법 (A·B 각자)

1. Unity 에디터를 닫는다
2. `git pull`
3. Unity Hub 에서 프로젝트를 연다 — 패키지를 자동으로 받는다 (처음엔 몇 분 걸릴 수 있다)
4. 확인: Window → Package Manager → **In Project** 에 위 표의 버전이 보이면 끝

## 4. 각자 PC 에서 할 것 (git 으로 넘어가지 않는다)

1. **Meta Horizon Link** 앱(PC 용)을 설치하고 로그인 → 헤드셋을 연결한다 (USB-C 케이블 Link 또는 Air Link)
2. Link 앱 → 설정 → 일반 → **OpenXR 런타임** 을 Meta Horizon Link 로 지정한다
3. 노트북처럼 GPU 가 두 개인 PC 는 Windows 설정 → 시스템 → 디스플레이 → 그래픽 에서 `Unity.exe` 를 **고성능(외장 GPU)** 으로 지정한다
4. 헤드셋 테스트: `Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/DemoScene.unity` 를 열고 Play → 헤드셋에 화면이 보이고 스틱으로 움직이면 성공
5. 헤드셋 없이 테스트할 때: `XR Interaction Simulator/XR Interaction Simulator.prefab` 을 **자기 씬에** 끌어다 놓고 Play

## 5. 부탁

- 공용 폴더 `Assets/Samples/` · `Assets/XR/` · `Assets/XRI/` · `Assets/TextMesh Pro/` 안의 파일은 수정·저장하지 말아 주세요. 샘플을 고쳐 쓰려면 자기 폴더로 복사해서 쓴다. 데모 씬도 열어 보기만 한다
- 패키지 추가·버전 변경, XR 설정 변경이 필요하면 **Docs 에 먼저** 올려 주세요. XR 설정은 모든 플랫폼 설정이 파일 하나(`Assets/XR/XRGeneralSettingsPerBuildTarget.asset`, `Assets/XR/Settings/OpenXR Package Settings.asset`)에 들어 있어서 각자 바꾸면 충돌한다
- 회신은 각자 폴더(`Docs/A`, `Docs/B`)에

## 6. 아직 확인 안 된 것

- ~~C 의 헤드셋 실기 테스트(위 4번) — 아직 안 했다. 확인하면 다시 알린다~~ → **10/22 확인했다** (Virtual Desktop). 결과와 주의점: [1022 통지](1022_AB통지_헤드셋실기_XRHands추가.md)
