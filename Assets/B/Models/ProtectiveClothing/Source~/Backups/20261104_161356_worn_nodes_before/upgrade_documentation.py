from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,csv,hashlib,re
import numpy as np
S=Path(__file__).resolve().parent;P=S.parent;PROJECT=P.parents[3];B=Path((S/'UpgradeBackupPath.txt').read_text())
stats=json.loads((S/'UpgradeStatistics.json').read_text());v=json.loads((S/'UpgradeValidation.json').read_text());anchors=json.loads((S/'AnchorManifest.json').read_text())
assert v['contract']['passed'] and all(d['passed'] for d in v['roundtrip'].values())
assert all(d['unchanged'] for d in v['preservation'].values())
assert all(d['inside_over_1mm']==0 for d in v['fit'].values())
assert all(d['duplicate_faces_1um']==d['degenerate_faces']==d['nonmanifold_nonboundary_edges']==d['inward_closed_islands']==0 for d in v['health'].values())
ext=json.loads((B/'ExternalDependenciesBefore.json').read_text());external={}
for name,d in ext.items():
    f=Path(name);unchanged=f.exists() and hashlib.sha256(f.read_bytes()).hexdigest()==d['sha256']
    external[name]={'unchanged':unchanged,'intentional_image_update':d['intentional_image_update']}
    assert unchanged or d['intentional_image_update'],name
v['external_preservation']=external
folder_meta=P.parent/(P.name+'.meta')
assert hashlib.sha256(folder_meta.read_bytes()).digest()==hashlib.sha256((B/'ProtectiveClothing_folder.meta').read_bytes()).digest()
derived=PROJECT/'Assets/B/Materials/ProtectiveClothing/PC_MetallicSmoothness_1K.png'
rough=np.array(Image.open(P/'Textures/PC_Roughness_1K.png'));a=np.array(Image.open(derived))
assert np.all(a[:,:,:3]==0) and np.array_equal(a[:,:,3],255-rough)
v['derived_image']={'path':str(derived),'rgb_zero':True,'alpha_matches_1_minus_roughness':True,'size':list(Image.open(derived).size)}
(S/'UpgradeValidation.json').write_text(json.dumps(v,indent=2))
(S/'ValidationReport.json').write_text(json.dumps(v,indent=2))
canonical={}
for n,d in stats.items():
    h=v['health'][n]
    canonical[n]={'triangles':d['triangles'],'vertices':d['vertices'],'material_slots':d['material_slots'],'materials':['PC_Shared'],'bounds_m':d['bounds_after_m'],'boundary_edges':h['boundary_edges'],'nonmanifold_nonboundary_edges':h['nonmanifold_nonboundary_edges'],'degenerate_faces':h['degenerate_faces'],'negative_scale':h['negative_scale'],'triangles_before':d['before_triangles'],'bounds_before_m':d['bounds_before_m']}
(S/'MeshStatistics.json').write_text(json.dumps(canonical,indent=2))
keys=['Jacket','Trousers','Glove_L','Glove_R','Overboot_L','Overboot_R']
def xyz(x):return '('+', '.join(f'{z:.5f}' for z in x)+')'
def center(b):return [(a+c)/2 for a,c in b]
def size(b):return [c-a for a,c in b]
with (S/'BoundsComparison.csv').open('w',newline='',encoding='utf-8-sig') as f:
    w=csv.writer(f);w.writerow(['Object','State','CoordinateSpace']+[stage+key+axis for stage in ['Before','After'] for key in ['Min','Max','Center','Size'] for axis in ['X','Y','Z']])
    for n,d in stats.items():
        row=[n,n.split('_')[-1],'Blender_local_m_object_transform_unchanged']
        for field in ['bounds_before_m','bounds_after_m']:
            b=d[field];row.extend([t[0] for t in b]+[t[1] for t in b]+center(b)+size(b))
        w.writerow(row)
# Read existing BoxColliders without starting Unity. Their old dimensions confirm
# the Blender XYZ -> Unity wrapper XZY axis permutation for these centered props.
scene_text=(PROJECT/'Assets/B/Scene/B_ProtectiveClothing_Equipment.unity').read_text(encoding='utf-8')
blocks=re.split(r'(?m)^--- !u!',scene_text)[1:];gonames={};cols=[]
for b in blocks:
    m=re.match(r'(\d+) &(\S+)',b)
    if not m:continue
    typ,oid=m.groups()
    if typ=='1':
        nm=re.search(r'^  m_Name: (.*)$',b,re.M)
        if nm:gonames[oid]=nm.group(1)
    elif typ=='65':
        go=re.search(r'm_GameObject: \{fileID: (\d+)',b);cc=re.search(r'm_Center: (.*)',b);ss=re.search(r'm_Size: (.*)',b)
        if go and cc and ss:
            parse=lambda x:[float(t) for t in re.findall(r'[xyz]: ([\d.eE+\-]+)',x)]
            cols.append((go.group(1),parse(cc.group(1)),parse(ss.group(1))))
colliders={}
for k in keys:
    name='PC_'+k+'_Pickup';old=next((c,s) for g,c,s in cols if gonames[g]==name)
    d=stats[k+'_Pickup'];bl=d['size_before_m'];mapped=[bl[0],bl[2],bl[1]]
    assert max(abs(x-y) for x,y in zip(mapped,old[1]))<.00001,(name,mapped,old)
    cb=center(d['bounds_after_m']);sb=size(d['bounds_after_m']);newsize=[sb[0],sb[2],sb[1]];newcenter=[-cb[0],cb[2],-cb[1]]
    contains=all(abs(newcenter[i]-old[0][i])+newsize[i]/2<=old[1][i]/2+.00001 for i in range(3))
    colliders[name]={'existing_center_unity_wrapper_m':old[0],'existing_size_unity_wrapper_m':old[1],'recommended_center_unity_wrapper_m':newcenter,'recommended_size_unity_wrapper_m':newsize,'existing_contains_new_bounds':contains,'basis':'Static YAML comparison, old model bounds verified against existing BoxCollider. No Unity execution.'}
(S/'ColliderRecommendations.json').write_text(json.dumps(colliders,indent=2))
tot={state:(sum(s['before_triangles'] for n,s in stats.items() if n.endswith(state)),sum(s['triangles'] for n,s in stats.items() if n.endswith(state))) for state in ['Pickup','Worn']}
tri_table='\n'.join(f"| {k} | {stats[k+'_Pickup']['before_triangles']:,} → {stats[k+'_Pickup']['triangles']:,} | {stats[k+'_Worn']['before_triangles']:,} → {stats[k+'_Worn']['triangles']:,} | 1 → 1 |" for k in keys)
bound_table='\n'.join(f"| {n} | {xyz(d['size_before_m'])} | {xyz(d['size_after_m'])} |" for n,d in stats.items())
collider_table='\n'.join(f"| {n} | {xyz(d['recommended_center_unity_wrapper_m'])} | {xyz(d['recommended_size_unity_wrapper_m'])} | {'기존 범위 포함' if d['existing_contains_new_bounds'] else '확대/중심 확인 필요'} |" for n,d in colliders.items())
fit_table='\n'.join(f"| {n} | {d['inside_over_1mm']} | {d['minimum_signed_m']*1000:.3f} |" for n,d in v['fit'].items())
maxgeo=max(d.get('max_vertex_error_m',0) for r in v['roundtrip'].values() for d in r['objects'].values());maxmat=max(d['max_matrix_error'] for r in v['roundtrip'].values() for d in r['objects'].values())
anchor_table='\n'.join(f"| {k} | {xyz([anchors['Grip_'+k+'_Pickup']['matrix_world_blender_m'][i][3] for i in range(3)])} | {xyz([anchors['Attach_'+k+'_Worn']['matrix_world_blender_m'][i][3] for i in range(3)])} |" for k in keys)
readme=f'''# 보호장비 모델 — 참고 이미지 기반 외형 개선

갱신일 2026-11-03, Blender 5.2.1 LTS. 특정 제품을 확정하거나 성능을 인증한 모델이 아닌 **교육용 개발 기본형**이다. 기존 고정 마네킹 기준 자세와 Unity 연결 계약을 유지하면서 외형과 이미지 텍스처를 개선했다.

## 결과 파일과 보존

- `ProtectiveClothing.blend`: 최신 제작 원본. Reference / Worn / Pickup / Review_Studio 컬렉션 유지.
- `../ProtectiveClothing_Pickup.fbx`, `../ProtectiveClothing_Worn.fbx`: 기존 경로에 재내보내기. 상태별 6개 메시.
- `../Textures/PC_BaseColor_2K.png`, `PC_Normal_1K.png`, `PC_Roughness_1K.png`: 기존 이름으로 실제 갱신.
- `../../../Materials/ProtectiveClothing/PC_MetallicSmoothness_1K.png`: 기존 Unity 머티리얼이 참조하는 파생 PNG도 갱신. RGB=0, A=1−Roughness. `.mat` 연결과 `.meta`는 변경하지 않았다.
- `Previews/`: 최신 필수 검토 렌더 12장. `Before/`는 동일 조명·시점·해상도로 다시 렌더한 이전 모델. `Comparison/`은 두 이미지를 나란히 배치한 비교 자료.
- `FBX_Roundtrip_Review.blend`: 별개 Blender 장면 Reimport_Worn / Reimport_Pickup.
- `UpgradeStatistics.json`, `MeshStatistics.json`, `BoundsComparison.csv`: 모델별 전후 메시 통계와 좌표 경계.
- `UpgradeValidation.json` / `ValidationReport.json`: 보존·재수입·UV·재질·이미지 경로·메시 검사 결과.
- `AnchorManifest.json`: **기존 파일 바이트 그대로 보존**. 새 측정값은 `ActualAnchorManifest_Upgrade.json`에 별도 기록.
- `ColliderRecommendations.json`: 현재 Unity 씬 YAML을 읽기만 해서 대조한 Pickup BoxCollider 권장치. Unity에서 적용하거나 실행 검증한 값은 아니다.

수정 전 결과 전체를 `Backups/{B.name}/`에 복사했다. 기존 FBX/PNG/문서/Blender와 `.meta`의 SHA256은 그 안의 `BackupManifest.json`에 있다. 열린 Blender의 기존 장면과 기본 Scene도 `LiveSession_before_upgrade.blend`로 별도 보존했다. 이전 전달 명세와 비교했을 때 저장 원본에는 추가 사용자 수정이 없었다. 저장 원본의 기존 39개 오브젝트 전체를 유지하고, 장비 메시 12개의 데이터만 교체했다. 추가 인체·마스크·헬멧·위장망·전투화 자체·조작 대상은 만들지 않았다.

## 참고 이미지 해석과 선택

첨부 원본은 `ReferenceImages/Reference_01.png`~`04.png`에 보존했다. 이미지에 포함된 글자는 작업 지시로 사용하지 않았다.

| 참고 | 반영한 특징과 선택 |
|---|---|
| 01 전체 착용 | 녹색·갈색·어두운 녹색·검은색의 삼림형 불규칙 무늬, 넉넉한 옷과 소매/무릎/허리 모임 |
| 02 장갑 확대 | 검은 고무색, 분리된 다섯 손가락, 손등·엄지 뿌리, 길고 넓은 커프. 04의 녹색으로 보이는 장갑보다 전문 확대 사진 02를 우선 |
| 03 덮개 확대 | 검은 고무, 넉넉한 앞코·발등·발목 통, 두꺼운 밑창 경계·입구·측면 겹침. 군화 끈은 추가하지 않음 |
| 04 분리/착용 | 보호의 전체 구성의 주된 기준. 올리브색 후드와 어깨 덮개, 앞여밈, 덮개 측면 고정 표현. 01의 헬멧·위장망은 제외 |

서로 다르게 보이는 장갑/덮개 색상은 02·03 확대 사진의 검은 고무로 통일했다. 후드와 어깨 덮개는 04의 분명한 올리브색으로 선택했다. 측면 고정은 사진 03·04에서 판독되는 큰 겹침과 짧은 띠 형태를 좌우 동일한 세 개의 고정 띠로 단순화했다. 실제 제품의 정확한 고정 방식·개수·치수·원단 규격을 확인한 복제는 아니며 기계적으로 작동하지 않는다. 제품명·브랜드·인증 문구는 넣지 않았다.

## 형상 수정

상의는 기존 맞춤 외피를 유지해 어깨/소매 연결을 부드럽게 하고 팔꿈치·겨드랑이·허리에 절제된 주름을 만들었다. 앞여밈은 몸통 곡면을 따르는 얇은 직물 덮개로 바꿨다. 주머니는 네모난 상자 대신 휘어진 천 포켓, 약한 부풀음, 얇은 덮개와 가장자리 봉제 구분으로 제작했다. 올리브색 후드·얼굴 테두리·어깨 덮개는 전부 **Jacket_Worn 한 메시** 안에 포함했다. 후드의 목 통과 공간을 열어 목 내부의 불필요한 막힘을 제거했다.

하의는 골반·사타구니·다리 표면을 완만하게 정리하고 넉넉한 바지통, 무릎의 굴곡, 발목 모임, 허리띠와 허벅지 포켓을 보완했다. 허리·손목·발목의 피부 노출과 표면 교차를 렌더로 확인하며 보정했다.

장갑은 원본 마네킹 손의 해부학적 정점/손가락 사이 구조를 기반으로 얇은 고무 외피를 다시 만들었다. 이전의 손가락이 뭉친 Voxel 형태를 제거하고 다섯 손가락·엄지와 검지 사이·손등을 분리해 읽을 수 있게 했다. 길고 넓은 커프가 소매를 감싸며 입구·말린 테두리·고무 접힘을 표현했다. 본인 좌우 손을 각각 원본의 해당 손에 맞췄다.

덮개는 발목에서 발등·앞코로 이어지는 단면을 늘려 원통/신발의 딱딱한 접합을 줄였다. 고무 발목 주름, 입구 테두리, 밑창 립, 측면 겹침과 고정 띠를 추가했다. 본인 좌우 발을 각각 감싼다. 눈에 띄는 베이지색 L/R 패치는 Worn/Pickup에서 모두 제거했다. 좌우 식별은 기존 이름과 손·발의 실제 좌우 형상으로 유지한다.

Pickup 상·하의도 같은 위장 직물로 갱신했다. 둥근 접힌 층, 얕은 상단 굴곡, 소매·포켓·접힌 올리브 후드 및 허리띠·다리 중앙 접힘으로 구별한다. 층 측면에는 별도 둘레 길이 UV를 사용해 두께 방향 무늬가 한 줄로 늘어나지 않게 했다. 장갑·덮개 Pickup에도 개선된 고무 형상을 동일하게 반영했다. 피벗·Grip·Attach는 외형 변경 후에도 이동하지 않았다.

## 텍스처와 공용 머티리얼

고유 장비 머티리얼 `PC_Shared` 1종, 메시마다 슬롯 1개를 유지했다. Blender 전용 Noise/시뮬레이션에 의존하지 않고 파일 이미지 기반 BaseColor/Roughness/Tangent Normal을 사용한다. Metallic은 0이다. 고무는 거칠기에 변화를 주어 검은색에서 형태가 읽히도록 했으며 금속 재질로 설정하지 않았다.

| 파일 | 변경 전 → 후 해상도 | 변경 내용 |
|---|---|---|
| PC_BaseColor_2K.png | 2048² → 2048² RGB | 단색 올리브/LR 패치 → 삼림 위장·올리브 후드/케이프·검은 고무 공용 아틀라스 |
| PC_Normal_1K.png | 1024² → 1024² RGB | 얕은 직물 결·봉제/고무 표면 변화 |
| PC_Roughness_1K.png | 1024² → 1024² L | 직물 약 .84~.91, 고무 약 .40~.52 수준의 표면별 거칠기 |
| PC_MetallicSmoothness_1K.png (기존 Materials 폴더) | 1024² → 1024² RGBA | 같은 최신 Roughness를 반전한 Alpha, RGB 전부 0. 기존 파일과 GUID 유지 |

아틀라스의 UV [0,.75]²는 위장 직물이고 오른쪽 영역은 올리브·고무·봉제용이다. 기본 직물 물리 밀도는 약 983px/m(2048 이미지, 1.5m당 UV .72)이며 몸통·소매·다리를 미터 단위 길이와 둘레로 배치했다. 소매와 바지의 실제 둘레 차이/모임에 따른 밀도 차이는 남지만 구성품마다 임의로 Smart UV를 꽉 채워 무늬 크기가 달라지는 방식은 피했다. 패널 경계는 봉제 방향에 놓고 방위각을 넘어가는 면의 UV 연결을 보정했다.

위장 원본은 **imagegen 스킬의 기본 내장 image_gen 도구**로 새로 생성했다. `ReferenceImages/Woodland_Generated_Master.png`가 생성 결과이고 사용 프롬프트 전체는 `ReferenceImages/ImageGen_Prompt.txt`에 보존했다. 원본 사진의 조명/그림자를 굽지 않았으며 생성 원본도 팔레트로 양자화해 명암을 제거했다. 이후 주기적인 미러 샘플링으로 경계를 연속화해 아틀라스에 배치하고 미세한 결·거칠기·탄젠트 노멀을 수치적으로 합성했다. 특정 제조사의 무늬를 정확하게 복제하거나 제품으로 인증한 것은 아니다.

## 기준 마네킹·좌표·계층 계약

기준은 `Assets/B/Models/Mannequin/Mannequin.fbx`를 애니메이션 없이 직접 불러온 원래 바인드 T 자세이다. 원본 Ch36와 65개 mixamorig1 본, 기준 변환·텍스처를 수정하지 않았다. 원본 월드 범위(m)는 X=-.88980~.88980, Y=-.16827~.11395, Z=.00411~1.76916이며 표준 신장으로 재조정하지 않았다. 원본 Armature의 가져오기 스케일 .01을 그대로 유지했다. 원본 FBX에 포함된 애니메이션 1프레임의 발 위치는 바인드 자세와 다르며 이번 Worn의 기준이 아니다.

Blender +Z 위 / -Y 전방 / +X 본인 왼쪽 / -X 본인 오른쪽. Metric / Unit Scale=1 / 장비 1unit=1m. Worn 공통 원점과 모든 메시·루트·기준점 오브젝트 변환을 그대로 유지했다. 별도 리그·IK·천 시뮬레이션·착의 애니메이션·새 조작 대상이 없다. 포켓·커프·후드·띠는 각각 기존 구성품 메시 안에 합쳐져 있다.

```text
ProtectiveClothing_Pickup
  <Component>_Pickup
    Grip_<Component>_Pickup
    Attach_<Component>_Pickup
ProtectiveClothing_Worn
  <Component>_Worn
    Attach_<Component>_Worn
```

Component=Jacket/Trousers/Glove_L/Glove_R/Overboot_L/Overboot_R. 기존 메시 오브젝트 이름과 Mesh 데이터 이름도 보존했다(`MeshNamePreservation.json`). Worn 6개, Pickup 6개, 루트/기준점 Empty 20개, 저장 원본 전체 39개 오브젝트의 이름·부모·변환 대조 통과. 기존 20개 루트/기준점의 최대 행렬 차이는 **{v['contract']['anchor_max_error']:.1f}**이며 `AnchorManifest.json` 자체도 수정하지 않았다. 기준점 축 의미는 그대로 +Z=위, -Y=전방이다.

| 구성품 | 기존 Pickup Grip XYZ(m) | 기존 Worn Attach XYZ(m) |
|---|---|---|
{anchor_table}

Worn 메시 자체를 Attach 위치로 다시 이동하면 정렬이 깨진다. 기존 연결의 공통 마네킹 공간 정렬과 숨김/표시 전환을 그대로 사용한다. 제작 파일에서는 원점의 Pickup이 겹치므로 해당 컬렉션을 숨겨 두었으며, 검토 렌더에서만 작업대 배치로 펼쳤다.

## 변경 전후 통계와 경계

명시적 삼각분할 후 계산. 참조 인체/스튜디오 제외. 각 메시의 머티리얼 수는 모두 1→1, 고유 공용 머티리얼도 1→1이다.

| 구성품 | Pickup 삼각형 전 → 후 | Worn 삼각형 전 → 후 | 머티리얼 수 |
|---|---:|---:|---:|
{tri_table}
| 합계 | {tot['Pickup'][0]:,} → {tot['Pickup'][1]:,} | {tot['Worn'][0]:,} → {tot['Worn'][1]:,} | 공유 1종 |

증가분은 상의의 후드/어깨 덮개·곡면 포켓·소매 실루엣, 하의의 무릎/발목 주름과 포켓, 장갑의 분리된 손가락/긴 커프, 덮개의 부드러운 발등/테두리/고정 띠, Pickup의 접힌 층/상단 굴곡에 사용했다. 기존 외피는 한 차례 국소 세분화/완화 후 Decimate해 실루엣을 조정했고 모든 제작 Modifier를 적용했다. 미세 직물 결은 메시 대신 이미지에 넣었다. 두께는 대부분 1.2~2.5mm이며 내부 전신 복제나 새로운 인체를 넣지 않았다. 개구부 안쪽과 두께 표현용 얇은 셸은 유지했다.

아래는 **Blender 로컬 XYZ 경계의 크기(m)**이며 피벗/오브젝트 변환은 동일하다. 정확한 Min/Max/Center/Size 전후 값은 `BoundsComparison.csv`의 12개 행 참조.

| 메시 | 이전 XYZ 크기(m) | 수정 XYZ 크기(m) |
|---|---|---|
{bound_table}

상의 Worn의 높이 증가는 후드 추가 때문이다. Pickup 상·하의의 작업대 범위는 기존과 거의 같고 장갑은 길이가 약 24.5~24.7mm 증가했다. 덮개는 깊이가 약 4.9mm, 높이가 약 2mm 증가했다. 외형을 콜라이더에 억지로 줄이지 않았다.

## Unity에 전달할 확인/조정 사항

Unity를 시작하거나 Editor API/메뉴/Play/Simulator/테스트/빌드를 실행하지 않았다. 현재 씬·설정 코드와 `.mat`를 파일로 읽기만 했다. 코드·씬·Prefab·`.mat` 연결·Importer `.meta`는 수정하지 않았다. 기존 FBX 및 PNG의 `.meta`와 상위 폴더 GUID도 그대로이다.

기존 `PC_Shared.mat`는 BaseColor와 Normal을 같은 GUID로 참조하므로 연결을 바꿀 필요가 없도록 파일을 갱신했다. URP의 MetallicGloss Alpha용 기존 파생 이미지도 위처럼 갱신했으므로, Unity 작업자는 기존 FBX 2개/이미지 4개의 재임포트 및 머티리얼 표시 갱신을 확인하면 된다. FBX의 기존 materialImportMode는 0이므로 새 FBX에서 별도 머티리얼을 추출하는 절차를 요구하지 않는다. 실제 Unity 화면과 VR 반사는 아직 확인하지 않았다.

씬 `B_ProtectiveClothing_Equipment.unity`의 기존 BoxCollider 값과 이전 모델 경계를 정적으로 대조하여, 각 Pickup wrapper의 크기 축이 Blender X,Z,Y 순서임을 확인했다. 아래는 **기존 Unity wrapper 로컬 XYZ**에서의 새 메시 경계를 포함하는 최소 Center/Size 권장값이다. 여유 패딩은 포함하지 않았다. 기존 Collider가 새 경계를 포함하는 상·하의는 그대로 둘 수 있다. 장갑·덮개는 확인/갱신이 필요하다.

| 기존 wrapper | 권장 Center XYZ(m) | 권장 Size XYZ(m) | 판정 |
|---|---|---|---|
{collider_table}

현재 구성 코드는 이미 존재하는 wrapper의 BoxCollider Center/Size를 자동으로 다시 맞추지 않는다. 따라서 FBX 재임포트만으로 기존 콜라이더가 새 메시를 모두 감싼다고 가정하지 말아야 한다. 위 정적 대조값/`ColliderRecommendations.json`을 기준으로 Unity 담당자가 확인할 항목이며 본 작업에서는 씬이나 연결부를 직접 수정하지 않았다. 잡기점·SNAP 기준점·거리 판정·착용 순서는 이동하거나 변경하지 않았다.

## Blender 검토와 FBX 재수입

전체 정면·후면·본인 좌우·사선, 후드/목/어깨 확대, 양쪽 장갑/손목(다섯 손가락이 읽히는 위쪽 시점), 양쪽 덮개/발목, Pickup 배치, 구성품 분리 렌더를 저장했다. 이전 모델도 같은 카메라/조명/배치/샘플/색관리로 다시 렌더해 나란히 비교했다. K1은 렌더에 포함하지 않았다.

Mesh/Empty만 선택해 기존 경로에 FBX Binary를 재내보냈다. Global Scale=1, apply_unit_scale=True, FBX_SCALE_UNITS, Forward=-Z, Up=Y, use_space_transform=True, bake_space_transform=False, 애니메이션/leaf bone/텍스처 임베드 제외, 이미지 상대 경로. FBX UnitScaleFactor=100은 단위당 100cm=1m라는 메타데이터이며 Transform 100배 보정 지시가 아니다.

별개 새 Blender 장면에 재수입하여 Worn 13개/Pickup 19개(각 메시 6개)의 이름·부모·행렬·피벗·기준점·정점 위치·삼각형 수·UV 값·머티리얼 슬롯·이미지 경로를 대조했다. 모두 통과. 양방향 최근접 정점 최대 오차 **{maxgeo:.9g}m**, 행렬 원소 최대 차이 **{maxmat:.9g}**, 허용값 1e-5. 참조 마네킹·리그·카메라·조명·바닥은 FBX에 없다. 원본 인체와 장비의 공통 공간 정렬도 보존했다.

음수 스케일·퇴화 면·1µm 기준 중복 면·경계 이외 비다양체 모서리·뒤집힌 닫힌 메시 섬은 모두 0이다. 봉제 리본은 단면 표면 디테일이므로 일부 열린 경계를 가진다. 의도하지 않은 외피 구멍과 혼동하지 않았다. 실제 시간에 따른 z-fighting 검사는 Unity/VR에서 하지 않았으나 Blender 시점 검토에서 피부 노출·소매/고무 커프 교차·발목 교차를 보정했다.

모든 Worn 정점을 원본 인체 최근접 표면 법선과 대조한 보조 검사(곡면 전체의 교차를 수학적으로 증명하는 검사는 아님):

| 메시 | 1mm 초과 인체 안쪽 표본 | 최소 부호 거리(mm) |
|---|---:|---:|
{fit_table}

## 남은 제한

- **K1 병용 목·얼굴 충돌은 미확인.** 원본 K1은 수정하지 않았고 실제 착용 위치/끈 포즈를 재현하지 않았다. 후드 얼굴 구멍을 열어 두고 마스크 전방 공간을 고려했으나 K1 조합 통과로 판정하지 않는다.
- 원본 바인드 고정 T 자세 전용. 움직이는 MirrorAvatar·다른 본 포즈·다른 체형·클립 1프레임의 착용 정렬은 미확인이다.
- 제품 실측·섬유 구조·실제 보호성능·실제 고정장치 작동을 재현한 인증 모델이 아니다. 포켓/끈/여밈을 직접 조작하지 않는다.
- Unity의 실제 Grab/SNAP/표시 전환·재임포트 후 파일 참조·렌더·그림자·콜라이더 및 Quest 기기 성능은 실행 검증하지 않았다. **Quest 최적화 완료라고 기록하지 않는다.** 삼각형은 위와 같이 증가했으며, 기기 측정 후 필요하면 LOD/내부 면 추가 정리를 검토할 수 있다.
- 스테이징/커밋을 하지 않았다. 원본 마네킹·K1, Unity 코드·씬·Prefab 및 기존 메타파일을 보존했다.

## 검토 이미지와 재현

[전체 검토 시트](Previews/Review_ContactSheet.jpg), [전후 비교 시트](Previews/Comparison/Before_After_Overview.jpg).
개별 파일: Worn_Front/Back/Left/Right/ThreeQuarter, Hood_Close, Wrist_L/R, Ankle_L/R, Pickup_All, Components_Separated. 비교 이미지는 `Previews/Comparison/`에 있다.

최신 재현 순서는 `upgrade_textures.py` → `upgrade_material_image.py` → `upgrade_model.py` → `upgrade_validate.py` → `upgrade_render.py`(이전 비교는 `-- before`) → `upgrade_documentation.py`이다. **이 스크립트는 백업을 입력으로 읽고 최신 결과를 덮어쓰므로 사용자 추가 편집 후에는 별도 복사본에서만 실행한다.** `build_protective_clothing.py`/`make_textures.py`/`finalize_documentation.py`는 이전 단색 기본형의 기록이며 현재 모델을 다시 만드는 스크립트가 아니다.
'''
(S/'README_Model.md').write_text(readme,encoding='utf-8')
# Assemble review comparisons from actual Blender renders, never substitute generated images.
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',25)
names=['Worn_Front','Worn_Back','Worn_Left','Worn_Right','Worn_ThreeQuarter','Hood_Close','Wrist_L','Wrist_R','Ankle_L','Ankle_R','Pickup_All','Components_Separated']
sheet=Image.new('RGB',(1600,1410),(25,30,34));dr=ImageDraw.Draw(sheet)
for i,n in enumerate(names):
    im=Image.open(S/'Previews'/(n+'.png')).convert('RGB');im.thumbnail((390,390));xx=(i%4)*400;yy=(i//4)*470
    sheet.paste(im,(xx+5,yy+43));dr.text((xx+9,yy+8),n,font=font,fill=(226,229,223))
sheet.save(S/'Previews/Review_ContactSheet.jpg',quality=92)
comp=S/'Previews/Comparison';comp.mkdir(exist_ok=True)
for n in names:
    canvas=Image.new('RGB',(1600,852),(25,30,34));d=ImageDraw.Draw(canvas)
    for idx,(label,path) in enumerate([('BEFORE',S/'Previews/Before'/(n+'.png')),('AFTER',S/'Previews'/(n+'.png'))]):
        im=Image.open(path).convert('RGB').resize((800,800),Image.Resampling.LANCZOS);canvas.paste(im,(idx*800,52));d.text((idx*800+20,13),label+' - '+n,font=font,fill=(232,234,228))
    canvas.save(comp/(n+'_Before_After.jpg'),quality=92)
overview=Image.new('RGB',(1600,2140),(25,30,34))
for row,n in enumerate(['Worn_ThreeQuarter','Wrist_L','Pickup_All']):
    im=Image.open(comp/(n+'_Before_After.jpg'));im.thumbnail((1600,710));overview.paste(im,((1600-im.width)//2,row*710))
overview.save(comp/'Before_After_Overview.jpg',quality=93)
manifest={}
for f in P.rglob('*'):
    if not f.is_file() or any(x in f.relative_to(P).parts for x in ['Backups','Iteration01']):continue
    if f.suffix not in {'.blend','.fbx','.png','.jpg','.md','.json','.py','.csv','.txt'} or f.name=='DeliveryManifest.json':continue
    manifest[str(f.relative_to(P)).replace('\\','/')]={'bytes':f.stat().st_size,'sha256':hashlib.sha256(f.read_bytes()).hexdigest()}
manifest[str(derived.relative_to(PROJECT)).replace('\\','/')]={'bytes':derived.stat().st_size,'sha256':hashlib.sha256(derived.read_bytes()).hexdigest(),'relative_to':'project_root'}
(S/'DeliveryManifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print(json.dumps({'triangle_totals_before_after':tot,'geometry_error_m':maxgeo,'matrix_error':maxmat,'contract':v['contract']['passed'],'unchanged_meta_and_originals':True,'external_unexpected_changes':False,'comparison_pairs':len(names),'manifest_files':len(manifest)}))
