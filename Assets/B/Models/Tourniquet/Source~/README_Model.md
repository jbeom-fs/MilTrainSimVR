# Tourniquet_Model — 모델 제작·연결 기록

2026-10-29 · B · 자산 v1.1(FBX 형식 호환성 수정, 원본 형상 v1.0 유지) · Blender 5.2.1 LTS. **Blender 제작·FBX 검사 완료 / Unity 연결·실행 확인 전.**


## 2026-10-29 FBX 형식 호환성 수정(v1.1)

사용자 Unity 보고: `FBXImporter.cpp Line 420 / File is corrupted`.

이전 내보내기의 이름 정규화 후처리에서 `Shading` 플래그 19개의 자료형을 Blender 기본 `C`(char)에서 `B`(bool)로 잘못 변환한 오류를 확인했다. Blender 재반입 성공만으로 Unity 호환성을 판단할 수 없었다.

수정본은 FBX 바이너리를 파싱·재저장하는 후처리 전체를 제거했다. `Visual` 이름은 **Blender 내보내기 객체 이름 단계에서만** 정규화하고, 최종 파일은 **Blender 기본 FBX writer가 직접 저장한 바이트 그대로** 제공한다. `C` 플래그 19개 보존 확인. 메시·피벗·텍스처·원본 blend는 변경하지 않았다. 같은 FBX 경로와 기존 `.meta` GUID를 유지했다.

오류 원본과 당시 문서·manifest는 [Archive/2026-10-29_FBX_v1_rejected](Archive/2026-10-29_FBX_v1_rejected/)에 보존했다. 이 폴더의 FBX는 Unity에서 거부된 구버전이므로 교체용으로 사용하지 않는다.

수정본 Blender 재반입 및 [ufbx](https://github.com/ufbx/ufbx)의 strict 읽기 검사 통과: 메시 7, Empty 12, 재질 3, 13,128 triangles. 별도 파서에서도 Unity FBX 축 관례에 해당하는 좌수계를 명시하여 원점·막대 +Y·밴드 끝 +Z·표기면 법선을 정적으로 확인했다. 검증 기록과 SHA-256은 [FBX_Compatibility_QA.json](FBX_Compatibility_QA.json)에 있다. ufbx 읽기 성공은 Unity Importer 성공과 구분한다. **수정본 Unity 재임포트·실행 확인은 사용자 확인 전**이다.

재내보내기에서도 FBX 자료형을 임의 매핑하여 전체 파일을 재인코딩하지 않는다. 이름 정규화는 native writer 호출 이전에 완료한다.

## 산출물과 보존 범위

- 수정 가능한 원본: [Tourniquet_Model.blend](Tourniquet_Model.blend)
- 준비된 고리 자세 FBX: [Tourniquet_Model.fbx](../Tourniquet_Model.fbx)
- 이미지 3종: [Textures](../Textures/) — BaseColor / Normal / MetallicSmoothness, 각 2048×2048 PNG.
- 기계 판독 가능한 치수·피벗·계층: [Model_Manifest.json](Model_Manifest.json), [Bounds_Unity.csv](Bounds_Unity.csv).
- 기하·UV 검사: [Geometry_QA.json](Geometry_QA.json). FBX 재반입: [FBX_Roundtrip_QA.json](FBX_Roundtrip_QA.json).
- 미리보기: [정면](Previews/01_Front.png), [측면](Previews/02_Side.png), [위](Previews/03_Top.png), [아래](Previews/04_Bottom.png), [사선](Previews/05_Oblique.png), [부품 분리·피벗](Previews/06_Exploded_Pivots.png), [근접](Previews/07_Closeup.png), [막대 진입 자세](Previews/08_Seated_Geometry.png).

작업 시작 시 대상 blend/FBX/Textures/README는 없었다. 기존 `TQ_BandLoop.asset`과 그 meta, Unity 코드·Prefab·씬·프로젝트 설정을 보존했다. 읽은 자료는 `Docs/B/AGENTS.md`, `1029_지혈대_1챕터_잡기배치_구현기록.md`, `1029_출혈처치_교육과_과정통합_설계.md`, 현행 `Tourniquet_Training.prefab`, `TourniquetDevice.cs`다.

기존 Blender 미저장 기본 장면 `Scene`(Cube/Camera/Light)은 삭제하지 않았다. 원본 파일 안에 그대로 보존했으며 제작 장면은 `Tourniquet_Asset`이다. 게임 객체는 `GAME_Tourniquet`, 촬영용은 `STUDIO_NotExported`, 분리도 주석은 숨긴 `DIAGNOSTIC_NotExported`에 둔다. 원본에는 편집용 사각 면·변형 표본을 남겼다. FBX에는 게임용 메시 7개와 Empty 12개만 들어 있다. Collider·Rigidbody·Armature·카메라·조명·주석·작업용 사지는 포함하지 않았다.

## 구조 참고와 재현의 범위

공식 자료 열람일: 2026-10-29.

- [CAT Resources 공식 Gen 7 소개](https://www.combattourniquet.com/): 단일 경로 버클, 보강·리브 막대, 고정부, 회색 마감 스트랩, 받침판, 내부 밴드 구성 확인.
- [제조사 제품 구조 사진](https://www.combattourniquet.com/wp-content/uploads/2016/02/product-features3.jpg): 버클 개구, 막대의 굵기 변화, 받침판·내부 밴드, 고정부와 마감부 관계 관찰. 사진 속 주황색 장비의 구조를 참고했으며 색은 요청한 검정으로 제작.
- [공식 버클 사진](https://www.combattourniquet.com/wp-content/uploads/2016/02/single-routing-buckle.png).
- [North American Rescue Gen 7 제품 페이지](https://www.narescue.com/all-products/massive-hemorrhage/combat-application-tourniquet-c-a-t.html): 단일 경로 버클·보강 막대·받침판·시간 표기가 가능한 마감 스트랩 확인.

다른 세대의 이중 경로 버클을 혼합하지 않았다. 브랜드 로고·사진·LOT·NSN 표기는 텍스처에 복사하지 않았다. 직조·봉제·색·TIME와 기록선은 직접 생성했다.

**실측 복제품이 아니다.** 아래 숫자는 프로젝트 교체를 위한 모델 설계값·메시 측정값이다. 정품의 미확인 세부 치수·공차·기계적 고정력·권장 조작 횟수를 뜻하지 않는다. 고정부는 진입 공간을 읽기 쉬운 C형 측면 진입 구조로 단순화했다. 준비 상태의 막대 높이, 19 mm 하강, −30° 진입은 이 게임 자산의 기하 설계이며 제품 IFU나 교육 판정 기준이 아니다. 실제 제품 채택·교안 검토는 별도다.

## 치수와 메시

| 항목 | 최종값 | 기존 시제품과의 차이·이유 |
|---|---:|---|
| 고리 안쪽 반지름 | 0.060 m | 연결 기준 유지. 160개 길이 방향 구간으로 원형 고리 구성 |
| 주 밴드 폭 | 0.038 m | 유지 |
| 주 밴드 두께 | 0.0012 m | 기존 0.003 → 얇은 직조 웨빙의 앞·뒷면 및 가장자리 표현 |
| 자유 밴드 끝 | 폭 0.036 / 두께 0.0010 m | 버클 통과 여유와 식별 탭 확보 |
| 받침판 자체 | 약 0.102 × 0.046 m, 두께 0.0024 m | 얇은 곡면. 아래 Body bounds는 버클·가이드·내부 밴드까지 포함 |
| 버클 통과 구멍 | 약 0.010 × 0.0392 m | 한 개의 실제 개구. 밴드가 위로 통과한 뒤 밖으로 되접히는 경로 |
| 조임막대 | 길이 0.110 m / 최대 단면 약 0.01252 × 0.01169 m | 길이는 기존과 동일. 중앙 협폭·그립 확장·성형 리브 및 내부 밴드 슬롯 추가 |
| 내부 밴드 | 폭 0.014 / 두께 0.0011 m | 막대 중앙 슬롯을 가로질러 지나가는 별도 직물 면 흐름 |
| 고정부 내부 높이 | 약 0.0155 m | 막대 끝이 들어가는 실제 빈 공간. 사방을 막은 상자가 아님 |
| 마감 스트랩 | 폭 0.024 / 경로 길이 약 0.044 m / 두께 0.0009 m | 초기에는 열려 있음. 고정부 상단에 접촉 |
| 시간 패치 | 약 0.022 × 0.032 / 두께 0.00055 m | 마감 스트랩 표면에 놓임. 별도 공중 배치 제거 |
| 준비 자세 전체 AABB | (0.212200, 0.162462, 0.105500) m | 밴드 끝·열린 마감부·회전용 높이까지 포함 |
| LOD0 | **13,128 triangles** | 7 Visual, 재질 3개. LOD1/2는 이번 범위에 없음 |

고리는 원형을 유지한다. 직선으로 뚜껑을 덮거나 고리를 타원으로 바꾸지 않았다. 별도 자유 밴드 끝, 중앙 내부 밴드, 열려 있는 마감부는 원형 고리 판정으로 덮이지 않는다. 준비 자세는 장비를 사지로 가져갈 수 있는 상태다. 막대는 회전 전 X축 방향이며 고정부에 들어가 있지 않다.

## 축과 피벗

단위 m. **최종 FBX 루트 위치·회전은 0, 축척은 (1,1,1)**. +Z는 고리를 관통하는 사지 방향, +Y는 본체 면 바깥이다. Point_LimbAttach는 원점·루트와 같은 방향이다.

Blender 표시 좌표 B와 최종 Unity 좌표 U의 대응은 **B=(Ux, −Uz, Uy)**다. 즉 Unity +X → Blender +X, Unity +Y → Blender +Z, Unity +Z → Blender −Y. 원본의 모든 부품 로컬 좌표·메시·변형 키를 Unity 축 기준으로 정리했다. 이를 Blender에서 보이도록 **Blender 루트에만 +90° X 회전**을 둔다. FBX의 −Z Forward / Y Up 변환이 이 회전을 상쇄한다. 원본 루트의 +90°를 임의로 Apply Rotation하거나 FBX Apply Transform을 켜지 않는다. 최종 FBX에는 +90° 보정 부모가 없다.

**좌수계/우수계 보정은 별도로 적용했다.** Unity는 Y-up 좌수계, 이 FBX 저장 형식은 MayaYUp 우수계다. [Unity 공식 FBX Exporter 소스](https://github.com/Unity-Technologies/com.unity.formats.fbx/blob/main/com.unity.formats.fbx/Editor/FbxExporter.cs)는 Unity와 Maya의 수계 차이를 구분하고 마지막에 DeepConvertScene을 적용한다. 이를 근거로 이번 내보내기 복사본에는 **H=diag(−1,1,1)**을 사용했다: 메시 좌표 F=H·U, 각 자식 로컬 행렬 F=H·U·H, 면 순서는 반전. 루트에는 음수 scale을 두지 않는다. Unity의 우수계→좌수계 해석에서 원래 U 좌표가 복원되도록 구성했다. 이 대응은 정적 변환 계산에 근거하며 Unity Importer 실행 검증을 뜻하지 않는다.

따라서 일반 우수계 FBX 뷰어나 Blender에 이 FBX를 직접 재반입하면 원본 미리보기와 X 방향이 반대로 보인다. 이는 Unity 연결용 저장 보정이다. JSON의 `fbx_models`, `fbx_raw_local_bounds`, `fbx_raw_world_bounds`는 파일에 저장된 우수계 값이고, `pivots`, `local_bounds`, `world_bounds` 및 CSV는 Unity 해석 후 좌표다. 예: 고정부 원시 FBX X=−0.042435 / Y 회전 +30° → Unity 기대 X=+0.042435 / Y 회전 −30°. 밴드 끝 +Z 기대 방향은 문서의 (−0.967075,−0.254493,0)이다.

아래 위치는 모델 루트 기준 Unity 위치, 회전은 부모에 대한 XYZ Euler 도(deg)다. Euler 특이점이 있는 밴드 끝은 아래 방향 벡터를 우선한다. 부동소수점 반올림 표시이며 정품 규격 정밀도를 주장하지 않는다.

| 피벗 | 루트 기준 위치 m | 부모 기준 회전 deg |
|---|---|---|
| `Tourniquet_Model` | (0.000000, 0.000000, 0.000000) | (-0.000004, -0.000000, 0.000000) |
| `Part_Body` | (0.000000, 0.063800, -0.000000) | (0.000000, -0.000000, 0.000000) |
| `Part_Band` | (0.000000, 0.000000, 0.000000) | (0.000000, -0.000000, 0.000000) |
| `Pivot_BandEnd` | (-0.151000, 0.035000, -0.000000) | (14.743564, -89.999989, 0.000000) |
| `Pivot_WindlassAxis` | (0.000000, 0.095000, -0.000000) | (0.000000, -0.000000, 0.000000) |
| `Part_Windlass` | (0.000000, 0.095000, -0.000000) | (0.000000, -0.000000, 0.000000) |
| `Part_WindlassClip` | (0.042435, 0.076000, 0.024500) | (0.000000, -30.000008, 0.000000) |
| `Part_SecuringStrap` | (0.044000, 0.087350, 0.037000) | (0.000000, -0.000000, 0.000000) |
| `Part_TimeLabel` | (0.044000, 0.091385, 0.064000) | (0.000000, -0.000000, 0.000000) |
| `Surface_TimeLabel` | (0.044000, 0.091710, 0.064000) | (-4.656003, -0.000000, 0.000000) |
| `Point_Grab` | (-0.009000, 0.073000, -0.000000) | (0.000000, -0.000000, 0.000000) |
| `Point_LimbAttach` | (0.000000, 0.000000, 0.000000) | (0.000000, -0.000000, 0.000000) |

- `Pivot_WindlassAxis` +Y=(0,1,0). 초기 막대 길이 방향은 해당 피벗 +X. 기존 Y=0.081 → **0.095**로 변경: 고정부·열린 마감부 위에서 완전 회전할 공간 확보.
- `Pivot_BandEnd` +Z≈**(−0.967075,−0.254493,0)**. 실제 식별 탭 끝 (−0.151,0.035,0)에 위치. 기존 (0.143,−0.020,0), 루트 +X 당김에서 변경: 실제 버클 경로와 끝단 접선에 맞춤. +Z=당김이라는 의미는 유지.
- `Part_WindlassClip` 위치 (0.042435,0.076,0.0245), Y 회전 −30°. 막대 반길이 0.055 m 안의 약 0.049 m 반경에서 끝이 진입. 기존 X=0.070 m 고정부는 0.055 m 끝의 회전 궤도 밖이므로 그대로 쓰지 않음.
- `Part_SecuringStrap`는 실제 접힘 시작점에 위치. `Part_TimeLabel` 피벗은 패치 중심. 두 부품은 요청한 루트 형제 계층을 유지하므로 후속 조작에서 함께 변형·이동시켜야 한다.
- `Surface_TimeLabel` +Y≈**(0,0.996700,−0.081173)**. 패치 표면 경사에 맞는 바깥 법선. 원점은 표기 중심 표면, Part_TimeLabel로부터 +Y 약 0.000325 m.
- `Point_Grab`=(−0.009,0.073,0): 받침판을 잡는 위치. 기존 (0,0.083,0)에서 낮춰 본체 쪽으로 정렬.
- `Part_Body` Y=0.0638, `Part_Band` 원점, `Point_LimbAttach` 원점. 모든 축척은 1.

Blender는 Object 이름의 전역 중복을 허용하지 않는다. 원본 메시 이름은 `Visual__Part_Body` 등으로 식별한다. **FBX에서는 각 부모 아래 이름을 모두 `Visual`로 정규화**했다. 부품 Empty 이름은 원본·FBX 모두 요청한 이름이다.

```text
Tourniquet_Model
├─ Part_Body / Visual
├─ Part_Band
│  ├─ Visual
│  └─ Pivot_BandEnd / Visual
├─ Pivot_WindlassAxis / Part_Windlass / Visual
├─ Part_WindlassClip / Visual
├─ Part_SecuringStrap / Visual
├─ Part_TimeLabel
│  ├─ Visual
│  └─ Surface_TimeLabel
├─ Point_Grab
└─ Point_LimbAttach
```

## 후속 조작을 위한 기하 관계

**막대 회전과 고정은 높이를 구분한다.** 준비 상태의 축 높이는 Y=0.095 m. 이 높이에서 0~360°를 회전할 수 있다. 고정부에 진입할 때는 열린 방향에서 Part_Windlass의 로컬 Y를 **−0.019 m**로 내려 중심 높이를 0.076 m로 맞추고, 축 Y 회전을 0→−30°로 접근한다. 위에서 곧바로 고정부 지붕을 관통해 내리지 않는다. −30°는 형상 확인을 위한 하나의 진입 자세다. 실제 조임 회전량·장력·의료적 완료 조건과 무관하다.

원본 변형 표본은 모두 기본값 0이다.

| Visual | 편집용 표본 | 관계 |
|---|---|---|
| Body | `InternalBand_FollowRod_30deg` | 축 −30°와 함께 적용. 중앙 직물 슬롯 부분이 막대 방향을 따름 |
| Body | `InternalBand_Seated30deg` | Part_Windlass 로컬 Y=−0.019, 축 −30°의 낮아진 고정 자세와 함께 적용 |
| BandEnd | `Pull_20mm` | 버클 쪽 구간을 유지하며 끝 쪽을 20 mm 이동하는 제한 변형 예시 |
| SecuringStrap + TimeLabel | `HingeLift_60deg` | 같은 값으로 구동. 마감부와 패치가 함께 접히는/들리는 면 흐름 확인 |

내부 밴드 변형 키 두 개를 동시에 1로 적용하지 않는다. 키는 동작 설계를 위한 표본이며 완성된 조임·해제 애니메이션이 아니다. FBX는 기본 준비 자세의 삼각형 메시로 내보냈으며 이 편집용 Shape Key를 런타임 BlendShape로 제공하지 않는다. 원본에서 필요한 리그/키를 정리한 뒤 후속 전용 내보내기가 필요하다.

주 밴드는 길이 방향 160구간 × 폭 방향 6칸의 면 흐름, 앞·뒷면과 가장자리 두께를 갖는다. 자유 끝도 길이 방향 구간을 유지한다. 마감부에는 연속 면 흐름이 있다. 런타임 Cloth는 필요 조건이 아니다. 후속에는 제한된 메시 변형·본·상태별 자세 중 선택할 수 있다.

시간 표기면은 마감 스트랩과 함께 움직여야 한다. 초기 기준 `Surface_TimeLabel`는 패치 표면에 맞춰 두었다. 패치를 강체로 이동할 때는 마감부 변화량을 Part_TimeLabel에도 적용하고, 면을 굽힐 때는 표기 기준점·법선을 변형된 패치에서 갱신한다. 현재 형제 계층만으로 자동 연동되지는 않는다. 완전 덮기·최종 체결 동작은 후속 작업이다. 현재 60° 표본은 개방/접힘 가능성을 보여 준다.

## 각 Visual의 bounds와 판정 부피 자료

공통 경로 접두사: `Tourniquet_Model/`. 아래 중심·크기는 **Unity 좌수계로 해석된 메시 로컬 공간**이다. FBX 원시 우수계 bounds는 JSON의 `fbx_raw_*`에 별도로 제공한다. Visual 자체 변환은 identity. 월드 크기는 모델 루트가 원점·회전 0·축척 1인 준비 자세의 AABB다. 회전된 BandEnd/Clip은 로컬 크기와 월드 크기가 다르다. 자세가 바뀌면 월드 AABB도 갱신해야 한다.

| 경로 | 로컬 bounds 중심 m | 로컬 bounds 크기 m | 준비 자세 월드 크기 m | triangles |
|---|---|---|---|---:|
| `Part_Band/Visual` | (0.000000, 0.000000, 0.000000) | (0.122400, 0.122400, 0.038000) | (0.122400, 0.122400, 0.038000) | 4,480 |
| `Part_Body/Visual` | (-0.009750, 0.007975, 0.000000) | (0.121500, 0.047550, 0.049000) | (0.121500, 0.047550, 0.049000) | 2,680 |
| `Pivot_WindlassAxis/Part_Windlass/Visual` | (0.000000, 0.000000, 0.000000) | (0.110000, 0.012523, 0.011688) | (0.110000, 0.012523, 0.011688) | 2,684 |
| `Part_WindlassClip/Visual` | (-0.000000, -0.004186, 0.000750) | (0.018000, 0.029873, 0.023500) | (0.026863, 0.029873, 0.028876) | 756 |
| `Part_SecuringStrap/Visual` | (0.000000, 0.002000, 0.022000) | (0.024000, 0.004900, 0.044000) | (0.024000, 0.004900, 0.044000) | 576 |
| `Part_TimeLabel/Visual` | (0.000000, -0.000521, -0.000000) | (0.022000, 0.003017, 0.032000) | (0.022000, 0.003017, 0.032000) | 416 |
| `Part_Band/Pivot_BandEnd/Visual` | (0.000000, -0.009896, -0.050114) | (0.036000, 0.026489, 0.100482) | (0.102765, 0.024000, 0.036000) | 1,536 |

정확한 min/max, 피벗 위치와 세 축 방향은 JSON에 포함했다. 고리 Visual을 전체 상자로 판정하면 고리 구멍이 막힌 것으로 처리되므로 사용하지 않는다.

기존 시제품 대비 부품별 변경:

- Body: 단순 0.10×0.012×0.045 상자 → 얇은 곡면 받침판·실제 버클 구멍·가이드·변형 가능한 내부 밴드를 포함하는 Visual. 고정 폴리머의 맞물린 내부 면은 합집합으로 정리.
- Band: 반지름·폭 유지, 두께 축소. 앞·뒤·가장자리 UV 분리. 원주 이음의 중복 면 제거.
- BandEnd: 반대쪽으로 연장된 실제 통과·되접힘 경로 및 붉은 식별 끝. 피벗·축·bounds 모두 변경.
- Windlass: 별도 강체, 굵기 변화·리브·통과 슬롯 추가. 초기 높이 및 고정 시 하강 관계 추가.
- WindlassClip: 궤도 안으로 이동·회전. C형 빈 공간과 성형 가장자리, Body와 맞는 장착 발 형상. 맞닿는 장착 경계는 의도된 접촉이다.
- SecuringStrap: 고정부 상단에서 자연스럽게 열린 얇은 스트랩. 기존 반대편 임시 상자 배치 폐기.
- TimeLabel: 별도 공중 상자 → 스트랩에 접촉한 얇은 회색 패치. 표면 법선·중심 갱신.

## 후속 Unity 연결 작업 — 이번에 수행하지 않음

1. 기존 `Tourniquet_Training` 래퍼·Rigidbody·XRGrabInteractable·TourniquetDevice를 보존하고, 이 FBX의 시각 계층과 새 기준점/Parts 참조를 연결한다. 원본 시제품 Prefab을 직접 덮어쓰지 않는다.
2. 기존 코드의 `Parts`와 grab/limb 기준점을 새 Empty로 재지정한다. 기존 시제품 좌표를 새 부품에 강제로 복사하지 않는다.
3. `bandLoopRadius=0.060`, `bandWidth=0.038`, `bandThickness=0.0012`로 새 모델에 맞춘 판정 데이터를 검토한다. 기존 24개 원형 구간 상자는 이 모델의 160구간 원형을 근사할 수 있으나 실제 금지 영역에서 다시 확인해야 한다.
4. 기존 `JudgeBox`의 단위 상자 `(1,1,1)`·막대의 `(1,2,1)` 등을 복사하지 않는다. 옛 Visual의 비단위 scale을 전제로 한 값이다. 새 Visual은 scale=1이므로 CSV의 실제 미터 중심·크기를 사용해 재작성한다. Band Visual은 원형 구간 판정을 유지하고, 자유 끝은 별도 상자/구간으로 다룬다.
5. Body bounds에는 높게 열린 내부 밴드가 포함돼 있다. 이 전체 상자는 빈 공간까지 금지 영역으로 볼 수 있는 보수적 근사다. 필요하면 Body의 고정 판과 버클/중앙 밴드를 여러 JudgeBox로 나누고, 조작 중 변형에는 갱신 또는 자세별 부피를 사용한다. 얇고 굽은 자유 끝·마감부도 전체 AABB 하나보다 여러 구간이 적절할 수 있다.
6. 본체 잡기 Collider를 새 형상과 Point_Grab에 맞춰 재설정한다. 기존 중심 (−0.003,0.078,−0.008), 크기 (0.17,0.035,0.07)은 신규 막대 높이·밴드 끝·열린 마감부를 충분히 설명하지 못한다. 고리 구멍 전체를 채우는 단일 Collider는 피한다. 각 조작점 Collider는 후속 단계에서 별도 추가한다.
7. 준비/조임/고정/마감 자세에 따라 메시 변형과 판정 부피를 함께 갱신한다. 자동 Cloth나 이 FBX만으로 밴드가 줄어들 것이라 가정하지 않는다. 막대의 선택·회전·축방향 하강·고정 판정도 아직 구현되지 않았다.
8. Unity에서 단위·루트 회전/scale·각 피벗 축·노멀·재질·VR 근접 품질·프레임 성능을 사용자 직접 확인한다. Prefab 교체·금지 영역·잡기/놓기·사지 배치는 별도 실행 확인 대상이다.

Unity API·Editor 메뉴·Inspector·Importer를 조작하지 않았다. 프로젝트 자산 경로에 저장할 때 열린 Editor가 자동 생성한 새 자산 `.meta`는 존재할 수 있으며 직접 편집하지 않았다. Unity 임포트 결과를 검증 완료로 기록하지 않는다.

## UV·재질·텍스처 재구성

재질 3개: `TQ_WovenNylon`, `TQ_MattePolymer`, `TQ_WriteablePatch`. 모두 같은 2K 아틀라스 3장을 사용한다. VR 근접 관찰에서 직조·봉제선·TIME 표기를 유지하기 위해 기본 1K보다 높은 2K를 선택했다. 금속값은 전 영역 0. 직물은 낮은 smoothness, 폴리머는 약한 비금속 반사로 설정했다. 과한 오염·파손·광택은 넣지 않았다.

| 이미지 | Unity URP Lit 연결 | 색 공간·설정 |
|---|---|---|
| `TQ_BaseColor.png` | Base Map | sRGB ON, tint 흰색, Opaque |
| `TQ_Normal.png` | Normal Map | Normal map로 지정, 비색상 데이터, +Y(OpenGL) 노멀. 시작 Normal Scale: 직물 0.65 / 폴리머 0.40 / 패치 0.40 |
| `TQ_MetallicSmoothness.png` | Metallic Map, Smoothness Source=Metallic Alpha | sRGB OFF. **R=Metallic(0), A=Smoothness**, G/B=0. 알파는 투명도가 아님 |

Blender에서는 BaseColor와 Normal을 직접 사용하고, 마스크 A를 1−A로 바꿔 Roughness에 연결했다. Unity는 같은 마스크 A를 Smoothness로 사용한다. 해당 이미지들만으로 외형을 재구성할 수 있다. FBX가 Blender 노드 연결을 URP 재질로 완전히 번역해 준다고 가정하지 않는다. Unity `.mat` 파일은 만들지 않았다.

UV0 한 세트. 주 웨빙은 경로 길이를 따라 펼친 스트립이며 앞·뒤·얇은 가장자리를 별도로 배치했다. 중앙 밴드와 마감부는 길이 방향·폭 방향 텍셀 밀도를 맞췄다. 외곽 여백은 최소 약 40px. 폴리머는 지정 영역 안에 투영·패킹했다. 표면에 고유 그림이 없는 폴리머는 Visual 사이에 UV 영역을 의도적으로 공유한다. 작은 직물 가장자리도 재사용하므로 전체 아틀라스가 비중첩 고유 베이크 UV인 것은 아니다. 고유 라이트맵 UV2는 제공하지 않는다. 0면적 UV face는 최종 0개다.

## FBX 내보내기와 확인 결과

FBX 7.4 binary, Scale=1, 단위 m, Apply Unit=ON, Apply Scalings=`FBX_SCALE_UNITS` (FBX UnitScaleFactor=100: 1 FBX 좌표 단위=1 m). Forward=−Z, Up=Y, **Apply Transform / bake_space_transform=OFF**. 원본의 정리된 로컬 Unity 축과 Blender 루트 +90° X를 사용하되, 내보내기 복사본에는 위의 H 좌우 반전·행렬 켤레변환·면 순서 반전을 먼저 적용한다.

게임 객체만 선택. 유형 Mesh/Empty, 애니메이션 OFF, leaf bones OFF, 상대 이미지 경로, 이미지 embed OFF, 노멀/탄젠트 포함. 원본의 `EXPORT_Triangulate_Fixed` 평가 결과를 별도 정적 메시로 만들어 내보냈으므로 최종 FBX의 모든 면은 삼각형이다. 원본 쿼드·변형 키는 유지된다. 재내보내기 시 게임 계층 복사본에 H 변환과 면 순서 반전을 적용한 뒤, Blender의 전역 고유 이름 `Visual__...`을 FBX 형제 이름 `Visual`로 정규화해야 최종 계약과 동일해진다. Source~ 전체를 Unity로 자동 변환하지 않는다.

Blender에서 확인한 내용:

- 7개 메시 모두 열린 경계·non-manifold edge·고립점·퇴화 면 0. 두께가 있는 폐합 면, 바깥 노멀, 의도하지 않은 중복 이음 정리.
- 원형 밴드 길이 방향 면 흐름 유지. 고정 폴리머 교차 내부 면 합집합 정리. 고정부 장착 발과 받침판 맞춤.
- 준비 상태 막대-본체 관통 0. 막대 0~360°를 5° 간격 73개 자세에서 검사: 고정 폴리머·고정부·마감 스트랩·시간 패치와 관통 0.
- 고정부 높이에서 0~30°를 2° 간격 16개 자세로 검사: 막대-고정부 관통 0. 최종 진입 자세는 내부 밴드 변형 표본과 함께 막대-본체 관통 0.
- 20 mm 당김 표본에서 자유 끝-본체/주 고리 관통 0. 마감부/패치의 60° 들기 표본을 11단계로 검사: 고정부 관통 0.
- UV 범위와 여백, 0면적 UV face 0 확인. 의도한 영역 공유는 위에 별도 기록.
- 최종 FBX binary에서 Empty/계층/scale/축·삼각분할 확인. Blender 임시 장면에 재반입한 뒤 H 좌수계 해석을 정적으로 적용한 결과 **메시 7 / Empty 12 / 13,128 tris**, 전체 치수 최대 오차 4.99e-08 m. 루트 scale=1, Point_LimbAttach 원점, 막대 +Y/밴드 끝 +Z/표기 +Y 축 확인. 임시 검사 장면은 제거했고 기존 장면은 보존.
- 정면·측면·상하·사선·근접·분리도·진입 자세 PNG 확인. 분리도와 진입 자세는 설명용이며 FBX 기본 자세를 변경하지 않는다.

위 검사는 Blender 기하와 파일 왕복 확인이다. 접촉 경계는 BVH의 교차 표본만으로 기계적 체결력을 보증하지 않는다. 완성된 조임/풀기/마감 애니메이션, 연속 충돌/변형 해석, Unity 런타임·Simulator·HMD 성능 및 교육 내용 승인은 이번에 검증하지 않았다. 게임 자산 연결·Collider·금지 영역 재설정과 사용자 실행 확인은 후속 작업으로 남긴다.

