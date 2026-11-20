# 휴대장비 제독 가상 소품 — 제작·검증 기록

2026-11-05. Blender 5.2.1 LTS 제작. 실제 장비·제독킷 미선정 상태에서 조작 구조를 시연하는 **가상 소품**이다. 실제 군 제독킷의 형상·규격·구성·사용법, 약제 구조, 방호 인증 또는 폐기 규정을 재현하지 않는다.

## 1. 재사용 확인과 제작 제외

`ReuseInspection.json`에 원본 FBX 재반입 치수, 개별 메시·리그 의존성, 씬 도형의 부모 변환을 포함한 실측을 기록했다. 아래 FBX 크기는 Blender X×Y×Z이며, 자세·끈까지 포함한 전체 경계다. 인체/착용 크기와 장비 본체 규격을 혼동하지 않는다.

| 기존 경로 | 전체 경계(m), 반올림 | 재사용 판단 |
|---|---|---|
| `Assets/B/Models/K1GasMask/K1_GasMask.fbx` | 0.2703×0.3070×0.2340 | 독립 9메시 장비. 방독면 제작 제외. 기존 Training Prefab 유지 |
| `Assets/B/Models/K1Carrier/K1_Carrier.fbx` | 0.3540×0.3722×0.4582 | 독립 8메시 휴대주머니. 가상 케이스 대체 용도로 전용하지 않음 |
| `Assets/B/Models/ProtectiveClothing/ProtectiveClothing_Pickup.fbx` | 0.3400×0.3600×0.2870 | 6종 강체 Pickup 메시 재사용 가능. 보호의·장갑·덧신 제작 제외 |
| `Assets/B/Models/ProtectiveClothing/ProtectiveClothing_Worn.fbx` | 1.7836×0.3600×1.5298 | 6종 스킨 메시. 기존 골격·바인드와 함께 재사용. 단독 강체 도구로 사용하지 않음 |
| `Assets/B/Models/Mannequin/Mannequin.fbx` | 1.7913×0.3115×1.7694 | 메시 1개와 기존 골격 재사용. 마네킹·인체 제작 제외 |
| `Assets/B/Scene/B_ProtectiveClothing_Equipment.unity` 및 `B_ProtectiveClothing_Tutorial.unity`의 `PC_Workbench` | 폭0.85×깊이1.30×높이0.85 | 독립 씬 Cube. 작업대 신규 제작 제외 |
| `Assets/B/Scene/B_PressureBandage_Tutorial.unity`의 `PB_Tray` | 폭0.45×깊이0.30×높이0.90 | 독립 씬 Cube 받침 구성. 일반 준비/받침 역할 재사용 후보 |
| `Assets/B/Scene/B_Tourniquet_Tutorial.unity`의 `TQ_Tray` | 폭0.50×깊이0.35×높이0.78 | 독립 씬 Cube 받침 구성. 일반 준비/받침 역할 재사용 후보 |

PB_Tray/TQ_Tray는 별도 완성형 트레이 FBX가 아니며 내부가 빈 회수 용기도 아니다. 씬·Prefab·모델 목록에 적합한 별도 제독 케이스·패드형 도구·열린 회수 용기는 없었다. AED·지혈대·압박붕대는 기존 의료 역할을 유지하며 제독 소품으로 전용하지 않았다. 기존 장비의 존재·재사용 가능성은 실제 제독 대상 적합성 승인을 뜻하지 않는다. 기존 자산 배치나 Unity 연결 작업은 수행하지 않았다.

## 2. 신규 파일

- `../DC_KitCase.fbx`
- `../DC_Applicator.fbx`
- `../DC_CollectionBin.fbx`
- `Decontamination_Prototype.blend`
- `Previews/01_Overview.png` ~ `08_Pivots_Anchors.png`
- `ValidationReport.json`, `ReuseInspection.json`: 수치·계층·왕복 대조 기록
- `PreservationBaseline.json`, `PreservationCheck.json`: 기존 파일 해시 보존 대조
- `PreviewGeometry.json`: 피벗 도면의 원본 좌표

텍스처는 사용하지 않아 Textures 폴더는 만들지 않았다. 신규 산출물은 모두 `Assets/B/Models/Decontamination/` 안에 있다. 기존 출력 폴더는 작업 시작 시 존재하지 않았다.

## 3. 치수·부품·앵커

개발용 가정 치수. 폭×깊이×높이. 각 게임 루트 원점은 바닥 중심 `(0,0,0)`, 회전 0, 축척 1. 아래 위치는 해당 루트 기준 **Blender 좌표(m)**다.

| 모델 | 최종 외형(m) | 메시 | triangles |
|---|---|---|---:|
| DC_KitCase | 0.180×0.1345×0.060 | Part_CaseBody, Part_CaseLid | 1,470 |
| DC_Applicator | 0.100×0.070×0.032 | Part_PadGrip, Part_PadSurface | 712 |
| DC_CollectionBin | 0.220×0.180×0.160 | Part_BinBody | 788 |
| 합계 | 메시 5개 | 공유 재질 4개 | **2,970** |

케이스 깊이는 경첩 돌출 4.5mm를 포함한다. 뚜껑 원점과 `Anchor_LidHinge`는 `(0,0.066,0.044)`, 회전축은 **로컬 +X**다. 열림 확인 자세는 X=-110°. 리그·키프레임·개폐 애니메이션은 없다. `Part_CaseLid`와 두 Anchor는 케이스 루트의 직접 자식이며 뚜껑은 자신의 원점을 중심으로 회전한다.

- `Anchor_ToolRest`: `(0,0,0.004)`; 내부 바닥 접촉 기준, +Z 위.
- `Anchor_Grip`: `(0,0,0.023)`; 파지부 중심, +Z 위.
- `Anchor_Contact`: `(0,0,0)`; X회전180°. **로컬 +Z = Blender -Z**, 작용 면 바깥 법선.
- `Anchor_Opening`: `(0,0,0.160)`; 입구 중앙, +Z 위.
- `Anchor_Return`: `(0,0,0.006)`; 용기 내부 바닥 놓임 기준, +Z 위.

모든 Part/Anchor는 해당 모델 독립 루트에 속한다. 앵커는 상태 전환이나 판정 기능을 제공하지 않는다.

패드 총높이는 파지부 식별·잡기 시연을 위해 제안 20mm에서 **32mm**로 조정했다. 작용 면의 **평평한 바깥 면은 96×66mm, 모서리 반경9mm, z=0**이다. 롤 모양 테두리를 포함한 Part_PadSurface 최대 폭·깊이는 100×70mm, 두께6mm다. 밝은 ivory 작용 면과 graphite 파지부는 독립 메시·독립 재질이다. 뒤집어도 밝은 바닥/주변 테두리와 솟은 파지 형상이 구분된다. 상태 표현은 추후 해당 작용 면 재질만 교체할 수 있는 구조이며 기능은 구현하지 않았다.

케이스 바닥 평면은 164×114mm, 바닥 z=4mm, 위쪽 내부 폭·깊이168×118mm, 림 z=43mm, 닫힌 뚜껑 중앙 안쪽 z=54mm다. 중앙에 놓인 도구 최고점은 z=36mm여서 닫힌 중앙 천장과 18mm 여유가 있다. 각진 외접 크기로 비교해도 바닥에 도구 좌우32mm·앞뒤22mm씩 여유가 있다.

회수 용기는 바닥·안쪽 벽·두꺼운 림이 이어진 열린 중공 구조다. 입구 안쪽204×164mm, 림 아래 중앙 통과부 최소202×162mm, 평평한 바닥168×128mm, 바닥 z=6mm, 내부 깊이154mm다. 도구를 중앙에 수평 유지해 통과할 때 좁은 통과부 기준 좌우51mm·앞뒤46mm씩 여유가 있다. 이는 중앙 경로의 기하 확인이며 임의 기울기·손·컨트롤러 전체의 통과 검증은 아니다.

## 4. Blender 구성·FBX 변환

- Metric, Unit Scale=1, 1단위=1m.
- 제작 축: **Blender +Z 위, -Y 전방**. 목표 Unity +Y 위, +Z 전방.
- Blender 기본 FBX exporter: `axis_forward=-Z`, `axis_up=Y`, `global_scale=1`, `apply_unit_scale=True`, `apply_scale_options=FBX_SCALE_UNITS`, `use_space_transform=True`, `bake_space_transform=False`.
- 축 변환은 exporter에서 한 번만 수행했다. 수동 사전 회전·바이너리 후처리는 없다. Unity에서 추가 90° 보정을 임의로 중복 적용하지 말고 실제 임포트 계층을 확인한다.
- `use_selection=True`, `object_types={MESH,EMPTY}`, `bake_anim=False`. 개별 루트·해당 메시·해당 Anchor만 선택했다. FBX별 오브젝트 수: 케이스5 / 패드5 / 용기4.
- `Decontamination_Prototype` 씬: `GAME_Export/EXPORT_*`의 게임 원본은 각각 원점. `REFERENCE_Existing_ReadOnly`에는 기존 장비의 숨긴 참고용 재반입본만 둔다.
- `REVIEW_Presentation` 씬: `PHOTO_Previews`에 원본 메시를 공유한 촬영 복사본·카메라·조명. 나란히 배치와 열림 자세는 이 씬에만 있다. 바닥·방·작업대·치수 메시·안내 UI·화살표 메시를 만들지 않았다.
- 원본 저장 시 게임용 뚜껑은 닫힘, 촬영 복사본만 열림. 카메라·조명·촬영 복사본·기존 참고 장비는 FBX에서 제외했다.

재질은 단순 Principled Base Color/Roughness/Metallic0로 구성했다. 새 모델에 이미지·외부 텍스처 의존성은 없다. URP에서는 같은 4개 공유 재질을 Lit의 Base Color/Smoothness로 연결하고 작용 면 슬롯을 따로 유지하면 된다. Blender FBX 재질은 Unity URP 자동 연결 완료를 뜻하지 않는다. 참고 장비의 기존 재질·텍스처는 신규 세트의 4개 집계에 포함하지 않았다.

## 5. 확인 범위와 결과

- 5개 신규 메시: 닫힌 manifold, 양의 signed volume, 뒤집힌 전체 방향 없음. 중복 위치 정점·느슨한 정점·면적0 삼각형 없음. 모든 메시 UV 1개, 유한 좌표·0..1 범위.
- 케이스·뚜껑 및 수납 도구와 뚜껑: 0~110°를 1° 간격으로 표면 삼각형 BVH 교차 검사, 교차0. 연속적인 모든 각도의 수학적 증명은 아니다.
- 도구는 케이스 바닥에 접한다. 이 접촉을 관통으로 계산하지 않았으며 0.1mm 들어 올렸을 때 케이스와 교차0을 확인했다.
- 용기 중앙 수직 회수 경로: 바닥 접촉 위치보다 1~201mm 위 구간을 1mm 간격으로 확인, 교차0. 용기 바닥·벽의 두께와 열린 입구는 렌더에서도 확인했다.
- 모든 FBX를 새 Blender 씬에 재반입해 크기·메시/Empty 이름·부모 관계·루트 원점·축척·앵커 +Z·UV 존재·재질 슬롯·triangles·양의 체적·manifold를 대조했다. 최대 정점 오차 약 **0.00003mm**, 행렬 요소 오차 약1.63e-7. 수치 원본은 `ValidationReport.json`.
- 법선 방향·UV·기본 재질과 질감 경로를 확인했다. 텍스처가 없어 누락된 신규 텍스처 경로는 없다. 기존 참고 FBX의 지원하지 않는 Short 사용자 속성 경고는 참고 가져오기에서 발생했으며 신규 FBX에는 사용자 속성을 넣지 않았다.
- 전체 사선, 케이스 닫힘/열림·도구 수납, 파지부/작용 면, 용기 내부·도구 회수, 피벗/앵커 도면을 별도 미리보기로 제공한다. 도면의 선·문자는 검토 이미지이며 모델·텍스처에 들어가지 않는다.
- 저장된 `.blend` 재열기와 기존 파일 보존 결과는 `ValidationReport.json`의 `blend_reopen`, `PreservationCheck.json`을 따른다.

## 6. 미확정 사항·사용자 Unity 확인

대상 장비, 실제 제독킷·재료·약제, 준비·처리·누락·재접촉·회수 규칙, 대상 표면, 실제 크기·손 도달성과 파지 자세는 미확정이다. 이번 모델의 크기는 개발용 가정이며 실제 교안/제품 치수로 사용하지 않는다.

Unity는 실행하지 않았다. 임포트·VR 조작·URP 연결·교육 기능 확인 완료로 보고하지 않는다. 사용자 확인 순서:

1. 각 FBX를 임포트한 뒤 크기1m 기준·+Y 위/+Z 전방·계층·루트 축척 및 음수 축척 유무 확인. 추가 축 회전을 중복 적용하지 않음.
2. Part_CaseLid 자체 피벗 및 Anchor_LidHinge 일치, 개폐 방향, 도구 수납·관통 여부 확인.
3. 4개 URP 공유 재질 연결, Part_PadSurface 별도 슬롯, 양면 관찰 시 작용 면 식별·노멀·깜빡임 확인.
4. Anchor_Contact의 **로컬 +Z가 작용 면 바깥쪽**인지 확인. 파지/접촉/입구/회수 기준의 위치 확인.
5. 추후 사용자 요청으로 잡기 기능을 구현한 뒤 실제 VR에서 손·컨트롤러 크기, 파지부 접근, 수납·회수 입구·도달 거리 확인. 이번에는 Collider·Rigidbody·스크립트·Prefab·씬 구현 없음.

Unity 실행·테스트·빌드·커밋·Push 없음. 기존 B 및 A/C·공용 모델, 코드·씬·Prefab·meta·Packages·ProjectSettings 변경 없음. 작업 전부터 존재하던 사용자 변경은 보존했다.
