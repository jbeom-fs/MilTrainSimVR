# K1 Gas Mask — 좌우 수정·머리끈 리그 인수인계

수정일: 2026-10-26. 상태: **Blender 수치 검사와 FBX 왕복 검증 완료. 실물 후면 구조·실측·Unity/VR 미검증.**
기존 K1 모델을 수정했다. 추가 후면·안쪽·버클 사진 및 실측 자료는 사용자가 없다고 답했다.
실제 K1의 끈 개수, 군 착용 절차, 교육 합격 기준을 확정한 모델이 아니다.

## 파일과 보존

- [K1_GasMask.blend](K1_GasMask.blend): 수정 원본, 작업 장면 K1_Prototype_Workshop.
- [렌더 FBX](../K1_GasMask.fbx): Mesh 9개, Armature 1개/본 24개, Empty 21개(앵커 18개 포함), 조임 Key 4개.
- [충돌 FBX](../K1_GasMask_Collision.fbx): 루트 Empty와 고정 프록시 Mesh 5개.
- [K1_Validation.json](K1_Validation.json): 본·앵커 좌표/축, 205개 경로 포즈, 실제 검사 수치.
- [수정 전 백업](Backups/20261026_101035_before_handedness_rig/BackupManifest.json): 원본 blend, 렌더·충돌 FBX, 문서 3개와 SHA256.
- Textures/의 기존 본체 2K 4장·머리끈 1K 3장을 재사용했다.
- 기존 Scene의 Cube/Light/Camera 및 다른 작업물을 보존했다.
- 기존 Previews/는 수정 전 v1 자료다. 현재 좌우·리그 검증 자료로 사용하지 않는다. 이번 수정에서는 이미지·렌더·스크린샷을 생성하거나 변경하지 않았다.

Unity 코드·Prefab·씬·.mat·.meta·Packages·ProjectSettings를 수정하지 않았다.
Unity 실행·Simulator·batchmode·자동 테스트·빌드는 수행하지 않았다.

## 좌우·치수·루트

Blender 전방 -Y / 위 +Z에서 **착용자 왼쪽 +X, 오른쪽 -X**다.
기존 문서의 +X=착용자 오른쪽 설명은 오류였다. 형상도 지정한 왼쪽 정화통형과 반대였으므로,
렌더 형상·전체 Shape Key 좌표·피벗·앵커 위치/방향·충돌 프록시를 X 반전하여 실제 데이터에 반영했다.
면 winding을 뒤집어 바깥쪽 법선을 유지했다. L/R 명칭은 반전 후 실제 착용자 좌우와 일치하므로 유지했다.

정화통/연결부 피벗은 (+0.075,-0.044,-0.038)m, 관 부품 피벗은 (-0.052,-0.067,-0.047)m.
왼쪽 렌즈·Grip·P_L*·COL_Facepiece_L은 +X 쪽이다. 오른쪽은 -X 쪽이다.
루트·렌더 Mesh·충돌 Mesh·Armature 오브젝트 Scale=(1,1,1). 음수 Scale이나 100배 보정을 사용하지 않는다.

Metric, Unit Scale=1.0, Blender 1단위=1m. FaceFit/루트 원점은 (0,0,0).
안면부 폭 약 0.18m는 제작 가정이며 실제 대/중/소 규격 실측값이 아니다.
Unity 목표는 +Z 전방/+Y 위, 착용자 왼쪽 -X다. Blender X 부호를 그대로 Unity의 해부학적 좌우에 적용하지 않는다.
기하학적 목표 좌표 대응은 (x,y,z)Blender → (-x,z,-y)Unity이지만, FBX/Unity 임포터가 적용하는 변환을 확인한 뒤 사용한다.
이미 수행된 변환을 다시 적용하면 이중 반전된다. Unity의 실제 축·크기는 사용자가 확인해야 한다.

## 렌더 부품과 예산

LOD0 총 **13,620 triangles**, Mesh 9개, 공유 재질 3개, Mesh당 슬롯 1개.
목표 15,000 triangles / 상한 25,000 / Mesh 20개 이하를 충족한다.
본 24개, 정점당 최대 2개 본 영향, 웨이트 합 1. 고정 본체 8개 Mesh는 스킨하지 않았다.
Mesh 수를 실제 draw call 수로 간주하지 않는다.

| 부품 | triangles | 부모 | 피벗 Blender XYZ(m) |
|---|---:|---|---|
| Part_Buckles | 768 | Visuals | (0.000000, 0.000000, 0.000000) |
| Part_Facepiece | 7156 | Visuals | (0.000000, 0.000000, 0.000000) |
| Part_FilterCanister | 1884 | Visuals | (0.075000, -0.044000, -0.038000) |
| Part_FilterConnector | 476 | Visuals | (0.075000, -0.044000, -0.038000) |
| Part_FrontAssembly | 1232 | Visuals | (0.000000, -0.106000, -0.047000) |
| Part_Harness | 1392 | Visuals | (0.000000, 0.000000, 0.000000) |
| Part_Lens_L | 192 | Visuals | (0.040000, -0.084000, 0.038000) |
| Part_Lens_R | 192 | Visuals | (-0.040000, -0.084000, 0.038000) |
| Part_TubeAssembly | 328 | Visuals | (-0.052000, -0.067000, -0.047000) |

Part_Harness는 736정점/1,392 triangles다. Part_Buckles의 후두부 지지 링을 Part_Harness로 옮겼다.
고정 버클/가이드는 Part_Buckles에 남았다. 재질은 Body, Harness, Lens 3종을 공유한다.
정화통 피벗은 연결면 중심이며 분리 가능한 Mesh다. 실제 나사 체결 기능은 없다.
관의 실제 기능·최종 연결 경로, 내부 씰과 버클 세부는 확인되지 않았다.

## 머리끈 리그와 입력 계약

Armature: K1_HarnessRig. Armature modifier는 Part_Harness에만 있다.
Cloth/Soft Body, IK, Constraint, Blender 드라이버 또는 애니메이션 클립은 없다.

| 본/패턴 | 개수 | 부모 | 역할/입력 |
|---|---:|---|---|
| HarnessRoot | 1 | 없음 | 본체 기준. 본체에 고정 |
| RearCenter | 1 | HarnessRoot | 후두부 지지 중심 위치·회전 |
| RearGrab_L / RearGrab_R | 2 | RearCenter | 양손 목표 위치·회전 |
| Attach_P_LU/LD/RU/RD | 4 | HarnessRoot | 앞쪽 고정 가이드·꼬리 기준. 이동 금지 |
| SpanNear_<ID> | 4 | 해당 Attach_<ID> | 앞 고정점에서 시작하는 첫 구간 |
| SpanMid/Rear/End_<ID> | 12 | 해당 RearGrab_L/R | 중간·후방·끝 구간 변형 |

끈마다 4개 닫힌 직선 구간을 별도로 스킨한 관절식 근사다. 각 구간 본은 회전과 양의 로컬 Y 길이 스케일로 구동한다.
구간 끝에는 각각 3.5mm의 의도적 겹침이 있어 굽힘 접합부를 덮는다. 매끈한 연속 고무/직물 물리 모델은 아니다.
후두부 링은 RearCenter와 좌우 RearGrab에 혼합 가중했다. 앞 가이드/자유 꼬리는 Attach 본으로 고정했다.

**RearGrab 두 개만 움직여서는 모든 연결 구간의 길이·방향이 자동 계산되지 않는다.**
Unity 경로 제어기가 RearCenter/RearGrab 입력에서 16개 Span의 방향·길이를 계산해야 한다.
HarnessRoot/Attach와 고정 본체를 움직이지 않고, SpanNear의 시작점을 앞 가이드에 유지한다.
그 뒤 Tighten Key로 꼬리를 당긴다. 손 놓기/다시 잡기/착용 판정/초기화는 별도 XR 구현이다.

내보낸 기본 자세에서 본의 pose scale은 1이다.
검사 경로에서는 Span의 로컬 Y 길이 스케일 약 0.714269~2.130339를 사용했다.
이것은 경로 표현용 신축 근사이며 실물 고무의 허용 신장률을 뜻하지 않는다.
오브젝트의 비균일/음수 Scale 보정과 구분한다.

본의 로컬 +Y는 head→tail이다. rest의 +Z는 세계 Z를 +Y에 수직 투영하여 정하고 +X는 +Y×+Z다.
본별 실제 head/tail/축/rest 행렬은 JSON rig.bones에 기록했다.
부모·자식 Span을 연속 체인으로 연결하지 않아 구간 길이 스케일이 다음 구간에 누적되지 않는다.

## 움직이는 앵커와 기존 앵커

Anchor_HarnessGrab_L/R은 각각 RearGrab_L/R에 **BONE 부모**로 연결되어 실제 변형을 따라간다.
이동·회전 입력의 기준이며 본의 rest 프레임과 정렬했다.
기존 StrapPull/Guide Empty는 본체 Anchors 아래 정적 기준이다. Shape Key를 자동 추종하지 않는다.
Pull의 Key0/Key1 좌표와 당김 방향은 JSON anchors에 기록했다. Unity에서 BlendShape weight에 맞춰 보간해야 한다.
FilterMate는 정적 Empty이므로 정화통 분리 구현 시 대상 피벗과 함께 추적하도록 연결한다.
Grip/Inspect/ViewOrigin은 실측되지 않은 후보이며 손 오프셋과 검사 교안 승인이 필요하다.

아래 위치/방향은 기본 자세의 Blender 장면 기준. 본 부모 Empty의 matrix_local은 손 오프셋과 동일하지 않다.

| 앵커 | 부모 또는 본 | 장면 위치 XYZ(m) | 장면 +Z 방향 |
|---|---|---|---|
| Anchor_FaceFit | Anchors | (0.000000, 0.000000, 0.000000) | (0.000000, -1.000000, -0.000000) |
| Anchor_FilterAttach | Anchors | (0.075000, -0.044000, -0.038000) | (0.941838, -0.300587, -0.150293) |
| Anchor_FilterInspect | Anchors | (0.151289, -0.068348, -0.050174) | (0.941838, -0.300587, -0.150293) |
| Anchor_FilterMate | Anchors | (0.075000, -0.044000, -0.038000) | (-0.941838, 0.300587, 0.150293) |
| Anchor_FrontInspect | Anchors | (0.000000, -0.134000, -0.047000) | (0.000000, -1.000000, -0.000000) |
| Anchor_Grip_L | Anchors | (0.054665, -0.038865, -0.066043) | (0.738815, -0.420639, -0.526512) |
| Anchor_Grip_R | Anchors | (-0.054665, -0.038865, -0.066043) | (-0.738815, -0.420639, -0.526512) |
| Anchor_HarnessGrab_L | RearGrab_L | (0.040000, 0.170000, 0.005000) | (0.000000, 0.000000, 1.000000) |
| Anchor_HarnessGrab_R | RearGrab_R | (-0.040000, 0.170000, 0.005000) | (0.000000, 0.000000, 1.000000) |
| Anchor_StrapGuide_P_LD | Anchors | (0.080000, 0.002000, -0.056000) | (0.590872, -0.715266, -0.373182) |
| Anchor_StrapGuide_P_LU | Anchors | (0.080000, 0.002000, 0.067000) | (0.590872, -0.715266, -0.373182) |
| Anchor_StrapGuide_P_RD | Anchors | (-0.080000, 0.002000, -0.056000) | (-0.590872, -0.715266, -0.373182) |
| Anchor_StrapGuide_P_RU | Anchors | (-0.080000, 0.002000, 0.067000) | (-0.590872, -0.715266, -0.373182) |
| Anchor_StrapPull_P_LD | Anchors | (0.104000, -0.038000, -0.081000) | (0.590872, -0.715266, -0.373182) |
| Anchor_StrapPull_P_LU | Anchors | (0.104000, -0.038000, 0.042000) | (0.590872, -0.715266, -0.373182) |
| Anchor_StrapPull_P_RD | Anchors | (-0.104000, -0.038000, -0.081000) | (-0.590872, -0.715266, -0.373182) |
| Anchor_StrapPull_P_RU | Anchors | (-0.104000, -0.038000, 0.042000) | (-0.590872, -0.715266, -0.373182) |
| Anchor_ViewOrigin | Anchors | (0.000000, 0.018000, 0.038000) | (0.000000, -1.000000, -0.000000) |

앵커별 Euler 회전·+Y 방향·전체 행렬도 JSON anchors에 수록했다.

## 조임 Key

Basis + Tighten_P_LU / Tighten_P_LD / Tighten_P_RU / Tighten_P_RD.
Blender 0/.5/1 → Unity BlendShape weight 0/50/100.
P_는 provisional이다. 실제 K1의 끈 개수나 4점식/5점식/6점식 구조를 확정하지 않는다.

**머리끈 토폴로지를 재구성했으며 4개 조임 Key도 재구성했다.**
기존 이름과 ID를 1:1로 유지했지만 이전 정점 순서/Shape Key 데이터를 그대로 재사용한 것이 아니다.
기존 반전된 가이드·꼬리 목표를 유지하고 새 구간 좌표에 맞춰 변형을 다시 만들었다.
고정 가이드 단면은 리그/Key 조합에서도 움직이지 않는다. 본체·렌즈·정화통은 조임의 영향을 받지 않는다.
머리 위로 넘기기는 본 리그, 착용 후 당김은 Tighten Key로 역할을 나눈다.

## 가정한 착용 경로와 수치 검사

머리 기준: 중심 (0,0.075,0.012)m, 폭 148mm/앞뒤 170mm/높이 210mm의 분석적 타원체.
반지름에 5mm를 더한 여유 타원체도 검사했다. 실제 머리/HMD 자료가 아니며 머리 Mesh는 생성·내보내지 않았다.

| 단계 | 후두부 중심 Blender XYZ(m) | 양손 간격(m) | 후두부 X 회전(deg) |
|---|---|---:|---:|
| Loose_Front | (0.000000, -0.090000, 0.190000) | 0.320 | 90 |
| Spread | (0.000000, -0.075000, 0.200000) | 0.400 | 90 |
| Lift_Above | (0.000000, -0.030000, 0.270000) | 0.440 | 90 |
| Over_Crown | (0.000000, 0.110000, 0.270000) | 0.440 | 90 |
| Behind_Head | (0.000000, 0.225000, 0.155000) | 0.320 | 0 |
| Seated | (0.000000, 0.170000, 0.005000) | 0.080 | 0 |
| Tightened | (0.000000, 0.170000, 0.005000) | 0.080 | 0 |

앞에서 벌림 → 위로 올림 → 정수리 위 통과 → 뒤로 이동/회전 → 후두부 안착 → 조임의 유한 경로다.
본체는 얼굴 기준에 고정한다. 손 위치는 추적기 위치와 동일하지 않으며 최대 간격 0.44m도 제작 가정이다.
실제 군 착용 순서로 사용하지 않는다.

5개 전이 구간을 각각 41개로 나눠 205개 표본(경계 중복 포함)을 검사했다.
각 표본의 삼각형-타원체 최소 거리 검사에서 머리 관통 0건, 의도하지 않은 끈 교차 0건,
고정 가이드 변위 0m. 정규화된 최소 거리 제곱 q는 머리 1.328349, 여유 타원체 1.188298로 모두 1보다 크다.
7개 단계와 조임 0/.5/1 조합도 확인했다.
인접한 닫힌 구간 접합부·앞 꼬리/첫 구간·후두부 링/끝 구간의 의도적 접촉은 별도로 분류했다.
따라서 원시 자기 접촉 수가 0이라는 뜻이 아니다.
표본 사이 모든 순간·임의의 손 궤적·실제 사람의 머리·HMD 적합성을 증명하는 검사는 아니다.

각 RearGrab을 옆 15mm+위 8mm 이동한 독립 제어 검사에서 해당 측 변위 17mm,
반대측 끈 0m, 고정 꼬리 0m였다. 이 검사는 제어 분리 검사이며 전체 연결 경로는 Span 제어를 함께 적용한다.

JSON pose_samples에는 205개 포즈의 본별 행렬/실제 부모 기준 TRS/Blender basis delta가 있다.
행렬은 row-major 4×4, 거리 m, quaternion WXYZ다. Blender basis delta를 Unity localPosition에 직접 넣지 않는다.
FBX에서 본 rest 축이 바뀔 수 있으므로 좌표계를 변환한 뒤
D=sourcePose×inverse(sourceRest), importedPose=D×importedRest의 변형 행렬을 보존한다.
Armature 오브젝트 변환도 함께 고려한다. 이는 제어 데이터이며 FBX 애니메이션 클립이 아니다.

## 텍스처·UV·URP

본체 Atlas UV는 유지했고 새 끈 구간에는 타일링 UV0를 재작성했다.
꼬리 UV는 유지하고 후두부 링은 Harness 타일링 재질로 옮겼다. 끈/렌즈 UV 공유는 의도적이다.
모든 Mesh의 UV0와 슬롯 수가 FBX 왕복 후 보존됐다. 이번 수정에서 텍스처 PNG는 변경하지 않았다.
기존 본체 Atlas의 UV 래스터 검사 결과는 JSON body_uv_evidence_from_v1에 범위를 명시해 보관했다.
새 끈 전체의 UV 간격을 새로 래스터 검사했다는 뜻은 아니다.

| 재질 | URP Lit 입력 | 파일/설정 |
|---|---|---|
| MAT_K1_Body | Base Map | BaseColor.png, 2K, sRGB ON |
| MAT_K1_Body | Normal | Normal.png, tangent OpenGL +Y, Texture Type Normal map |
| MAT_K1_Body | Metallic/Smoothness | MetallicSmoothness.png, R=Metallic / A=Smoothness, sRGB OFF |
| MAT_K1_Body | Occlusion | AO.png, G=AO, sRGB OFF |
| MAT_K1_Harness | Base/Normal | Harness_BaseColor.png / Harness_Normal.png, 1K |
| MAT_K1_Harness | Metallic/Smoothness | Harness_MetallicSmoothness.png, R/A, sRGB OFF |
| MAT_K1_Lens | 색/투명 | (0.25,0.34,0.29), Alpha .24, Roughness .18, IOR 1.46, Transparent 후보 |

Smoothness=1-Roughness. Harness BaseColor만 sRGB ON, Normal/packed map은 데이터로 취급한다.
끈 AO는 동적 변형/타일링 UV 때문에 생략했다. 사진·로고·인쇄물 복제는 없다.
기존 본체 AO는 원래 형상의 미세 음영 자료이며 이번에 다시 굽지 않았다.
FBX 재가져오기에서 BaseColor/Normal 4개 경로의 실제 파일 존재를 확인했다.
MetallicSmoothness 2개와 AO는 URP에서 수동 연결한다. Blender 노드 전체의 자동 변환은 전제하지 않는다.
렌즈는 공유 단색 재질이며 굴절/화면 복사/커스텀 셰이더가 없다. 실제 URP 투명 정렬은 미검증이다.

## 충돌 프록시

K1_Collision_Root 원점/회전/Scale은 렌더 루트와 같다.
5개 Mesh, 총 92 triangles(최대 44). 착용자 좌우와 정화통 프록시를 함께 반전했다.
본체의 고정 충돌용이며 변형 머리끈을 따라가는 충돌체가 아니다.
관·직물 전체 고해상도 MeshCollider는 없다. Unity 담당자가 Primitive 또는 Convex MeshCollider를 연결한다.

| 프록시 | triangles | 피벗 Blender XYZ(m) |
|---|---:|---|
| COL_Facepiece | 12 | (0.000000, -0.080000, 0.094000) |
| COL_Facepiece_L | 12 | (0.069000, -0.042000, -0.012000) |
| COL_Facepiece_R | 12 | (-0.069000, -0.042000, -0.012000) |
| COL_Filter | 44 | (0.115499, -0.056925, -0.044463) |
| COL_Front | 12 | (0.000000, -0.105000, -0.068000) |

## FBX 설정·왕복 결과

Blender 5.2.1 LTS / FBX exporter 5.15.0 / binary FBX 7400.
Selected Objects, Mesh/Empty/Armature, Scale=1, Apply Unit Scale ON, Apply Scalings=FBX_SCALE_ALL.
Forward=-Z, Up=Y, Use Space Transform ON, Bake Space Transform OFF.
Use Mesh Modifiers OFF, Triangles/Tangent Space ON, Bake Animation OFF, Add Leaf Bones OFF,
Only Deform Bones OFF(제어 본 보존), Custom Properties ON, Path Mode RELATIVE, Embed Textures OFF.
충돌 FBX는 Mesh/Empty만 선택했다. 숨겨진 프록시를 내보내는 동안 표시하고 원래 상태로 복구했다.
카메라·조명·충돌체·검사용 객체는 렌더 FBX에서 제외했다.

별도 임시 Blender 장면에서 두 FBX를 재가져와 수치 비교한 뒤 검사용 장면/데이터를 제거했다.

- 렌더 Mesh 9개, 충돌 Mesh 5개, 본 24개 및 전체 부모 계층 보존. Leaf Bone 추가 없음.
- 본 웨이트 최대 차이 0. Shape Key 이름·정점 수 보존, 기본/Key 좌표 최대 차이 약 0.0000000352m.
- 개별 Key 0/.5/1의 변형 최대 차이 약 0.0000000654m.
- 205개 경로의 스킨 변형 최대 차이 약 0.000000153m.
- 7단계×조임 0/.5/1 조합의 변형 최대 차이 약 0.000000116m.
- 움직이는 앵커 BONE 부모 보존. 경로 추종 위치 최대 차이 약 0.0000000817m.
- 루트·Mesh·Armature 오브젝트 Scale 1, UV0/재질 슬롯 보존, 좌우·실측 가정 크기 보존.
- 모든 원본 렌더/충돌 Mesh의 열린 경계·non-manifold edge·면적 0 면은 0.
- 닫힌 조각 사이 의도적 겹침이 있는 조립체이며 단일 watertight 고무체 판정이 아니다.

왕복 변형 비교는 FBX rest 본 프레임에 변형 행렬을 맞춰 수행했다. 소스 본의 Euler/TRS를 그대로 복사한 검사가 아니다.
이 결과는 Unity 임포트·실제 VR 실행 성공을 뜻하지 않는다.

## Unity 담당 후속 확인

1. Scale 1에서 1m 기준물과 크기 비교. +Z 전방/+Y 위, 정화통은 착용자 왼쪽인지 확인.
2. Generic 본 계층 24개, SkinnedMeshRenderer, Tighten BlendShape 4개, 움직이는 앵커 부모 확인.
3. 실제 임포트 rest 축을 기준으로 경로 제어기를 연결. RearCenter/Grab 위치·회전과 16개 Span 방향·길이를 함께 구동.
4. 본체 앞 고정점 유지, 손별 독립 제어, 놓기/다시 잡기/안착/초기화 연결. BlendShape 0/50/100 및 조합 확인.
5. URP 재질·Normal·Metallic R/Smoothness A/AO G·렌즈 투명도를 수동 연결/검사.
6. 고정 프록시 Collider 연결 후 조작 공간 확인. 본체를 움직이는 끈 충돌체로 취급하지 않음.
7. Quest 2 + Link에서 HMD/머리/손 추적 위치, 잡기 오프셋, 최대 벌림, 관통, 조작 여유 확인.
8. 착용 판정·교안·시간/합격 기준은 승인된 교육 자료에 따라 별도로 확정.

실측과 동일 형식 후면 자료가 확보되면 임시 P_ 구조/후두부 망/버클을 재검토해야 한다.
