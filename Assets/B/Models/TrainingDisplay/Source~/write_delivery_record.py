import json,pathlib,hashlib,uuid
SRC=pathlib.Path(__file__).resolve().parent;BASE=SRC.parent;ROOT=SRC.parents[4]
r=json.loads((SRC/'validation.json').read_text(encoding='utf-8'))
ui=json.loads((SRC/'saved_ui_inspection.json').read_text(encoding='utf-8'))
meta=BASE.with_suffix('.meta')
if not meta.exists():
    meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
def dims(a,n=3):return ' × '.join(f'{v:.{n}f}' for v in a)
rows=[]
for key,m in r['models'].items():
    s=m['source'];rows.append(f"| {key} | {dims(m['screen_m'])} | {dims(m['opening_nominal_m'])} | {dims(s['bounds_unity_m']['size'],4)} | (0,0,0) | +0.070 | {s['triangles']} | {s['part_count']} | {s['material_count']} |")
uirows=[]
for sc in ui:
    for b in sc['boards']:
        uirows.append(f"| `{pathlib.Path(sc['scene']).name}` | `{b['path']}` | {b['size']['x']} × {b['size']['y']} | RectTransform YAML {b['line']}행 |")
text='''# TrainingDisplay 납품·검사 기록

제작일: 2026-11-10 한국 시간. Blender 5.2.1 LTS 사용. 작업 범위는 메시 제작, FBX 납품, Blender 검사와 미리보기다. 기존 UI 내용과 씬 배치는 변경하지 않았다.

## 납품 규격 표

치수 단위는 m. 외곽은 브래킷을 포함한 X 폭 × Y 높이 × Z 깊이 AABB다. 원점은 기존 UI 사각형 중심이다. 모든 모델 이동/회전=0, 배율=1로 내보냈다.

| 규격 | 화면 영역 | 개구부 공칭 폭×높이 | 전체 외곽 폭×높이×깊이 | 원점 | 벽 접촉 Z | 삼각형 | 부품 | 재질 |
|---|---|---|---|---|---:|---:|---:|---:|
'''+ '\n'.join(rows)+'''

7개 파일 이름은 `Display_AED_720x460.fbx`, `Display_TQ_720x640.fbx`, `Display_PB_720x600.fbx`, `Display_K1_720x480.fbx`, `Display_PC_720x520.fbx`, `Display_KD1_720x440.fbx`, `Display_Entrance_760x560.fbx`다. 파일명 숫자는 Canvas 단위이며, 실제 메시 좌표는 미터다. 입구 모델은 구급법·화생방 두 씬에 같은 파일을 재사용한다.

원본 `TrainingDisplay.blend`에는 파일명과 같은 이름의 별도 컬렉션 7개가 있다. 원점을 보존하기 위해 같은 위치에 겹쳐 두었고, 최초에는 AED만 뷰포트/렌더 표시를 켰다. 다른 규격을 확인할 때 AED를 숨기고 해당 컬렉션의 모니터/렌더 표시를 켠다. 오브젝트를 옮기거나 크기를 조절할 필요가 없다.

## 저장 씬·코드 대조

`Docs/B/AGENTS.md`, 저장된 운영 씬 `B_FirstAid.unity`·`B_CBRN.unity`, `Assets/B/Editor/TrainingUI/`의 과정별 BoardUi 및 TrainingBoardKit, KD1EquipmentSetup.Tutorial.cs, 관련 런타임 화면 전환 코드, 기존 거울 전달 기록과 미리보기를 읽기만 했다.

| 저장 씬 | 대상 경로 | Canvas 단위 | 근거 위치 |
|---|---|---:|---|
'''+ '\n'.join(uirows)+'''

전부 Pivot=(0.5,0.5), 로컬 배율=(0.001,0.001,0.001)이다. `TrainingBoardKit.Width=720`, AED/TQ/PB/K1/PC BoardUi의 Height=460/640/600/480/520과 일치한다. KD-1은 `CourseBoardSize=(720,440)`, 입구는 두 운영 씬에서 (760,560)으로 일치한다. 문서에 지정된 7개 화면 영역을 변경할 근거는 발견하지 못했다.

처음 비활성인 패널도 제외하지 않았다. AED StartPanel/GuidePanel/Completion/RetryButton, TQ StartPanel/OperationPanel/FinalCheckPanel/CompletionPanel/Steps 및 공통 Notice, PB Start/Practice/Completion/Steps, K1 StartPanel/ProgressPanel/CompletePanel, PC Start/Practice/Completion/StepRow, KD1 StartPanel/PracticePanel/CompletionPanel을 모두 검사 범위에 넣었다. 화면 전환 코드는 교육 상태에 따라 이 패널을 사용한다. 시작·실습·최종 확인·완료 화면마다 외형을 따로 만들지 않는다.

저장된 자식 RectTransform의 anchor·pivot·sizeDelta·anchoredPosition·scale을 부모부터 합성해 Canvas 내부 범위를 계산했다. 자식 회전은 모두 항등이었다. 정확한 계층/활성 상태/범위와 파일ID·행 번호는 `saved_ui_inspection.json`에 남겼다. 이 검사는 RectTransform 영역의 정적 확인이며 실제 TMP 글리프 경계나 가독성 확인은 아니다.

**구형 문구 차이 보고:** PB의 `Notice`는 X=-300~300, Y=338~382 Canvas 단위로 현재 화면 위쪽(Y 최대 300)을 벗어난다. 그러나 저장 비활성(`m_IsActive=0`)이고 PB 상태판의 `writesGuidance=0`, `writesDeviceLines=0`이다. PressureBandageStatusBoard.cs는 교육 씬에서 이 구형 Notice/장치 상세 문구를 숨겨 둔다고 명시한다. 이 문구 때문에 화면 높이를 늘리지 않았다. 다른 자식 RectTransform은 숨겨진 실습/완료 패널까지 포함해 기존 Canvas 안에 있다. K1의 구형 Course/Mark 역시 BoardUi가 영구 숨김 대상으로 지정하므로 외형 확장 근거로 삼지 않았다. 기존 UI는 수정하지 않았다.

운영 씬의 `FieldUI/Blocker_1_*` Canvas에도 대응 규격이 반복되어 있지만 별도 규격으로 세지 않았다. `ExitBoard`, FieldPanel, K1_RearView, KD1/PC의 구형 장비 보드, 장비 표식, AED PowerButtonCanvas/ShockButtonCanvas 등은 요청대로 제작 대상에서 제외했다.

## 구조·돌출·벽 접촉

- 베젤 폭은 직선부 14mm. 요구 화면 각 변에 2mm 여유를 더했다. 개구부 모서리 반경 2mm, 바깥 모서리 반경 4mm, 베젤 가장자리 베벨 0.35mm다. 모든 규격을 같은 수치로 새로 생성했으며 비균일 배율로 늘리지 않았다.
- `FrameCase`는 Z=-0.0005~+0.0395(40mm). `Backplate`는 Z=+0.0395~+0.0425(3mm). 케이스+덮개의 전체 깊이는 43mm다.
- 전방 최대 돌출은 0.5mm, 후방 최대 돌출은 70mm, 전체 Z 깊이는 70.5mm다. 화면 Z=0에 겹치는 별도 면은 없다. 뒤판 앞면은 UI 뒤쪽 39.5mm다.
- `WallBracket_L/R`은 뒤판에 맞닿는 짧은 L형 금속 브래킷이다. 각 브래킷을 닫힌 단일 L형 단면으로 만들었으며 겹친 박스나 내부 칸막이로 조립하지 않았다. 앞쪽 부착 위치 Z=+0.0425, 후면 평평한 벽 접촉 위치 **Z=+0.070**. 브래킷 전체 범위는 좌측 X=-0.145~-0.100, 우측 X=+0.100~+0.145, Y=-0.110~+0.110이다. 벽에 닿는 플랜지는 Z=+0.067~+0.070이다. 작은 베벨 가장자리를 제외한 후면 평면을 벽 기준으로 사용한다.
- 최종 벽 접촉점 기준으로 기존 화면 평면은 벽에서 사용자 쪽으로 70mm, 외형 가장 앞은 70.5mm 떨어진다. 브래킷과 베젤은 케이스 측방 경계를 넘지 않는다. 바닥 받침대·책상용 스탠드는 없다.
- 모델당 4부품: 접두사 `Display_<규격>_<Canvas크기>_` + `FrameCase`, `Backplate`, `WallBracket_L`, `WallBracket_R`.

## 재질

전체 7개 규격에서 실제 사용하는 Blender 재질은 2개다. 텍스처·발광·반사 기능·전원 애니메이션은 없다.

| 공유 재질 | 적용 | Base Color linear RGB | Metallic | Roughness |
|---|---|---|---:|---:|
| TrainingDisplay_MatteBlackCase | 베젤·케이스·뒤판 | (0.018,0.021,0.024) | 0.15 | 0.72 |
| TrainingDisplay_MatteBracketMetal | 후면 브래킷 | (0.065,0.075,0.082) | 0.80 | 0.68 |

브래킷은 기존 거울 금속과 같은 색감, 케이스는 더 어두운 검정으로 구분했다. 위 값은 **Blender linear** 값이다. Unity 컬러 입력에 그대로 복사해 같은 외관이 된다고 보장하지 않는다. FBX의 단순 재질 정의만 내보냈고 Unity `.mat`·프리팹·`.cs`는 만들지 않았다.

## 원점·축·단위와 향후 결합

목표 로컬 좌표는 미터, UI=XY 평면 Z=0, +Y=위, -Z=사용자, +Z=벽이다. 원점은 UI 중심 (0,0,0). Blender에서는 (X,Y,Z)=(목표 X,-목표 Z,목표 Y)로 제작해 +Z를 위쪽으로 사용했다.

FBX: global_scale=1, Apply Unit Scale=true, Apply Scalings=FBX Units Scale, Forward=-Z, Up=Y, Bake Space Transform=true, Mesh 선택 내보내기, 애니메이션=false. FBX 원시 데이터에서 노드 이동/회전=0, 배율=1과 요구 XYZ 메시 좌표를 확인했다. `UnitScaleFactor=100`은 1 좌표 단위=100cm=1m를 뜻하며 메시를 100배 키운 설정이 아니다. Blender 재반입 시 Y-up FBX를 Z-up Blender로 되돌리는 회전이 생기므로, 재반입 로컬 회전을 임의로 0으로 바꾸지 않고 **월드 변환을 적용한 실제 삼각형 좌표**를 원본과 비교했다. 내보낸 FBX 노드 변환은 별도 파서로 항등임을 검증했다.

향후 구조 기준은 **화면과 외형을 미터 단위 공통 부모 아래 형제로 구성**하는 방식이다. 공통 부모 배율=1, UI 중심을 공통 부모의 원점에 맞추고, 기존 Canvas의 배율 0.001은 유지한다. 외형 모델은 형제 자식으로 로컬 위치/회전=0, 배율=1로 둔다. Canvas 자식으로 배율 1인 모델을 그대로 넣으면 Canvas 배율을 상속하여 1/1000 크기가 되므로 이 구성을 기본으로 삼지 않는다. 기존 Canvas의 위치·회전·배율·계층을 이번 제작에서 변경하지 않았다. 실제 공통 부모 구성과 벽 배치는 후속 사용자 적용 작업이다.

## 검사 결과

1. **7개 파일/규격/부품:** 모두 실제 FBX로 내보내고 별도 Blender 씬에 재반입했다. 파일당 4개 Mesh와 2개 Material 정의만 있다. 부품 이름·수·삼각형 수·재질 수가 원본과 일치한다. 각 규격 916삼각형으로 1,500 목표 이내다.
2. **정확한 화면 사각형 비침범:** 공칭 폭/높이만 비교하지 않았다. 모든 삼각형을 정확한 화면 직사각형과 Z≤0 영역으로 클리핑한 결과 교차 0개. 안쪽 둥근 모서리와 베벨도 포함했다. 원본과 재반입 모두 0개다.
3. **얕은 앞턱/사선 여유:** 앞쪽 구간의 화면 사각형을 정면 법선에서 60°까지의 각도에 해당하는 보수적 확장 영역으로 만들고 모든 삼각형과 검사했다. 원본·재반입 모두 교차 0개다. 이는 외형의 기하학적 가림 검사이며 Unity UI 가독성이나 VR 시야 검증은 아니다. 60°보다 큰 각도는 이 완료 항목에 포함하지 않는다.
4. **형상/배정 비교:** 전체 삼각형의 월드 좌표·부품명·삼각형별 재질명을 정렬해 0.01mm 정밀도로 대조했다. 모든 규격에서 동일하다. 재반입한 기본색·Metallic·Roughness도 원본과 허용오차 0.00001 이내다. 전체 외곽·원점·배율·FBX 원시 좌표와 단위도 대조했다.
5. **닫힌 메시/법선:** 원본·재반입에서 열린 경계, 비다양체 모서리, 느슨한 정점, 중복 정점/면 0개. 모든 부품은 양의 signed volume이고, 바깥 방향 법선 재계산으로 바뀌는 면 0개다. 부품 간 중복 면 0개, 양의 부피를 갖는 AABB 겹침 0개다. 부품은 닫힌 입체로 맞대어 연결하며 독립 부품의 접촉 캡은 유지했다. FrameCase-Backplate 및 Backplate-WallBracket 접촉으로 떠 있는 부품은 없다.
6. **불필요한 포함물 없음:** FBX 원시 Objects는 Geometry 4 / Model 4 / Material 2뿐이다. UI·화면 평면·유리·검은 스크린·가이드·Collider·Rigidbody·카메라·조명·아마추어·애니메이션·텍스처가 없다. 원본 blend도 메시 28개와 실제 사용 재질 2개만 남겼다.
7. **시각 확인:** 대표 AED 정면·측면·후면·사선, 추가 후면 사선, 7규격 동일 축척 정면 비교를 실제 Blender 렌더로 확인했다. 청회색 면은 기존 Canvas의 위치를 설명하는 임시 가이드다. 이미지에 이를 명시했으며 납품 FBX/원본 blend에는 저장하지 않았다. 실제 UI가 적용된 Unity 화면이 아니다.

상세 수치/검사값은 `validation.json`이다. 미리보기는 `Previews/Representative_4Views.png`, `Previews/All_7_Front_Comparison.png`, 개별 PNG에 있다. 제작·검사·미리보기용 Python은 모두 이 Source~ 내부에만 있다.

## 보존과 미검증 항목

기준 기록의 Git 추적 파일 및 ignore되지 않은 미추적 기존 파일 4,339개를 크기/수정시각(ns)으로 대조한다. 허용된 TrainingDisplay 폴더와 그 동반 `TrainingDisplay.meta`만 검사 제외 경로다. 최종 결과는 `workspace_after.json`에 남긴다. 읽은 씬·코드·거울 자료와 납품 바이너리의 SHA-256은 `input_and_delivery_hashes.json`에 남긴다. 원래 존재하던 B_CBRN 거울 배치·거울 자산·글꼴·문서 변경도 그대로 보존한다. 기존 파일을 삭제하거나 복구하지 않는다. 폴더용 메타는 없을 때만 생성하고, 이미 있으면 유지한다.

Unity 실행·Play·Simulator·batchmode·테스트·빌드·씬 배치·커밋·Push는 수행하지 않았다. 기존 씬·UI·거울·책상·코드·재질·Packages·ProjectSettings·A/C 자산은 수정하지 않았다. **실제 Unity 임포트 결과, UI 가독성, 버튼 입력, 벽 배치, 몸/손 간섭은 검증하지 않았고 완료 항목으로 보고하지 않는다.**

후속 사용자 확인: (1) 실제 임포트 크기·방향·원점 확인 → (2) 공통 미터 부모 아래 Canvas/외형을 형제로 결합 → (3) Z=+0.070 벽 접촉면 기준으로 배치 → (4) 시작/실습/최종 확인/완료 화면·재질·버튼 입력 확인 → (5) 정면/사선 가독성과 몸·손·주변 물체 간섭 확인. UI가 가려지거나 입력이 막히면 배치/재질/계층을 확인하고 사용을 확정한다.

## 재현

Blender background에서 `build_training_display.py`, `render_previews.py` 순으로 실행한다. 일반 Python+Pillow로 `label_previews.py`, `write_delivery_record.py`를 실행한다. `inspect_saved_ui.py`는 Python+PyYAML로 저장 씬을 읽기만 한다. `workspace_audit.py before`는 최초 기준 파일을 덮어쓰지 않으며, `after`로 보존 여부를 대조한다. 이 스크립트들은 Unity를 호출하지 않는다.
'''
(SRC/'전달기록.md').write_text(text,encoding='utf-8')
inputs=[ROOT/'Docs/B/AGENTS.md',ROOT/'Assets/B/Scene/B_FirstAid.unity',ROOT/'Assets/B/Scene/B_CBRN.unity',ROOT/'Assets/B/Editor/Decontamination/KD1/KD1EquipmentSetup.Tutorial.cs',ROOT/'Assets/B/Models/TrainingMirror/Source~/전달기록.md',ROOT/'Assets/B/Models/TrainingMirror/Source~/Previews/K1_Overview.png']
inputs+=list((ROOT/'Assets/B/Editor/TrainingUI').glob('*.cs'))
deliveries=list(BASE.glob('*.fbx'))+[SRC/'TrainingDisplay.blend']
hashes={'inputs':{p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs},'deliveries':{p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in deliveries}}
(SRC/'input_and_delivery_hashes.json').write_text(json.dumps(hashes,ensure_ascii=False,indent=2),encoding='utf-8')
print('Delivery record, input/delivery hashes and authorized folder metadata prepared.')
