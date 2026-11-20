# K1 Gas Mask — 모델 인수인계
제작일: 2026-10-26 (Asia/Seoul). 상태: **치수·후면 구조 미확정 외형/상호작용 프로토타입**.

연결된 Blender MCP에서 직접 메시 제작. 외부 완성 모델 및 AI 3D 생성 서비스 미사용.
기준 사진은 200×200 한 장. 실측 자료와 동일 형식 후면·안쪽·버클 상세는 사용자 확인 결과 없음.
따라서 실제 K1 재현 확정본, 실제 머리끈 수 확정본, 교육 절차 구현본으로 취급하지 않는다.

## 파일
- ../K1_GasMask.fbx: 렌더 메시 9개, Empty 계층, 앵커 16개, 끈 BlendShape 4개.
- ../K1_GasMask_Collision.fbx: 충돌 프록시 5개 및 원점 (0,0,0) 루트만.
- K1_GasMask.blend: 수정 원본. 작업 장면 K1_Prototype_Workshop. 기존 Scene의 Cube/Light/Camera 보존.
- ../Textures/: 본체 PNG 4장(2048×2048), 끈 PNG 3장(1024×1024).
- Previews/: 개별 1600×1200 PNG, 조임 비교·검토용 합성 이미지.
- References.md: 확인 범위와 추정 부분.
- K1_Validation.json: 실제 Blender 검사 수치 및 FBX 비교 결과.

## 수치와 단위
Metric, Unit Scale 1.0. Blender 1단위=1m.
**확인된 실물 치수 없음.** 안면부 최대 폭 0.18m는 작업 가정이며 제조사 제원이 아니다.

| 대상 | 폭 X(m) | 높이 Z(m) | 깊이 Y(m) |
|---|---:|---:|---:|
| 안면부 Part_Facepiece | 0.179999 | 0.234000 | 0.128831 |
| 전체 Basis, 정화통·끈 포함 | 0.270062 | 0.234000 | 0.307000 |
| 전체 모든 Key=1 | 0.286556 | 0.234000 | 0.307000 |

렌더 LOD0: **14,708 triangles**, Mesh 9개, 공유 재질 3종, Mesh당 슬롯 1개.
Shape Key를 폴리곤 수에 중복 합산하지 않음. 카메라/조명/충돌 메시 제외.
Mesh 수는 draw call 수가 아니다. 추가 LOD, Armature, 타임라인 애니메이션 없음.

## 좌표·루트
Blender 작업 축: +X=착용자 오른쪽, -Y=전방, +Z=위.
Unity에서 확인할 계약: +X=오른쪽, +Y=위, +Z=전방. 예상 위치 대응은 (x,y,z)Blender → (x,z,-y)Unity이나, Unity 임포터의 실제 축 변환·루트 회전은 미확인이다.
정화통 중심은 -X 측. 좌우는 착용자 기준. 사진의 화면 좌우만으로 방향을 확정하지 않는다.
K1_GasMask_Root는 밀착 테두리 중심 (0,0,0), Scale (1,1,1).
Anchor_FaceFit 위치와 루트 일치. ViewOrigin은 인체 자료 없는 임시 위치.
렌더 Mesh Rotation=(0,0,0), Scale=(1,1,1). Mirror/미적용 변형 Modifier 없음.

## 부품·피벗
다음 좌표는 Visuals 기준 Blender XYZ(m). Visuals와 Anchors는 루트 아래 항등 Transform.

| 부품 | triangles | 부모 | 피벗 XYZ(m) | 재질 슬롯 |
|---|---:|---|---|---:|
| Part_Facepiece | 7156 | Visuals | (0.000000, 0.000000, 0.000000) | 1 |
| Part_Lens_L | 192 | Visuals | (-0.040000, -0.084000, 0.038000) | 1 |
| Part_Lens_R | 192 | Visuals | (0.040000, -0.084000, 0.038000) | 1 |
| Part_FilterConnector | 476 | Visuals | (-0.075000, -0.044000, -0.038000) | 1 |
| Part_FilterCanister | 1884 | Visuals | (-0.075000, -0.044000, -0.038000) | 1 |
| Part_FrontAssembly | 1232 | Visuals | (0.000000, -0.106000, -0.047000) | 1 |
| Part_TubeAssembly | 328 | Visuals | (0.052000, -0.067000, -0.047000) | 1 |
| Part_Harness | 2096 | Visuals | (0.000000, 0.000000, 0.000000) | 1 |
| Part_Buckles | 1152 | Visuals | (0.000000, 0.000000, 0.000000) | 1 |

렌즈 2개는 공유 MAT_K1_Lens. 본체와 정화통·전면·관·버클은 MAT_K1_Body, 끈은 MAT_K1_Harness.
정화통 피벗은 본체와 맞닿는 부착 중심. 분리 가능 메시이며 실제 나사 기능 없음.
안면부는 두께 있는 열린 컵. 뒤를 평면으로 막지 않았다. 렌즈 구멍과 고무 연결부 포함.
앞 부품과 관은 기능을 확정하지 않고 중립 이름 유지. 관의 최종 보관·연결 경로는 추정.
개별 두께 있는 셸은 일부 조립부에서 의도적으로 관입해 연결된다. 제조용 단일 솔리드가 아니다.

## 앵커 표
위치: 부모 로컬 Blender XYZ(m). 회전: Blender XYZ Euler(deg).
+Z/+Y 방향은 아래 별도 표의 **Blender 장면 방향**. FBX 이후 Unity 로컬 축을 반드시 확인한다.

| 앵커 | 부모 | 위치 XYZ(m) | 회전 XYZ(deg) | 의미 |
|---|---|---|---|---|
| Anchor_FaceFit | Anchors | (0.000000, 0.000000, 0.000000) | (90.000, 0.000, 0.000) | face seal center; root origin; temporary anatomical fit |
| Anchor_ViewOrigin | Anchors | (0.000000, 0.018000, 0.038000) | (90.000, 0.000, 0.000) | provisional binocular eye point; no anthropometric validation |
| Anchor_Grip_L | Anchors | (-0.054665, -0.038865, -0.066043) | (121.770, -0.000, -60.345) | outer lower rubber surface contact candidate, 1mm outward; hand offset required |
| Anchor_Grip_R | Anchors | (0.054665, -0.038865, -0.066043) | (121.770, -0.000, 60.345) | outer lower rubber surface contact candidate, 1mm outward; hand offset required |
| Anchor_FilterAttach | Anchors | (-0.075000, -0.044000, -0.038000) | (98.644, -0.000, -72.300) | body mating center; Z toward filter |
| Anchor_FilterMate | Anchors | (-0.075000, -0.044000, -0.038000) | (81.356, 0.000, 107.700) | filter matching plane; opposite Z; reparent in Unity to detachable filter if needed |
| Anchor_FilterInspect | Anchors | (-0.151289, -0.068348, -0.050174) | (98.644, -0.000, -72.300) | inspection candidate; not an approved procedure |
| Anchor_FrontInspect | Anchors | (0.000000, -0.134000, -0.047000) | (90.000, 0.000, 0.000) | front inspection candidate; not an approved procedure |
| Anchor_StrapPull_P_LU | Anchors | (-0.104000, -0.038000, 0.042000) | (111.912, -0.000, -39.560) | provisional free strap end; interpolate position using BlendShape weight |
| Anchor_StrapGuide_P_LU | Anchors | (-0.080000, 0.002000, 0.067000) | (111.912, -0.000, -39.560) | stationary provisional buckle guide |
| Anchor_StrapPull_P_LD | Anchors | (-0.104000, -0.038000, -0.081000) | (111.912, -0.000, -39.560) | provisional free strap end; interpolate position using BlendShape weight |
| Anchor_StrapGuide_P_LD | Anchors | (-0.080000, 0.002000, -0.056000) | (111.912, -0.000, -39.560) | stationary provisional buckle guide |
| Anchor_StrapPull_P_RU | Anchors | (0.104000, -0.038000, 0.042000) | (111.912, -0.000, 39.560) | provisional free strap end; interpolate position using BlendShape weight |
| Anchor_StrapGuide_P_RU | Anchors | (0.080000, 0.002000, 0.067000) | (111.912, -0.000, 39.560) | stationary provisional buckle guide |
| Anchor_StrapPull_P_RD | Anchors | (0.104000, -0.038000, -0.081000) | (111.912, -0.000, 39.560) | provisional free strap end; interpolate position using BlendShape weight |
| Anchor_StrapGuide_P_RD | Anchors | (0.080000, 0.002000, -0.056000) | (111.912, -0.000, 39.560) | stationary provisional buckle guide |

| 앵커 | 로컬 +Z의 장면 방향 | 로컬 +Y의 장면 방향 |
|---|---|---|
| Anchor_FaceFit | (0.0000, -1.0000, -0.0000) | (0.0000, -0.0000, 1.0000) |
| Anchor_ViewOrigin | (0.0000, -1.0000, -0.0000) | (0.0000, -0.0000, 1.0000) |
| Anchor_Grip_L | (-0.7388, -0.4206, -0.5265) | (-0.4576, -0.2605, 0.8502) |
| Anchor_Grip_R | (0.7388, -0.4206, -0.5265) | (0.4576, -0.2605, 0.8502) |
| Anchor_FilterAttach | (-0.9418, -0.3006, -0.1503) | (-0.1432, -0.0457, 0.9886) |
| Anchor_FilterMate | (0.9418, 0.3006, 0.1503) | (-0.1432, -0.0457, 0.9886) |
| Anchor_FilterInspect | (-0.9418, -0.3006, -0.1503) | (-0.1432, -0.0457, 0.9886) |
| Anchor_FrontInspect | (0.0000, -1.0000, -0.0000) | (0.0000, -0.0000, 1.0000) |
| Anchor_StrapPull_P_LU | (-0.5909, -0.7153, -0.3732) | (-0.2377, -0.2877, 0.9278) |
| Anchor_StrapGuide_P_LU | (-0.5909, -0.7153, -0.3732) | (-0.2377, -0.2877, 0.9278) |
| Anchor_StrapPull_P_LD | (-0.5909, -0.7153, -0.3732) | (-0.2377, -0.2877, 0.9278) |
| Anchor_StrapGuide_P_LD | (-0.5909, -0.7153, -0.3732) | (-0.2377, -0.2877, 0.9278) |
| Anchor_StrapPull_P_RU | (0.5909, -0.7153, -0.3732) | (0.2377, -0.2877, 0.9278) |
| Anchor_StrapGuide_P_RU | (0.5909, -0.7153, -0.3732) | (0.2377, -0.2877, 0.9278) |
| Anchor_StrapPull_P_RD | (0.5909, -0.7153, -0.3732) | (0.2377, -0.2877, 0.9278) |
| Anchor_StrapGuide_P_RD | (0.5909, -0.7153, -0.3732) | (0.2377, -0.2877, 0.9278) |

FilterAttach와 Mate 거리 0m, +Z 내적 약 -1. Mate는 Anchors 아래 정적 Empty다.
정화통 분리 제어 시 Unity에서 대응 피벗/앵커의 부모와 추종을 연결해야 한다.
Grip은 하부 외측 고무 표면에서 약 1mm 바깥 후보. 실제 손 포즈 오프셋은 후속 조정.
Inspect 2개는 교안 미승인 후보 지점이다.

## 끈 Shape Key
**P_ 접두어는 provisional(임시). 실제 K1의 끈 개수 또는 배치를 뜻하지 않는다.**
조작 시연용 ID: P_LU / P_LD / P_RU / P_RD. 임시 측면 밴드 4개와 후방 지지 링.
확인되지 않은 5점식·6점식 구조를 주장하지 않는다. 동일 형식 후면 자료 확보 후 교체 필요.

Basis + Tighten_<ID> 4개. 각 ID별 Strap_<ID> 정점 그룹.
Blender 0/0.5/1 ↔ Unity BlendShape Weight 0/50/100.
토폴로지·정점 순서 확정 후 키 제작. 이후 수정은 동일 정점의 좌표 조정으로 수행.
고정 후면 단면 및 버클 가이드 단면의 변형 이동 최대 **0.000000000m**.
해당 밴드의 휘어짐과 가이드 전후 구간·자유 끝만 움직임. 본체·렌즈·정화통에 키 없음.
각 Key 0/.5/1, 전체=1: 비인접 삼각형 자기 교차 검사 모두 0건.
BVH 검사는 정적 메시 자체의 교차 검사이며 사람 머리/손과의 충돌 또는 실제 착용 검증은 아니다.

| ID | Pull Key=0 XYZ(m) | Pull Key=1 XYZ(m) | 당김 방향 XYZ |
|---|---|---|---|
| P_LU | (-0.104000, -0.038000, 0.042000) | (-0.123000, -0.061000, 0.030000) | (-0.5909, -0.7153, -0.3732) |
| P_LD | (-0.104000, -0.038000, -0.081000) | (-0.123000, -0.061000, -0.093000) | (-0.5909, -0.7153, -0.3732) |
| P_RU | (0.104000, -0.038000, 0.042000) | (0.123000, -0.061000, 0.030000) | (0.5909, -0.7153, -0.3732) |
| P_RD | (0.104000, -0.038000, -0.081000) | (0.123000, -0.061000, -0.093000) | (0.5909, -0.7153, -0.3732) |

Pull Empty는 기본 상태 위치에 고정. Shape Key에 자동 추종하지 않는다.
Unity 담당자는 위 위치를 weight/100으로 보간하고 기준 축 변환 후 적용한다.
XRI 양손 잡기·착용 판정·점검 판정·다시하기는 별도 구현 범위.

## 텍스처와 URP 연결
BaseColor는 sRGB, 나머지 데이터 맵은 sRGB OFF(선형).
Normal은 tangent-space OpenGL(+Y). Unity Texture Type을 Normal map으로 설정.
미세 고무/직물 높이에서 수치 생성한 tangent normal이며 고해상도 모델 전사 베이크는 아니다.
최종 삼각형·UV 기준 사용. AO는 Cycles AO 노드(거리 0.016m)→Emission 베이크, 16 samples, dilation 12px.
사진·로고·인쇄물 복제 없음.

| 재질 | URP Lit 입력 | PNG/설정 |
|---|---|---|
| MAT_K1_Body | Base Map | BaseColor.png, sRGB ON, Opaque |
| MAT_K1_Body | Normal Map | Normal.png, Normal map, +Y |
| MAT_K1_Body | Metallic Map | MetallicSmoothness.png: R=Metallic, A=Smoothness |
| MAT_K1_Body | Occlusion Map | AO.png: G=AO(RGB 동일 회색), sRGB OFF |
| MAT_K1_Harness | Base Map | Harness_BaseColor.png, sRGB ON, Opaque |
| MAT_K1_Harness | Normal Map | Harness_Normal.png, Normal map, +Y |
| MAT_K1_Harness | Metallic Map | Harness_MetallicSmoothness.png: R=Metallic, A=Smoothness |
| MAT_K1_Lens | Base Color / Surface | (0.25,0.34,0.29), Alpha 0.24, Roughness 0.18, IOR 1.46; Transparent Alpha 후보 |

Smoothness=1-Roughness. Unity에서 Smoothness Source=Metallic Alpha로 설정해 확인.
고무·도장 정화통·플라스틱 Metallic=0. 임시 버클 영역만 Metallic=0.35.
끈 AO 생략: 타일링 UV는 고정 고유 차폐에 맞지 않고 조임 시 음영이 변하므로 고정 AO를 넣지 않음.
렌즈: 좌우 공유 단색 재질, 별도 텍스처 없음. Transmission=0; 화면 복사·굴절·커스텀 셰이더 없음.
Blender 투명 렌즈는 Eevee dithering으로 프리뷰에 노이즈가 보일 수 있다. 실제 URP 투명도·정렬은 사용자 확인 필요.
Unity .mat는 생성하지 않았다. Blender 노드 전체 자동 변환을 전제하지 않는다.
FBX 재가져오기에서 BaseColor/Normal 4장 연결은 확인. MetallicSmoothness 2장과 AO는 PNG 파일·연결표로 제공하며 URP에 수동 연결해야 한다.

## UV와 미프맵
모든 렌더 Mesh UV0. 본체 Atlas는 오브젝트별 독립 사각 영역에 Smart UV packing.
의도된 겹침: 끈의 타일링/4개 밴드 공유, 단색 렌즈의 앞뒤·좌우 투영 UV.
본체 Atlas의 의도된 겹침 없음.
2K 픽셀 중심 삼각형 검사: 414개 섬, 교차 섬 겹침 0 pixel, 9px 미만 이웃 섬 검출 없음.
Packing 목표 margin 16px, 요구 기준 8px. 이 검사는 래스터 검사이며 연속 좌표 최소 간격의 해석적 증명은 아니다.
Previews/08_UVChecker.png 제공. 작은 부품의 texel density는 본체와 다르며 정밀 인쇄용 UV가 아니다.
Atlas 각 영역 주변은 같은 표면 색으로 연장. 컬러 무늬 경계가 없어 초기 미프 번짐 위험을 줄였다.
Unity 실제 mip 단계·압축·Normal import 결과는 사용자 확인 필요.

## 충돌 프록시
루트 K1_Collision_Root: (0,0,0), Rotation 0, Scale 1. 렌더 루트와 같은 기준.
5개, 총 92 triangles, 각각 128 미만. 닫힌 Convex.
전면·좌·우·상단을 나눠 얼굴 공간을 단일 볼록 덩어리로 채우지 않음.

| 프록시 | triangles | 부모 | 피벗 Blender XYZ(m) | 크기 XYZ(m) |
|---|---:|---|---|---|
| COL_Facepiece | 12 | K1_Collision_Root | (0.000000, -0.080000, 0.094000) | (0.125000, 0.027000, 0.025000) |
| COL_Facepiece_L | 12 | K1_Collision_Root | (-0.069000, -0.042000, -0.012000) | (0.033000, 0.064000, 0.135000) |
| COL_Facepiece_R | 12 | K1_Collision_Root | (0.069000, -0.042000, -0.012000) | (0.033000, 0.064000, 0.135000) |
| COL_Front | 12 | K1_Collision_Root | (-0.000000, -0.105000, -0.068000) | (0.070000, 0.045000, 0.084000) |
| COL_Filter | 44 | K1_Collision_Root | (-0.115499, -0.056925, -0.044463) | (0.104525, 0.121320, 0.113940) |

Convex 평면 이탈 최대 약 0.00000014m(부동소수 오차), 열린 경계 0.
COL_ 이름/FBX만으로 Unity Collider가 생성되지 않는다.
Unity 담당자가 Primitive 또는 Convex MeshCollider를 연결해야 한다.
손가락/직물/끈 충돌 메시 없음. 실제 충돌 여유와 손 조작은 미검증.

## FBX 설정과 왕복 검사
Blender 5.2.1 LTS, 번들 FBX exporter 5.15.0, FBX 7400.
Selected Objects; Mesh/Empty; Scale 1; Apply Unit Scale ON; Apply Scalings FBX_SCALE_ALL.
Forward=-Z / Up=Y; Use Space Transform ON; Bake Space Transform(Apply Transform) OFF.
Use Mesh Modifiers OFF(Shape Key 보존), 이미 확정된 삼각형, Tangent Space ON.
Bake Animation OFF, Embed Textures OFF, Path Mode RELATIVE, custom properties ON.
카메라/조명/UV checker/와이어 재질/충돌 프록시는 렌더 FBX에서 제외.
기존 Scene의 Cube/Light/Camera도 제외.

별도 임시 Blender 장면에서 렌더·충돌 FBX 재가져오기:
- 렌더 9 Mesh, 필수 계층·UV0·Mesh당 재질 슬롯 1 유지.
- 앵커 16개 이름/부모/위치/+Z 방향 유지. 최대 위치 오차 0.0000000269m.
- 4개 Shape Key 이름·개수·정점 수 유지. 각 0/.5/1 변형 최대 오차 약 0.00000003m.
- 전체 Key=1 조합 최대 오차 0.0000000300m.
- Mesh bounding box 최대 오차 0.0000000224m.
- 루트 Scale=(1,1,1), Mesh Scale=(1,1,1), 100배 차이·좌우 반전 미발견(Blender 왕복 기준).
- 충돌 5 Mesh, 면수·조립 위치 유지.
- BaseColor/Normal 상대 경로 해석 및 파일 존재 확인. URP packed map은 수동 연결 대상.

렌더 Mesh 모두 열린 경계 0, non-manifold edge 0, 면적 0 면 0. 고무 내부와 외부 두께 유지.
의도한 셸 조립 중첩이 있으므로 제조용 watertight 단일체 검사 결과로 해석하지 않는다.
동일 형식 사진이 없는 후면/안쪽/버클은 실물 대조 통과로 기록하지 않음.
Blender 왕복은 Unity 임포트·실행·VR 실기 성공이 아니다.

## Unity에서 사용자가 확인할 순서
1. 1m 기준 물체와 크기 비교. 임시 본체 폭 약 0.18m. 100배 보정/부모·자식 Scale 상쇄가 필요하면 사용 중지 후 임포트 축·단위 재검토.
2. +Z 전방/+Y 위, 정화통 -X(착용자 왼쪽), FaceFit 원점 확인. 루트 회전과 손 모델 회전 오프셋을 별개로 확인.
3. Part 9개, Anchor 16개, Tighten_ 4개와 UV0 유지 확인.
4. 위 URP 표대로 수동 재질 연결. Normal map 타입/Metallic R/Smoothness A/AO G 확인.
5. BlendShape 0/50/100 및 모두100에서 끈만 변형, 고정 가이드/접합 이탈·관통·외관 노이즈 확인.
6. 별도 XR 연결 이후 양손 잡기, 얼굴 정렬, 로컬 사용자/다른 카메라 표시 전환, 점검 후보와 다시하기를 확인.
7. 로컬 착용 시 외부 본체/렌즈 숨김은 Unity 카메라별 표시 제어로 구현. 다른 아바타/카메라는 표시 유지 가능하도록 분리됨.

Unity Play/Simulator/batchmode/자동 테스트/빌드 수행 없음. 코드/Prefab/씬/.mat/.meta/Packages/ProjectSettings 작성·수정 없음.
실제 얼굴 밀착·호흡·방호 성능을 평가하지 않음. 교육 절차·제한시간·합격 기준은 승인 교안 담당 범위.
