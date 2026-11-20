# 보호장비 개발용 기본형 — Blender 제작 기록

제작일: 2026-11-03 (Asia/Seoul). Blender 5.2.1 LTS.

특정 제품을 재현하지 않은 교육용 개발 기본형이다. 제품명·인증·실제 보호성능을 표시하지 않는다. 이후 제품 자료에 맞춰 6개 구성품을 각각 교체할 수 있다. 방독면과 제독 도구는 제작 범위에 포함하지 않았다.

## 전달 파일

- `ProtectiveClothing.blend`: 제작 원본. `Reference`, `Worn`, `Pickup`, `Review_Studio` 컬렉션.
- `../ProtectiveClothing_Pickup.fbx`: 작업대용 독립 소품 6개, 루트 Empty 1개, Grip/Attach Empty 12개.
- `../ProtectiveClothing_Worn.fbx`: 착용 메시 6개, 루트 Empty 1개, Attach Empty 6개.
- `../Textures/`: 공유 BaseColor·Roughness·Normal PNG 3개.
- `Previews/`: 전체 5방향, 양쪽 손목·발목 확대, Pickup 전체, 구성품 분리 렌더 11장과 검토용 연락 시트.
- `FBX_Roundtrip_Review.blend`: 제작 원본과 별개 파일. `Reimport_Worn`, `Reimport_Pickup` 장면에서 FBX 재수입 결과만 확인 가능.
- `MeshStatistics.json`, `AnchorManifest.json`, `ValidationReport.json`: 메시·기준점·재수입·표면 검사 수치.
- `OriginalAssetHashes.json`: 참조한 기존 파일들의 SHA256. 새 장비 파일 해시는 `DeliveryManifest.json`.

## 마네킹과 기준 자세

기준은 `Assets/B/Models/Mannequin/Mannequin.fbx` 하나이다. 원본 FBX를 Blender FBX Import로 직접 불러왔으며 `use_anim=False`로 애니메이션 재생 영향을 제외했다. 이때의 원본 바인드 기준 T 자세에 맞춘 **고정 메시**이다. 신규 인체나 신규 인체 리그는 만들지 않았다.

원본 Armature와 `Ch36` 메시, 65개 본 및 기존 `mixamorig1:` 이름·계층·기준 변환을 유지한다. 원본 가져오기에서 생기는 Armature의 0.01 스케일을 적용하거나 바꾸지 않았다. 장비는 이 참조의 실제 월드 좌표를 미터로 측정해서 만들었으므로 원본과 장비 사이에 추가 100배/0.01배 보정이 필요하도록 제작하지 않았다.

원본 표시 메시의 Blender 월드 범위(m): X=-0.88980~0.88980, Y=-0.16827~0.11395, Z=0.00411~1.76916. 실제 범위 높이는 약 1.76505m이다. 표준 신장으로 재조정하지 않았다. `ReferenceInspection.json`에 정확한 행렬·본 좌표·구간 측정값을 저장했다.

원본에는 `Armature|mixamo.com|Layer0` 애니메이션이 있으며, 기본 Import에서 활성화되는 1프레임 자세와 바인드 자세의 발 위치가 다르다. 예를 들어 왼발 본 X는 바인드 약 0.15180m, 클립 1프레임 약 0.08079m이다. `PoseInspection.json`은 이 차이를 확인한 별도 진단 기록이며 착용 정렬의 기준으로 사용하지 않았다.

`B_K1_Tutorial.unity`, `K1UserAvatar.cs` 및 해당 아바타 구성 코드를 읽기만 했다. 기존 아바타는 본을 구동하고 모델 방향도 맞추므로, 움직이는 MirrorAvatar에 이 고정 Worn 메시를 입힌 결과는 보장하지 않는다. 고정 교육 마네킹의 위 기준 자세가 사용 대상이다. 원본 본과 메시·텍스처 파일은 수정하지 않았다. 검토 렌더에만 중성 회색 오브젝트 머티리얼을 임시 적용했으며 저장한 제작 파일에서는 원본 DATA 연결로 복원했다.

## 구성과 표시 방식

각 FBX의 독립 메시 이름은 다음과 같다.

| 구성품 | Pickup | Worn |
|---|---|---|
| 상의 | Jacket_Pickup | Jacket_Worn |
| 하의 | Trousers_Pickup | Trousers_Worn |
| 본인 왼쪽 장갑 | Glove_L_Pickup | Glove_L_Worn |
| 본인 오른쪽 장갑 | Glove_R_Pickup | Glove_R_Worn |
| 본인 왼쪽 덮개 | Overboot_L_Pickup | Overboot_L_Worn |
| 본인 오른쪽 덮개 | Overboot_R_Pickup | Overboot_R_Worn |

```text
ProtectiveClothing_Pickup (Empty, origin)
  <Component>_Pickup (Mesh, local origin)
    Grip_<Component>_Pickup (Empty)
    Attach_<Component>_Pickup (Empty)

ProtectiveClothing_Worn (Empty, origin)
  <Component>_Worn (Mesh, common mannequin origin)
    Attach_<Component>_Worn (Empty)
```

`<Component>`는 Jacket, Trousers, Glove_L, Glove_R, Overboot_L, Overboot_R이다. 장갑과 덮개는 실제 좌우 형상이 다르며 L/R 패치를 붙였다. 장갑 Pickup에는 위에서 보이는 L/R 패치도 추가했다. 접힌 상의는 소매·주머니·앞여밈, 접힌 하의는 허리띠·두 다리 접힘으로 구분한다.

상의의 주머니·앞여밈, 하의 허리띠, 장갑 커프, 덮개 밑창·커프·L/R 패치는 각 해당 메시 안에 합쳐져 있다. 별도 조작 오브젝트가 아니다. 천/SoftBody/IK/Constraint/새 본/ShapeKey/착의 애니메이션은 없다. 제작용 Remesh/Decimate/Solidify/Bevel/Triangulate는 모두 적용되어 전달 파일에 의존성이 남지 않는다.

SNAP 연동 시 해당 Pickup을 숨기고 Worn 구성품을 표시하는 용도이다. 모든 Worn 메시의 로컬 원점·회전·스케일은 동일한 기준 (0,0,0)/(0,0,0)/(1,1,1)이다. 마네킹 원본 FBX의 동일한 기준 공간 아래에 공통 Worn 루트를 맞춰야 한다. Worn 메시 자체를 각 Attach 위치로 다시 이동하면 이중 이동이 된다. 독립 장비를 움직이는 본 아래에 자동 장착하거나 새로운 아바타 구조를 요구하지 않는다.

제작 파일에서는 Pickup이 원점에서 서로 겹치므로 `Pickup` 컬렉션을 숨겨 놓았다. 구성품별 선택/표시 후 독립 배치한다. `Pickup_All.png`의 펼친 배치는 검토 렌더에서만 사용했으며, FBX와 저장 원본의 Pickup은 각자 로컬 바닥 중심 피벗이다.

## 좌우, 피벗과 기준점

**Blender: +Z 위, -Y 전방, +X 마네킹 본인의 왼쪽, -X 본인의 오른쪽.** 정면에서 보는 화면 왼쪽이 본인의 오른쪽이다. 본 이름도 이 기준으로 대조했다.

Worn 메시 원점은 마네킹의 공통 원점이다. Pickup 원점은 소품의 수평 경계 중심/최저점으로, 작업대 표면에 놓기 쉽다. Grip은 소품 중심 부근의 잡기 편한 위치이다. Pickup Attach는 이동 소품의 일반적인 중심 기준점, Worn Attach는 신체 목표를 정할 때 참고할 의미상의 위치이다. 두 Attach를 정렬해 접힌 옷을 펼치는 변형은 구현하지 않았다.

모든 기준점은 Blender 공통축을 유지하는 회전 0/스케일 1의 Empty이다. +Z=위/손잡이 기준의 위, -Y=소품 전방, +X=횡방향. 실제 VR 컨트롤러의 손 방향에 맞춘 포즈나 코드가 들어 있지 않다. Unity에서 각도와 무관한 범위 판정을 만들 수 있도록 위치만 제공한다. 축 변환 이후의 Unity 수치는 아래 Blender 좌표를 임의로 복사하지 말고 FBX에 포함된 Empty Transform을 사용한다.

다음 값은 모두 Blender 좌표(m), 해당 Pickup 또는 공통 Worn 원점 기준이다. 상세 행렬은 `AnchorManifest.json` 참조.

| 구성품 | Pickup Grip XYZ | Pickup Attach XYZ | Worn Attach XYZ |
|---|---|---|---|
| Jacket | (0.00000, 0.00000, 0.06293) | (0.00000, 0.00000, 0.05075) | (0.00000, -0.02500, 1.29000) |
| Trousers | (0.00000, 0.00000, 0.05487) | (0.00000, 0.00000, 0.04425) | (0.00000, -0.02500, 0.96500) |
| Glove_L | (0.00000, 0.00000, 0.08469) | (0.00000, 0.00000, 0.06830) | (0.67500, 0.03500, 1.42200) |
| Glove_R | (0.00000, 0.00000, 0.08476) | (0.00000, 0.00000, 0.06835) | (-0.67500, 0.03500, 1.42200) |
| Overboot_L | (0.00000, 0.00000, 0.17671) | (0.00000, 0.00000, 0.14251) | (0.15000, 0.03100, 0.10500) |
| Overboot_R | (0.00000, 0.00000, 0.17671) | (0.00000, 0.00000, 0.14251) | (-0.15000, 0.03100, 0.10500) |

## 메시와 텍스처 통계

삼각형은 모든 최종 메시를 명시적으로 삼각분할한 후 계산했다. 각각 공유 `PC_Shared` 머티리얼 슬롯 1개이며, 전체 세트에서 사용하는 고유 장비 머티리얼도 1개이다. 메시 개수는 상태별 6개이다. 참조 인체·스튜디오 소품은 통계에서 제외했다.

| 구성품 | Pickup 삼각형 | Worn 삼각형 | 머티리얼 수 Pickup/Worn |
|---|---:|---:|---:|
| Jacket | 1,440 | 7,816 | 1 / 1 |
| Trousers | 940 | 5,836 | 1 / 1 |
| Glove_L | 3,016 | 3,014 | 1 / 1 |
| Glove_R | 3,048 | 3,046 | 1 / 1 |
| Overboot_L | 1,790 | 1,790 | 1 / 1 |
| Overboot_R | 1,790 | 1,790 | 1 / 1 |
| 합계 | 12,024 | 23,292 | 공유 1종 |

| 이미지 파일 | 크기 | 저장/색 해석 | 용도 |
|---|---|---|---|
| PC_BaseColor_2K.png | 2048×2048 RGB | PNG / sRGB | 올리브 직물·어두운 고무·커프·L/R 공용 아틀라스 |
| PC_Roughness_1K.png | 1024×1024 L | PNG / Linear | 표면 거칠기 |
| PC_Normal_1K.png | 1024×1024 RGB | PNG / Linear, tangent normal | 얕은 직물 결 |

Blender에서 이미지 기반 Principled BSDF로 구성했다. 장비의 Blender 파일에는 같은 이미지도 패킹했으며 Unity 전달용 독립 PNG를 별도 제공한다. FBX에는 UV와 머티리얼/이미지 경로를 포함하고 텍스처를 중복 임베드하지 않았다. 공용 이미지 3개는 모든 구성품이 함께 사용한다.

Unity 렌더 파이프라인에 맞는 최종 머티리얼 생성·normal import·거칠기/매끄러움 연결은 미확인이다. 사용하는 Unity 셰이더가 Smoothness를 요구하면 이 Roughness의 반전값이 필요하다. 본 작업에서는 Unity 머티리얼·Importer·공용 설정을 수정하지 않았다. 실제 draw call·프레임 시간·메모리·기기 성능을 측정하지 않았으며 **Quest 2 최적화 완료로 판정하지 않는다**.

## FBX 내보내기와 재수입 확인

Metric, Unit Scale=1.0, Blender 1 unit=1m. 장비 메시와 Empty는 양수 단위 스케일. FBX Binary export: `global_scale=1`, `apply_unit_scale=True`, `apply_scale_options=FBX_SCALE_UNITS`, Forward=-Z, Up=Y, `use_space_transform=True`, `bake_space_transform=False`. 애니메이션 제외, Mesh/Empty만 명시 선택, leaf bone 제외, 상대 이미지 경로, 텍스처 임베드 제외.

FBX에 참조 마네킹·원본 리그·카메라·조명·바닥이 포함되지 않았음을 재수입 오브젝트 타입/명단으로 확인했다. 단위/축 헤더 수치는 `ValidationReport.json`의 `fbx_global_settings`에 기록했다. `UnitScaleFactor=100`은 FBX 단위당 100cm, 즉 1m라는 단위 메타데이터이며 Transform 스케일을 100으로 설정하라는 뜻이 아니다. Blender와 FBX 사이의 변환을 적용한 결과를 확인했으며, Unity에서 실행하여 재검증한 결과는 아니다. 별도의 100 또는 0.01 Transform 스케일 보정을 전제로 하지 않는다.

완전히 새 Blender 장면 2개에 각각 FBX를 다시 불러왔다. 원본과 이름·부모·구성품 분리·삼각형 수·UV·머티리얼 슬롯·위치·크기·방향·기준점 행렬을 비교했다. Worn 13개, Pickup 19개 오브젝트 검사 통과. 양방향 최근접 정점 위치 최대 오차 2.4959445e-07m, 4×4 변환 행렬 원소 최대 차이 2.30967999e-07; 허용값은 각각 0.00001이다. 기준점 이름과 위치도 비교에 포함했다.

## 외형과 메시 검토

렌더 11장을 저장하고 정면·후면·본인 좌우·사선, 양쪽 장갑/손목, 양쪽 덮개/발목, Pickup 전체, 구성품 분리를 직접 확인했다. 검토 렌더에 K1 방독면은 표시하지 않았다. 어깨 상부의 의도하지 않은 구멍, 국소 인체 노출, 초기 발목 커프 교차를 찾아 수정했다.

상의·하의에는 몸통과 팔다리보다 넓은 의복 여유를 주었다. 손목은 소매 위로 장갑의 넓은 커프가 덮고, 발목은 바지 끝을 덮개가 감싸며, 허리는 상의 밑으로 하의 상단이 들어간다. 연결부는 동일 평면을 공유하도록 만들지 않았고, 의도된 내부 겹침에 공간 차이를 두었다. 패치는 표면에서 약간 띄워 공면을 피했다. 고무 손가락은 단순화하여 손가락 사이가 실제 장갑처럼 세밀하게 분리되지 않은 부분이 있다.

최종 메시 검사: 음수 스케일 0, 퇴화 면 0, 1µm 양자화 기준 공간 중복 면 0, 경계가 아닌 비다양체 모서리 0. 닫힌 메시 섬의 부호 체적을 검사해 뒤집힌 닫힌 섬이 없는지 확인했다. L/R 표시는 단면 패치이므로 경계 모서리가 존재한다(장갑/덮개 Worn 및 덮개 Pickup 각 4개, 장갑 Pickup 각 8개). 이는 의복 셸의 구멍이 아니다. 의복 개구부는 안쪽 두께와 가장자리로 마감했다. 내부 전신 복사 메시를 추가하지 않았으며, 고정 오브젝트의 내부는 얇은 안쪽 셸과 연결부 겹침만 사용했다.

아래는 원본 인체 표면의 최근접 법선에 대해 모든 Worn 정점의 부호 거리를 검사한 보조 결과이다. `최소 거리`는 가장 가까운 표면에 대한 부호 거리이며 옷 전체의 최소 두께/여유를 보증하는 값은 아니다. 곡면의 교차나 모든 시선 방향을 완전히 증명하는 검사는 아니므로 렌더 확인과 함께 사용했다.

| 메시 | 1mm 초과 인체 안쪽 표본 수 | 최소 부호 거리(mm) |
|---|---:|---:|
| Jacket_Worn | 0 | 3.104 |
| Trousers_Worn | 0 | 13.590 |
| Glove_L_Worn | 0 | 5.162 |
| Overboot_L_Worn | 0 | 3.657 |
| Glove_R_Worn | 0 | 4.779 |
| Overboot_R_Worn | 0 | 3.620 |

## 남은 제한과 Unity 미확인 항목

- **기존 K1과 함께 표시했을 때의 목·얼굴 충돌은 미확인.** 기존 K1 자산과 착용 코드의 존재는 확인했지만, 이 마네킹에 K1 착용 위치·끈 포즈까지 재현한 조합 검토는 하지 않았다. 목둘레 형상은 기본형이며 최종 K1 조합에 따라 교체 가능하다.
- 고정 바인드 기준 자세에만 맞췄다. 애니메이션 클립 1프레임, 움직이는 MirrorAvatar, 다른 자세/체형에서의 정렬·관통은 미확인. 새 아바타나 원본 리그 수정은 요구하지 않는다.
- Blender 정지 렌더에서는 확인 가능한 연결부에 인체 노출이나 공면 교차가 보이지 않도록 보정했다. VR 이동 시 시간에 따른 깜빡임과 Unity 셰이더의 뒷면/그림자/노멀 동작은 검증하지 않았다.
- SNAP 범위·판정·착용 순서·콜라이더·XR Grab 연결·표시 전환은 이번 모델의 기능에 포함하지 않는다. Grip/Attach Empty와 독립 메시만 제공한다.
- Unity 실행·Play·Simulator·테스트·빌드를 수행하지 않았다. Unity 코드·씬·Prefab·기존 텍스처/본·공용 설정을 변경하지 않았다. 스테이징과 커밋을 하지 않았다.

## 검토 이미지

| 파일 | 내용 |
|---|---|
| [Worn_Front](Previews/Worn_Front.png) | 정면 |
| [Worn_Back](Previews/Worn_Back.png) | 후면 |
| [Worn_Left](Previews/Worn_Left.png) | 본인 왼쪽 측면 (+X에서 촬영) |
| [Worn_Right](Previews/Worn_Right.png) | 본인 오른쪽 측면 (-X에서 촬영) |
| [Worn_ThreeQuarter](Previews/Worn_ThreeQuarter.png) | 사선 전체 |
| [Wrist_L](Previews/Wrist_L.png), [Wrist_R](Previews/Wrist_R.png) | 장갑·손목 |
| [Ankle_L](Previews/Ankle_L.png), [Ankle_R](Previews/Ankle_R.png) | 덮개·발목 |
| [Pickup_All](Previews/Pickup_All.png) | 독립 소품 전체 |
| [Components_Separated](Previews/Components_Separated.png) | 6개 Worn 분리 |

## 재현용 스크립트

`make_textures.py`(시스템 Python + Pillow/NumPy), `build_protective_clothing.py`(Blender background), `validate_exports.py`(Blender background), `finalize_documentation.py`(시스템 Python) 순서이다. 재현 시 이 폴더의 **이미 생성된 결과를 다시 저장하므로**, 사람이 수정한 후에는 별도 복사본에서 실행해야 한다. 재현 스크립트는 원본 마네킹·K1·Unity 파일을 읽기만 하고 지정 장비 폴더에만 쓴다.
