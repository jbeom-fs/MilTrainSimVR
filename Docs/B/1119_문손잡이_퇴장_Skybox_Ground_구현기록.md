# 1119 구급법·화생방 문 손잡이 퇴장 · Skybox · Ground 구현 기록

- 작성 2026-11-19. 시작 HEAD `4eed8a3`, 시작할 때 작업 트리는 깨끗했다.
- **상태: 코드·프리팹·두 씬 파일 변경과 정적 대조 후 사용자가 실제 검증 완료를 보고했다.** 에이전트는 Unity 실행, Play, 컴파일러 실행, 테스트, 빌드를 하지 않았다. 개별 입력·측정값은 따로 수집하지 않았으며 §7은 확인 기준으로 보존한다. 후속 실내 광원 배치 요청은 §9를 참고한다.
- 후속 보고서·실제 코드·씬 대조에서 추가 필수 수정 오류는 발견하지 못했다. 문판 선택 시험의 거리 기준을 실제 판정식에 맞춰 정정했다. 상세는 §8을 참고한다.
- 대상 씬은 `Assets/B/Scene/B_FirstAid.unity`와 `B_CBRN.unity`다. A·C·Samples·Packages·ProjectSettings는 수정하지 않았다. 스테이징·커밋·Push도 하지 않았다.

## 1. 변경 파일

| 파일 | 목적 |
|---|---|
| `Assets/B/Scripts/Interaction/HubExitHandle.cs`(+.meta, 신규, guid `3f3e6a297e5940a09468c3239c267561`) | 문 손잡이를 근접 Grab했을 때만 페이드 후 허브로 복귀한다. 근접 판정 hover·select 필터와 퇴장 실행을 함께 담당한다. |
| `Assets/B/Prefabs/TrainingEnvironment/B_OutdoorGround.prefab`(+.meta, 신규, guid `82518c5e77484cb6b53739547d56994a`) | 두 씬이 함께 쓰는 외부 지면이다. |
| `Assets/B/Scene/B_FirstAid.unity`, `B_CBRN.unity` | 포털·명판을 제거하고 손잡이, Ground, Skybox를 연결한다. |
| `TrainingField/TrainingFieldCoordinator.cs` | 출구 설명 주석을 문 손잡이 방식으로 고쳤다. |
| `TrainingField/TrainingSubtitles.cs` | 포털 명판 문구 `HubPortalSign`을 삭제하고 머리 주석을 정리했다. |
| `TrainingField/TrainingFieldSfx.cs` | 주석에서 "hub portal"을 "hub exit door knob"으로 바꿨다. |

asmdef는 바꾸지 않았다. `MilTrainSimVR.B.Interaction`이 이미 A.Common, B.CommonMenu, XRI, InputSystem, CoreUtils를 참조하기 때문이다. 새 참조나 순환 참조는 없다.

## 2. 포털 제거 범위

두 씬에서 `ExitAnchor` 루트 하위 전체를 삭제했다. 씬당 12개 문서다.

- `ExitAnchor`
- A `HubPortal_ToHub` 인스턴스: 렌즈·EdgeTint·Sparkles·Label·`HubPortal` 머리 진입 판정·레이 선택을 포함한다.
- `HubPlaque`: 명판 Visual 인스턴스 포함.
- `HubSign`: TMP "연병장".

`SceneRoots`에서도 `ExitAnchor`를 뺐다. 제거한 ID를 참조하는 문서는 남아 있지 않다.

다음은 유지했다.

- `SceneSystems/ScreenFader`: 손잡이 퇴장이 재사용한다.
- `EntryAnchor` (0, 0, −1.85), Y 180°.
- `Entrance_Stand` 교육 목록.
- A의 `HubPortal_ToHub.prefab` 원본과 A_Hub 입장 포털.

## 3. 손잡이 위치와 Collider

- **실제 손잡이 확인:** `sage_glass_door.fbx`를 Blender 5.2 백그라운드로 읽기만 하고 저장하지 않았다. 해시는 A 사본과 같다.
  - 손잡이 메시는 `Round_knob_Front/Back`(Blender x 0.313~0.377, 높이 0.968~1.032, 문 두께 방향 ±0.047~0.094)과 `Rose`, `Neck`이다.
  - 프리팹의 Model 회전(−90° X)과 Unity의 x 반전을 적용하면, 문 기준 실내 쪽 손잡이는 x −0.377~−0.313, y 0.968~1.032, z −0.094~−0.047이다.
  - A_Sandbox `Handle_Barracks` 위치(−0.345, 1.0, −0.0705)와 일치한다.
- **배치:** `B_BarracksShell` 인스턴스의 `Door_Range` 아래에 B 소유 자식 `HubExitHandle`을 추가했다.
  - 씬 파일에서는 PrefabInstance의 `m_AddedGameObjects`와 Door_Range stripped Transform으로 연결된다. FBX와 셸 프리팹 원본은 무변경이다.
  - 경로: `EnvironmentRoot/B_BarracksShell/Door_Range/HubExitHandle`(ID는 구급법 `8600000000000000002`, 화생방 `8610000000000000002`).
  - 셸·Door_Range가 원점·무회전이라 로컬 좌표와 월드 좌표가 같다.
- **Collider:** BoxCollider, 트리거 아님, 레이어 0(Default).

  | 항목 | 값 |
  |---|---|
  | Transform 위치 | (−0.345, 1.0, −0.07) |
  | center | (0, 0, 0) |
  | size | (0.11, 0.11, 0.07) |
  | 월드 범위 | x −0.40~−0.29, y 0.945~1.055, z −0.105~−0.035 |

  손잡이 둥근 머리를 사방 약 1.1~1.8 cm 여유로 감싼다. 문판 면(z −0.021)과 경첩 쪽 문판은 포함하지 않는다.
- **컴포넌트:**
  - `XRSimpleInteractable`: A Handle_Barracks와 같은 값이다. 상호작용 레이어 Default, Single 선택, Colliders는 자동(자기 BoxCollider)이다.
  - `HubExitHandle`: knobCollider = 위 BoxCollider, fader = 씬 ScreenFader, palmReach 0.1, fadeOutSeconds 0.4.
  - A `DoorTransition`은 붙이지 않았다(§4).
- **문판 Collider와의 관계:**
  - `Door_Range` BoxCollider(1.016 × 2.644 × 0.194, z −0.097~0.097)가 손잡이 상자와 겹친다.
  - XRI 근접 캐스터는 OverlapSphere/SphereCast 결과 중 Interactable에 등록된 Collider만 대상으로 삼는다. 따라서 등록되지 않은 문판 Collider는 손잡이 감지를 가리지 않는다. 최대 10개 결과 안에는 문, 뒷판, 벽 몇 개만 들어온다.
  - 문·벽 Collider는 변경하지 않았다.
  - 원거리 레이는 문판에 먼저 맞을 수 있지만 어차피 거절 대상이다.

## 4. 근접 Grab 판정 근거와 원거리 차단

근거는 설치된 XRI 3.4.1 코드와 리그 설정이다.

- **선택 입력:** `XRBaseInputInteractor.PreprocessInteractor(Dynamic)`가 선택 입력의 `logicalSelectState`를 갱신한다. 그다음 같은 프레임에 매니저가 선택 필터를 평가한다.
- **B 리그(XRI Starter 기본, B 변형·씬 override 없음):**
  - `m_SelectActionTrigger` = StateChange: 누른 프레임에만 선택을 시작한다.
  - 근접 캐스터: `SphereInteractionCaster`, 반경 0.1 m, 트리거 무시, 레이어 마스크 Default, 원점 = 손바닥.
- **유효 대상 등록:** `NearFarInteractor`는 근거리·원거리 모두 `IsHoverPossible`을 통과한 Interactable만 유효 대상으로 등록한다(`RegisterNearValidTargets`/`RegisterFarValidTargets`).
- **selectionRegion을 쓰지 않는 이유:** `selectionRegion`은 선택 뒤 attach offset으로 정해진다(`DetermineSelectionRegion`). B 리그는 원거리 부착 설정이 조정돼 있어 판정에 쓰지 않았다.

`HubExitHandle`은 손잡이 Interactable의 hover·select 필터다. `canProcess`가 항상 true라서, 컴포넌트가 꺼지거나 설정이 무효이면 모든 hover·select를 거절한다.

1. **근접 공간 조건(hover·select 공통):**
   - 선택하는 손이 `NearFarInteractor`이고, near casting이 켜져 있다.
   - 근접 캐스터가 `SphereInteractionCaster`이며 활성 상태다. 캐스터 레이어 마스크에 손잡이 레이어가 포함돼 있다.
   - 캐스터의 `effectiveCastOrigin`(손바닥)에서 손잡이 BoxCollider까지의 `ClosestPoint` 거리가 기준 이하다. 기준은 min(castRadius × lossyScale.x, palmReach 0.1 m)이다.
   - 즉 실제 근접 캐스터 구가 지금 손잡이 상자와 겹쳐야 한다.
   - 타입이나 머리 거리만으로는 허용하지 않는다.
2. **원거리 차단:**
   - 같은 조건이 hover 필터에도 걸려 있다. 따라서 원거리 레이는 손잡이를 유효 대상으로 만들지 못하고, 원거리 선택 자체가 생기지 않는다.
   - 나중에 Near로 승격될 원거리 선택이 존재하지 않는다.
3. **선택한 순간의 조건(select만):**
   - `logicalSelectState.wasPerformedThisFrame`: 이번 프레임에 누른 입력이어야 한다. 수동 상호작용은 거절한다.
   - 멀리서 누른 채 다가오거나 손을 갖다 대도 선택되지 않는다. 놓고 손잡이에서 다시 누르면 선택된다.
   - `HandTracking.IsPoseTracked`: 손 위치·회전 추적이 유효해야 한다.
   - `Application.isFocused`이고 `OnApplicationPause` 상태가 아니어야 한다.
   - `CommonPauseMenu.IsPaused`가 아니어야 한다. 메뉴의 입력 게이트도 열려 있는 동안 모든 선택을 거절한다.
   - `CourseRoute.CanLoad(A_Hub)`가 참이어야 한다.
   - 퇴장이 이미 진행 중이 아니어야 한다.
4. **이미 잡고 있는 선택:** XRI는 선택 유지 중에도 필터를 다시 검사한다. 이때는 항상 통과시켜 필터가 선택을 끊지 않게 했다.

## 5. 허브 복귀·중복 방지·왕복

- **복귀 흐름:** 선택이 성공하면(`selectEntered`) `leaving`을 true로 바꾼다. 그 뒤 씬 ScreenFader로 0.4초 동안 검게 하고, A `CourseRoute.ReturnToHub()`를 호출한다.
  - `Last`는 덮어쓰지 않는다. 허브에서 들어올 때 `Enter`가 기록한 과정 ID가 유지된다.
  - 허브 경로는 `CourseRoute.HubScenePath`만 쓴다.
- **중복 방지:**
  - 양손이 동시에 누르면 같은 프레임에 첫 선택이 `leaving`을 세운다. 두 번째 손은 필터에서 거절된다.
  - 연속 입력도 같다. 화면 전환 시작은 한 번뿐이다.
- **왕복 후 동작:** 상태는 인스턴스 값뿐이다. 다시 들어오면 새 객체가 초기 상태로 시작한다. 정적 값은 쓰지 않는다.
  - A `DoorTransition`의 정적 `s_Moving`은 페이드 도중 씬이 바뀌면 true로 남을 수 있다. 그래서 재사용하지 않고 ScreenFader와 CourseRoute를 직접 사용했다.
  - 페이드 도중 이 객체가 꺼지면 코루틴을 멈추고 화면을 다시 밝게 한다. `ReturnToHub`를 호출한 뒤에는 되돌리지 않는다.
- **교육·메뉴와의 관계:**
  - 교육 진행 중에도 의도적인 퇴장은 가능하다. 기존 포털과 같다.
  - 페이드 도중 공통 메뉴가 열려도 언로드할 때 메뉴의 `OnDisable`이 시간·오디오를 복원한다.
- **추가하지 않은 것:** 안내 문구, 퇴장 확인창, 버튼, hover 강조.

## 6. Skybox · Ground

- **Skybox:**
  - 두 씬의 `RenderSettings.m_SkyboxMaterial`을 A_Sandbox 저장값과 같게 바꿨다. 기존 값은 기본 절차형 `10304`였다.
  - 적용값은 `{fileID: 2100000, guid: 01be7231491695047b44efa1909dbbb4}`(`Skybox_Terra 4 Medium.mat`, 6 Sided, Exposure 1, Rotation 221, Tint 0.5)다. 작업 시점에 A_Sandbox에서 다시 확인했다.
  - Fog, Ambient, 조명, 노출, Reflection 값은 복사하지 않았다. A는 Fog 켬, Ambient 0.15, Reflection 0.25다. B는 Fog 끔, Ambient 1, Reflection 1을 유지했다.
  - **HMD 카메라:** Starter `XR Origin (XR Rig)` Main Camera의 `m_ClearFlags: 1`(Skybox)이다. B 변형·씬 override는 없다. 거울·후면 카메라와 RenderTexture는 변경하지 않았다.
  - **주의(미검증):** 두 B 씬의 Ambient/Reflection 소스가 Skybox(`m_AmbientMode 0`)이고, 조명 데이터는 기본값 20201이다. Terra 4 텍스처의 6면 선형 평균은 약 (0.145, 0.136, 0.124)로, 기본 하늘보다 낮고 따뜻할 수 있다.
    - 이 경우 실내 그늘 면이 약간 어둡거나 노랗게 보일 수 있다. 실제 변화 여부는 사용자 확인 ⑪에서 본다.
    - 가독성이 떨어지면 보정 후보는 Ambient Intensity 1 → 1.3~1.5다. 이번에는 적용하지 않았다.
- **Ground:**
  - A_Sandbox `Ground`를 확인했다. 기본 Plane(`fileID 10209`), `Assets/A/Materials/Outdoor/Dirt.mat`(guid `caf490b9dffba144897617afbea9131c`), 배율 (200, 1, 200), y −0.02이며 Collider는 없다.
  - Mesh와 재질을 읽기 전용으로 참조하는 B 프리팹 `B_OutdoorGround`를 만들었다.
    - 루트에 Transform, MeshFilter, MeshRenderer만 있다. Collider, 상호작용, TeleportationArea는 없다.
    - 그림자는 받기만 하고 드리우지 않는다(`m_CastShadows 0`). 정적 플래그는 없다.
  - 두 씬의 `EnvironmentRoot` 아래에 인스턴스를 하나씩 두었다. 위치 (0, −0.02, −4.72), 회전 0, 배율 (200, 1, 200)이다. 건물 중심 기준 약 2000 × 2000 m가 된다.
  - **높이:** 실내 바닥 `Shell/Floor` 상자는 y −0.1~0, x −4~4, z 0~−9.44다. 지면은 그 윗면보다 2 cm 아래, 상자 내부에 있다.
    - 실내에서는 바닥이 지면을 덮어 보이지 않는다. 같은 높이의 면이 없어 깜빡임 조건도 없다.
    - 실내 바닥, Walk_* 순간이동 영역, 벽·문 충돌은 변경하지 않았다.
    - 지면에 Collider가 없으므로 건물 밖이 순간이동 목적지가 되지 않는다.

## 7. 정적 검토 결과와 사용자 확인

### 정적 검토 결과(실행 통과가 아님)

- **씬 대조:** 두 씬 모두 다음 항목에서 문제를 찾지 못했다.
  - 문서 ID 중복
  - 로컬 fileID 미해결 참조
  - Transform 부모·자식 불일치
  - 추가 객체 미등록
  - PrefabInstance 부모 누락
  - SceneRoots 누락
  - SceneRoots의 추가 항목 1개는 기존 공통 메뉴 루트 PrefabInstance다.
- **잔여 객체:** `ExitAnchor`, `HubPlaque`, `HubSign`, `HubPortal_ToHub` 이름이 남아 있지 않다. `HubExitHandle`, `B_OutdoorGround`, `EntryAnchor`, `Entrance_Stand`는 존재한다.
- **새 GUID:** 프로젝트 안에서 중복되지 않는다. 손잡이 컴포넌트의 knobCollider와 fader 참조는 같은 씬 문서로 해석된다.
- **API 수동 대조:** XRI 3.4.1 소스와 대조했다.
  - 대조 대상: `hoverFilters`, `selectFilters`, `logicalSelectState`, `isPerformingManualInteraction`, `nearInteractionCaster`, `SphereInteractionCaster.castRadius/physicsLayerMask/effectiveCastOrigin`, `ScreenFader.FadeTo/SetAlpha`, `CourseRoute.CanLoad/HubScenePath/ReturnToHub`
  - 컴파일러 실행은 하지 않았다.
- **보존 확인(diff 기준):**
  - 손 모델·장갑 표시 연계, `ObserverHandHide`, 덧신 판정, 교육 순서·중단·초기화·공통 메뉴 코드는 변경하지 않았다.
  - EntryAnchor 값과 A 파일은 그대로다. ProjectSettings(DX11 포함)도 변경하지 않았다.
- **Unity 열림 주의:** 작업 중 Unity가 열려 있었다(`UnityLockfile`). 두 씬이 열려 있었다면 외부 변경 알림에서 **Reload**를 선택한다. 편집기 쪽 저장으로 덮어쓰지 않아야 한다.

### 사용자 확인 ①~⑬

| 번호 | 확인 | 볼 점 |
|---|---|---|
| ① | 포털·효과·명판 없음 | 두 씬 북쪽 문 주변 |
| ② | 문 앞에서 걷기·뒤로 걷기·문 접촉 시 복귀 없음 | 머리 진입 판정 제거 |
| ③ | 문판 Grab 시 복귀 없음 | 손잡이 중심에서 20 cm 이상 떨어진 문판 등 근접 판정 영역 밖. 엄밀한 기준은 손바닥점과 손잡이 BoxCollider 표면의 최단거리가 0.1 m 초과일 것 |
| ④ | 손잡이를 만지거나 hover만 할 때 복귀 없음 | 버튼을 누르지 않음 |
| ⑤ | 손잡이 근접 Grab 시 페이드 후 A_Hub 복귀 | 왼손과 오른손 각각 |
| ⑥ | 멀리서 레이로 선택 시 복귀 없음 | 손잡이를 가리켜도 대상 표시 없음 |
| ⑦ | 원거리 Grab 입력을 누른 채 접근하면 복귀 없음, 놓고 손잡이 근처에서 다시 Grab하면 복귀 | Quest 컨트롤러는 그립(Select) 기준. 트리거 단독은 Grab 입력이 아니므로 근접해도 퇴장을 기대하지 않음 |
| ⑧ | 양손 동시·연속 Grab, 씬 왕복 후 중복 전환·작동 불가 없음 | 허브 → 교육장 → 허브 2회 |
| ⑨ | 문에 새 안내 문구·UI 없음 | — |
| ⑩ | Skybox가 A_Sandbox와 같고 창밖에 Ground 표시 | 남쪽 창 |
| ⑪ | 실내 바닥 깜빡임·장비 가림·이동·순간이동 변화 없음 | 환경광 변화로 교육판·마네킹 가독성이 떨어지는지 |
| ⑫ | 손·장갑·덧신·거울·교육 진행·진입 위치 유지 | — |
| ⑬ | 허브 복귀 후 해당 과정 포털 앞 복귀 위치 유지 | 허브 포털로 들어간 경우 |

### 미검증·제약

- **에디터에서 씬을 직접 열어 실행한 경우:** `CourseRoute.Last`가 null이라 허브 기본 시작 자리에 선다. 기존 포털은 `Enter`로 `Last`를 다시 썼지만, 이번에는 지시에 따라 덮어쓰지 않았다.
- **근접 기준의 체감:** palmReach 0.1 m와 캐스터 반경 0.1 m의 체감 거리는 미확인이다. 너무 엄격하거나 느슨하면 Inspector의 `palmReach`나 BoxCollider size로 조정한다.
- **Skybox 기반 환경광:** 실제 밝기 변화는 미확인이다(§6).

## 8. 보고서·실제 코드·씬 후속 정적 검토

2026-11-19 사용자 요청으로 실제 소스·설치된 XRI 코드·입력 에셋·두 씬·Ground 프리팹을 읽기 대조했다. 컴파일러·Unity·Play·Simulator·테스트·빌드는 실행하지 않았다. 이번 후속에서는 이 문서와 Docs/B/AGENTS.md만 갱신했다.

### 확인한 구현

- `logicalSelectState.wasPerformedThisFrame`는 설치된 XRI에서 이번 프레임 Select 입력 발생 여부를 보관하며 Dynamic PreprocessInteractor에서 갱신된다. 입력을 계속 누른 상태로 새 손잡이 영역에 진입하는 것을 선택 필터가 거절하는 구성과 일치한다.
- hover와 select가 실제 손바닥 캐스터 원점 및 BoxCollider까지의 최단거리로 제한된다. 인터랙터 타입만으로 근접을 허용하거나 selectionRegion/attach offset을 최초 선택 출처로 사용하는 코드는 없다. select는 기존 손 위치·회전 추적, 포커스·앱 일시 중단·공통 메뉴 상태, 허브 로드 가능 여부를 추가 확인한다.
- 선택 이벤트에서 leaving을 먼저 설정한 뒤 코루틴을 시작하므로 같은 객체의 양손·연속 선택이 중복 퇴장을 시작하는 경로는 발견하지 못했다. ReturnToHub는 Last를 변경하지 않는 기존 경로다. 일반적인 씬 왕복에서 상태가 영구히 남는 정적 퇴장 플래그는 새 코드에 없다.
- 두 씬의 손잡이 BoxCollider와 XRSimpleInteractable·HubExitHandle은 같은 오브젝트에 있고, Door_Range의 추가 자식으로 연결돼 있다. fader는 각 씬의 기존 ScreenFader를 가리킨다. XRSimpleInteractable의 빈 Collider 목록은 현재 자기 하위의 손잡이 Collider를 수집하는 구성이고 부모 문판은 포함하지 않는다.
- 두 씬 모두 로컬 fileID 미해결 참조와 문서 ID 중복은 발견하지 못했다. 새 메타 GUID는 코드/프리팹 연결과 일치하고 포털 이름·삭제한 HubPortalSign의 소스 참조는 남아 있지 않다.
- Skybox GUID는 현재 A_Sandbox와 같다. Ground는 동일 Plane·Dirt 재질을 쓰고, 두 씬의 같은 높이·위치·축척으로 연결됐다. Collider나 TeleportationArea는 추가되지 않았다. 기존 문·실내 바닥·이동/순간이동 구성을 변경한 근거는 발견하지 못했다.
- 변경 diff에서 기존 일반 객체의 동작 설정을 바꾼 부분은 RenderSettings의 Skybox뿐이다. 나머지는 출구 객체 제거·손잡이/Ground 추가·구성 연결과 출구 관련 주석/문구 정리이며 EntryAnchor·기존 손/장갑/거울/덧신·DX11 변경은 없다.

### 판단 및 보고서 보완

**추가 필수 수정 기능 오류는 미발견이다.** 실제 선택감·입력 차단·씬 왕복 및 렌더링 통과를 의미하지 않는다.

1. **문판 Grab 시험 거리:** 판정은 손잡이 중심까지의 거리 0.1m가 아니라 손바닥점에서 BoxCollider 표면까지 0.1m다. 상자 반폭이 0.055m이므로 수평/수직 축 방향은 중심에서 최대 0.155m까지 들어올 수 있고 대각 방향은 더 멀 수 있다. 따라서 기존 ‘손잡이에서 15cm 이상 떨어진 문판’은 항상 거절되는 시험 위치가 아니다. §7 ③을 중심에서 20cm 이상 등 실제 판정 영역 밖으로 정정했다. 코드의 근접 여유를 오류로 분류하거나 임의 축소하지 않았다.
2. **시험 입력:** B 입력 에셋의 XRI Left/Right Interaction Select는 Quest 컨트롤러에서 `{GripButton}`에 연결된다. §7 ⑦의 ‘그립·트리거 모두’는 부정확하므로 그립으로 새 Grab을 검증하도록 정정했다. 트리거 단독을 눌러 퇴장을 기대하지 않는다.
3. **환경광:** Skybox 변경 후 실내 밝기·색감은 미검증이다. 평균 텍스처 밝기만으로 1.3~1.5 보정이 반드시 필요하다고 판단하지 않는다. 실기 가독성 확인 전 Ambient를 일괄 변경하지 않는다.

사용자 우선 확인은 그립 새 입력으로 좌우 손잡이 퇴장, 원거리/누른 채 접근 거절, 허브 왕복 후 재사용, 실내 교육판·마네킹 가독성이다. 기존 §7 확인표를 사용한다. 코드·씬은 이번 검토에서 수정하지 않았다.

## 9. 사용자 실제 검증 완료 보고·천장 전등 위치 확인

2026-11-19 사용자가 이번 구현의 실제 검증 완료를 보고했다. 추가로 실내 광원이 중앙선 2개뿐인 반면 천장 전등 모델은 6개이므로 광원 배치를 수정해야 한다고 요청했다. 이 후속에서는 위치를 파일로 확인했으며 광원·씬·프리팹은 변경하지 않았다.

두 씬은 같은 `Assets/B/Prefabs/TrainingEnvironment/B_BarracksShell.prefab`을 쓰며 전등 배치 override가 없다. `CeilingFixtures` 아래 CeilingPanel 모델 인스턴스가 6개다. EnvironmentRoot와 셸의 위치·회전은 0, 축척은 1이므로 아래 모델 루트 배치점은 두 씬에서 같다.

| 행 | 왼쪽 모델 배치점 | 오른쪽 모델 배치점 |
|---|---|---|
| 입구 쪽 | (-2.139, 2.644, -2.369) | (2.139, 2.644, -2.369) |
| 가운데 | (-2.139, 2.644, -5.0905) | (2.139, 2.644, -5.0905) |
| 안쪽 | (-2.139, 2.644, -7.812) | (2.139, 2.644, -7.812) |

이는 모델 루트 피벗의 저장 좌표다. 새 광원을 놓을 최종 위치는 실제 발광면과 천장 내부 가림을 고려한 오프셋 확인이 필요하며, 위 Y값을 광원 최종 위치로 확정한 것은 아니다.

현재 두 씬의 실내 광원은 각각 Fill_1 (0,2.35,-3), Fill_2 (0,2.35,-7)인 Point Light 2개다. 각 Intensity 1.5, Range 7, Shadow 없음이다. 별도 Directional Light 1개는 실내 Fill 개수와 구분한다. 따라서 모델의 좌우 2열×3행과 실제 광원의 중앙선 2개 배치가 불일치한다.

이 위치 확인 시점에는 광원을 변경하지 않았다. 이후 사용자의 직접 배치 요청에 따라 §10의 6등 배치를 두 씬에 적용했다. 실기 밝기 확인은 사용자 대기다.

## 10. 실내 광원 6등 직접 배치

사용자가 “광원 배치는 직접 할 수 있는지?”라고 요청해 두 B 씬의 광원 배치를 직접 수정했다. 이번 변경은 B_FirstAid.unity·B_CBRN.unity 및 B 문서에 한정한다. 기존 미커밋 문 손잡이·Skybox·Ground 변경을 보존했다.

### 배치와 초기값

각 씬의 기존 중앙 Point Light 2개를 앞쪽 좌우 전등 위치로 재사용하고, 같은 구성의 Point Light 4개를 추가했다. 중앙선에 이전 Fill 광원을 남기지 않았으며 실내 Point Light는 씬마다 총 6개다. 별도의 Directional Light는 변경하지 않았다.

| 광원 이름 | 위치 |
|---|---|
| B_CeilingLight_L_Front | (-2.139, 2.56, -2.369) |
| B_CeilingLight_R_Front | (2.139, 2.56, -2.369) |
| B_CeilingLight_L_Middle | (-2.139, 2.56, -5.0905) |
| B_CeilingLight_R_Middle | (2.139, 2.56, -5.0905) |
| B_CeilingLight_L_Back | (-2.139, 2.56, -7.812) |
| B_CeilingLight_R_Back | (2.139, 2.56, -7.812) |

- 공통: Realtime Point, Intensity 0.75, Range 4.5m, Shadows None.
- 기존 색 RGB (1, 0.95686275, 0.8392157), 레이어 마스크, URP AdditionalLightData 설정을 유지했다.
- 이름과 위치·강도·범위 외에 재사용 광원의 다른 설정을 변경하지 않았다. 새 광원은 기존 광원의 Light·URP 구성 복사다.
- 부모는 기존과 같은 EnvironmentRoot다. 광원 6개를 이름으로 찾아 함께 조정할 수 있다.
- 모델 루트 Y 2.644보다 0.084m 아래에 광원을 뒀다. FBX를 Blender background에서 읽기 전용 임포트해 Diffuser/Frame/Housing 구조를 확인했으며 모델 저장·수정은 하지 않았다. Y 2.56은 전등 아래로 광원을 빼기 위한 초기 배치값이다.
- 기존 2개의 강도 1.5·범위 7을 그대로 6개로 늘리지 않고 강도 0.75·범위 4.5로 낮춰 분산했다. 이 수치는 실기에서 맞춘 최종 밝기가 아니며 이전과 동일한 조도를 보장하지 않는다.
- Skybox·Ground·Ambient·Reflection·Directional Light·전등 모델·발광 재질·URP 자산·ProjectSettings는 변경하지 않았다. 손·문·교육 코드는 이번에 수정하지 않았다.

### 파일 정적 대조

- 두 씬 모두 실내 Point 6개와 기존 Directional 1개다. 좌표·강도·범위와 그림자 꺼짐 설정이 일치한다.
- ID 중복·로컬 fileID 미해결 참조·새 자식의 부모 목록 누락은 발견하지 못했다.
- 작업 직전 백업과 문서 블록을 대조했다. 기존 블록 중 바뀐 것은 환경 부모의 자식 목록 1개와 재사용 광원 2개의 GameObject/Transform/Light 각 3개, 총 7개 블록뿐이다. 4개 새 광원의 16개 블록을 추가했다. 기존 URP 컴포넌트와 그 외 씬 데이터는 작업 직전과 동일하다.
- 두 씬의 작업 직전 사본: `C:/Users/MBC/AppData/Local/Temp/MilTrainSimVR_ceiling_lights_w7w4k29u/`.
- Unity·Play·Simulator·컴파일러·자동 테스트·빌드·라이팅 베이크는 실행하지 않았다. Blender 임포트는 모델 구조 읽기이며 Unity의 광원/화면 검증이 아니다. 스테이징·커밋·Push도 하지 않았다.

### 사용자 확인

Unity의 외부 변경 알림에서는 Reload를 선택해 수정된 씬을 불러온다. 사용자 편집이 남아 있다면 변경 내용을 먼저 확인하고 이전 사본으로 외부 변경을 덮어쓰지 않는다.

두 씬을 열어 좌우 2열×3행의 전등 위치와 광원을 확인한 뒤, DX11 빌드/Quest Link에서 교육판·장비·마네킹의 밝기와 거울 화면을 본다. 이동 중 밝기 변화나 과도한 하이라이트, 입구·방 안쪽의 어두운 구역 및 렌더 성능도 확인한다. 필요 시 각 씬의 B_CeilingLight_* 6개를 함께 선택해 Intensity/Range를 조정한다. 이번 광원 변경의 실제 검증 완료 보고는 아직 없다.

## 11. Directional Light를 A_Sandbox와 동일하게 적용

사용자의 명시 지시로 B_FirstAid·B_CBRN의 기존 Directional Light를 A_Sandbox의 현재 저장값과 맞췄다. A 원본은 읽기만 했고 변경하지 않았다.

- Intensity: 1 → **2.5**.
- Color: 기존 (1, 0.95686275, 0.8392157, 1) → **(1, 0.58, 0.32, 1)**.
- 실제 회전 Quaternion: **(0.081636354, -0.34887475, 0.030522583, 0.9331079)**로 원본과 일치시켰다.
- A의 m_LocalEulerAnglesHint는 (50,-30,0)이지만 실제 Quaternion과 불일치한다. 방향은 캐시된 Hint로 재구성하지 않고 실제 Quaternion을 복사했다. B의 Hint는 그 회전에 대응하는 (10,-41,0)으로 정리했다.
- Light 컴포넌트의 다른 설정은 이미 원본과 같았다. 객체 ID를 제외한 전체 Light 설정을 원본과 대조해 일치함을 확인했다. 위치 (0,3,0), B의 기존 부모, URP AdditionalLightData, Directional 개수 1개는 유지했다.
- §10의 천장 Point 6개는 좌표·Intensity 0.75·Range 4.5·Shadow None을 그대로 유지했다. Skybox·Ambient·Ground·프로젝트 설정·기존 기능도 변경하지 않았다.

두 씬 모두 작업 직전과 비교해 해당 Directional Transform과 Light의 2개 문서 블록만 바뀌었다. 나머지 문서 블록은 동일하다. 수정 전 두 씬 사본은 `C:/Users/MBC/AppData/Local/Temp/MilTrainSimVR_directional_match_dwbtc17j/`에 있다.

이번 작업은 씬 파일 수정과 정적 대조이며 Unity·컴파일러·Play·테스트·베이크·빌드를 실행하지 않았다. 실내·창가·거울의 최종 밝기와 색감은 사용자 확인 대기다. 에디터 외부 변경 알림에서는 저장 전 변경 내용을 확인하고 Reload로 최신 씬을 불러온다. 스테이징·커밋·Push는 하지 않았다.
