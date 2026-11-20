"""Update latest export records while retaining h5 measurements and past export results."""
from pathlib import Path
import json,hashlib,re
S=Path(__file__).resolve().parent;P=S.parent;ROOT=P.parents[3];W=S/'WornNodeFix'
def read(p):return json.loads(p.read_text(encoding='utf8'))
def write(p,r):p.write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
r=read(W/'WornNodeFixReport.json');repro=read(W/'ReproductionCheck.json')
assert r['complete'] and repro['passed']
assert all(sha(ROOT/p)==x['sha256'] for p,x in r['artifacts'].items())
before=r['before'];after=r['after'];imp=r['final_import']
start=read(W/'ProtectedScopeStartHashes.json') if False else json.loads((W/'ProtectedScopeStartHashes.json').read_text(encoding='utf-8-sig'))
external=[]
for x in start:
    p=Path(x['Path']);end=sha(p) if p.exists() else None
    if end!=x['Hash'].lower():external.append({'path':str(p),'start':x['Hash'].lower(),'end':end,'action':'preserved without restoration; cause unconfirmed'})
r['protected_scope_observations']={'files_checked':len(start),'changes':external,'restored_files':[]}
r['exporter_used_source']={'path':'WornNodeFix/ExporterSourceAtDelivery.py.txt','sha256':sha(W/'ExporterSourceAtDelivery.py.txt')}
r['reproduction_check']={'path':'WornNodeFix/ReproductionCheck.json','passed':True,'delivered_files_unchanged':repro['delivered_files_unchanged']}
r['meta']={'guid':re.search(r'^guid:\s*(\S+)',(P/'ProtectiveClothing_Worn.fbx.meta').read_text(),re.M).group(1),
    'animationType':2,'start_and_end_sha256':sha(P/'ProtectiveClothing_Worn.fbx.meta'),'unchanged_from_start':True}
assert r['meta']['start_and_end_sha256']==r['start_hashes'][str((P/'ProtectiveClothing_Worn.fbx.meta').relative_to(ROOT))]
write(W/'WornNodeFixReport.json',r)
latest={str((ROOT/p).relative_to(P)):x['sha256'] for p,x in r['artifacts'].items()}
for filename,artifact_key,roundtrip_key in [('JacketDeformFixReport.json','artifact_sha256','roundtrip'),('JacketFinalReview.json','artifacts','roundtrip_full')]:
    p=S/filename;old=read(p)
    assert 'worn_node_fix_followup' not in old,'Already updated; review new edits before repeating.'
    old[artifact_key+'_before_node_fix']=old[artifact_key]
    old[roundtrip_key+'_before_node_fix']=old[roundtrip_key]
    old[artifact_key]=latest;old[roundtrip_key]=imp
    old['worn_node_fix_followup']={'report':'WornNodeFix/WornNodeFixReport.json','raw_fbx_node_bind_attach_passed':True,
        'source_geometry_weights_bones_uv_materials_unchanged':True,'historical_export_results':'Fields ending _before_node_fix are historical. Current raw FBX proof is in the follow-up report.',
        'manifest':'WornNodeFix/DeliveryManifest.json','reproduce_check':'worn_node_fix.py -- check'}
    if 'completion' in old:old['completion']['raw_fbx_mesh_bind_attach_contract_passed']=True
    write(p,old)

depth_before=max(x['position_error_mm'] for x in before['attach'].values())
bindmax=max(x['node_vs_skin_bind_max'] for x in after['meshes'].values())
nodeerr=max(x['root_relative_identity_error'] for x in after['meshes'].values())
import_attach=max(x['position_error_mm'] for x in imp['attach'].values())
restmax=max(imp['poses']['Rest'].values());posemax=max(v for p,vals in imp['poses'].items() if p!='Rest' for v in vals.values())
lines=['## 최신: Worn FBX 메시 원점·Attach 수정 완료 (2026-11-04)', '',
    '**기존 FAIL #1·#5의 원시 FBX 결함을 수정했다. 이 절과 `WornNodeFix/WornNodeFixReport.json`이 아래 내보내기·재수입 이력보다 우선한다.** h5 형상·스키닝은 그대로이며, Unity 검사 메뉴의 실제 PASS는 사용자 실행 미확인이다.', '',
    '작업 시작 시 원본·Worn/Pickup FBX·두 FBX의 `.meta`·최상위 검사/재현 자료와 README를 `Backups/20261104_161356_worn_nodes_before/`에 새로 보존했다(`BackupSHA256.json`). 기존 백업은 덮어쓰지 않았다. 시작 상태의 Worn `.meta`는 Generic(2), GUID `'+r['meta']['guid']+'`였다. `.meta`에는 쓰기 작업을 하지 않았고 시작/종료 SHA256이 같다. 과거 None(0) 해시로 복구하지 않았다.', '',
    '### 원인과 실제 내보내기 변경', '',
    'Blender 5.2.1의 `io_scene_fbx` 로컬 코드를 확인했다. `fbx_skeleton_from_armature`가 리그로 변형되는 모든 메시를 `parented_to_armature=True`로 표시하고, `ObjectWrapper.fbx_object_matrix`는 이 표식을 전역 좌표 출력으로 처리한다. 반면 이 모델은 메시의 실제 부모가 Armature가 아닌 `ProtectiveClothing_Worn`이며, exporter의 OO 연결은 그 루트 부모를 유지한다. 그 결과 전역 축 변환이 들어간 값을 루트 자식의 Lcl로 다시 써 축 변환이 중복됐다.', '',
    '`worn_node_fix.py`의 `export()`는 이번 내보내기 호출 동안만 `ObjectWrapper.fbx_object_tx`를 감싼다. **루트 자식인 Worn 스킨 메시 6개에 한해** `실제 부모의 FBX 전역 행렬 역행렬 × 메시 FBX 전역 행렬`로 Model Lcl을 계산한다. 호출 뒤 원래 함수를 복원하며 Blender 설치 파일·애드온은 수정하지 않는다. 스킨 클러스터와 BindPose에서 쓰는 전역 행렬 계산은 변경하지 않았다.', '',
    '루트의 정상 축 변환 R(−90°,0,0), Armature의 R(+90°,0,0)·S(0.01,0.01,0.01), UnitScaleFactor=100·Y-up은 유지했다. 모든 노드 회전을 0으로 만든 것이 아니다. 원본의 메시·본·Attach 행렬이나 부모는 바꾸지 않았다. 원본 `.blend`에는 사용한 내보내기 방식과 SHA256을 담은 `Worn_FBX_Export_Contract.json` Text와 장면 설명만 추가했다.', '',
    '### #1·#5: importer를 거치지 않은 원시 FBX 검사', '',
    'Lcl T/R/S, OO 부모를 직접 읽어 전역 행렬을 구성했다. Pre/Post 회전·피벗 등의 비항등 추가 변환이 있으면 판독을 중단하도록 했다. 클러스터 행렬은 FBX 열 우선 배열로 읽고 `TransformLink × Transform`을 메시의 스킨 바인드 좌표계로 대조했다.', '',
    '| 검사 | 수정 전 | 최종 | 허용 기준 |','|---|---|---|---|',
    f'| Worn 6종 루트 기준 노드 | 추가 −90° X, 행렬 최대 차이 1 | T=0, R=0, S=1; 행렬 차이 {nodeerr:.9g} | 원소 ≤1e-5 |',
    f'| 메시 전역 vs 스킨 바인드 | 최대 {max(x["node_vs_skin_bind_max"] for x in before["meshes"].values()):.9g} | 최대 {bindmax:.9g} | 원소 ≤1e-5 |',
    f'| Attach 문서 대비 최대 위치 오차 | {depth_before:.6f}mm | 0mm | ≤1mm |',
    '| 이름·OO 부모 | 루트·메시·Attach·65본 | 동일(79 Model 노드) | 정확 일치 |', '',
    '| Attach | 기존 오차(mm) | 최종 원시 FBX(mm) | 원시 Blender 재수입(mm) |','|---|---:|---:|---:|']
for n,x in after['attach'].items():lines.append(f'| {n} | {before["attach"][n]["position_error_mm"]:.6f} | {x["position_error_mm"]:.6f} | {imp["attach"][n]["position_error_mm"]:.9f} |')
lines+=['', 'Attach는 모두 해당 메시의 자식이며, AnchorManifest의 좌표와 공통 모델 공간에서 일치한다. `AnchorManifest.json`을 수정하지 않았고 해시도 유지했다. 후보 파일에서 먼저 통과한 뒤 최종 경로로 내보내고 동일 검사를 다시 수행했다. #1·#5 해결 근거는 **원시 파일 자체의 노드·바인드·Attach 검사**이며 검사 장면 정규화가 아니다.', '',
    '### h5 보존 및 원시 재수입', '',
    '- 저장된 원본의 모든 메시 위치·간선·면·가중치·UV·면별 재질, 재질 노드/연결·이미지 경로/내장 텍스처, 모든 오브젝트의 이름/부모/행렬/표시 상태, 양쪽 리그의 본 이름/부모/rest/머리/꼬리가 시작 h5 계약과 정확히 같다. 다른 Worn 5종·Pickup·인체·원본 마네킹을 바꾸지 않았다.',
    '- 기준 자세와 기존 6자세에서 저장된 원본의 최대 정점 차이: **0m**. 원시 FBX Geometry 전체 배열(정점·면·법선·UV 등), 본 65개, 클러스터 390개의 인덱스·가중치·바인드 행렬 지문은 수정 전 h5와 동일하다. 스킨과 형상을 바꿔 좌표 문제를 숨기지 않았다.',
    f'- 최종 FBX 원시 재수입: 오브젝트 14개, 본 65개, 누락·추가·본 부모 차이 0. 본 월드 rest 행렬 최대 차이 {imp["bone_rest_world_matrix_max"]:.9g}; 기준 자세 정점 최대 {restmax:.9g}m; 6자세 변형 최대 {posemax:.9g}m. 허용치 각각 1e-5, 1e-5m. 가중치·UV·재질 이름·면별 재질 배정은 동일하다.',
    f'- 원시 재수입 Attach 최대 오차는 {import_attach:.9f}mm(허용 1mm)이고 부모는 해당 메시로 유지된다.', '',
    '| 자세 | 원시 재수입 vs h5 최대 정점 차이(m) |','|---|---:|']
ko={'Rest':'기준 자세','ArmsDown':'팔 내림','ElbowBend':'팔꿈치 굽힘','HandToChest':'손 가슴','HandToWaist':'손 허리','TorsoTwist':'몸통 회전','LightBend':'가벼운 굽힘'}
for name,vals in imp['poses'].items():lines.append(f'| {ko[name]} | {max(vals.values()):.9g} |')
lines+=['', '**Blender importer의 표현 차이는 별도 기록했다.** 수정된 FBX도 importer가 스킨 메시 6개를 PC_Armature 자식으로 재배치하고 로컬 축척 100으로 보상한다. 따라서 원시 Blender 재수입의 부모·로컬 행렬 직접 대조는 여전히 불일치(최대 99.9999771118)이며 통과로 표시하지 않는다. 원시 결과는 `FBX_Roundtrip_Review.blend`에 그대로 저장했다. 이때 월드 행렬·Attach·기준/6자세 변형은 이미 허용오차 안이다.', '',
    f'`WornNodeFix/Final_NormalizedReview.blend`는 별도 복사본에서 월드 행렬을 유지하면서 메시 부모만 원본 루트로 맞춘 검토 장면이다. 정규화 후 로컬/월드 행렬 최대 차이 {imp["normalized_local_matrix_max"]:.9g}/{imp["normalized_world_matrix_max"]:.9g}이며, 7개 자세도 다시 대조했다. 이 장면을 최종 FBX 결함 해결 근거로 사용하지 않았다. 이전 h5의 정규화 결과만으로 원시 파일이 정상이라는 해석은 이 최신 검사로 대체한다.', '',
    '### 산출물·해시·재현', '',
    '| 파일(Source~ 기준) | SHA256 |','|---|---|']
for name in ['ProtectiveClothing.blend','../ProtectiveClothing_Worn.fbx','FBX_Roundtrip_Review.blend','WornNodeFix/Final_NormalizedReview.blend']:
    p=(S/name).resolve();lines.append(f'| [{name}]({name}) | `{sha(p)}` |')
lines+=['', f'Worn `.meta` SHA256: `{r["meta"]["start_and_end_sha256"]}` (Generic 2·GUID 유지).', '',
    '검사 전체 수치/원시 Lcl·부모·본·Attach: [WornNodeFixReport.json](WornNodeFix/WornNodeFixReport.json). 기준 데이터: [H5SourceContract.json](WornNodeFix/H5SourceContract.json), `H5PoseReference.npz`. 최종 파일·검사·README 해시: [DeliveryManifest.json](WornNodeFix/DeliveryManifest.json). `JacketDeformFixReport.json`과 `JacketFinalReview.json`은 h5 측정을 유지하면서 최신 내보내기 기록을 연결했고, 이전 내보내기 결과는 `_before_node_fix` 필드와 시작 백업에 남겼다. 기존 `JacketFinalDeliveryManifest.json`은 과거 h5 납품 이력이다.', '',
    '현재 최종 파일을 덮어쓰지 않고 재현 검사하려면 프로젝트 루트에서 다음을 실행한다. 별도 `Reproduced_Worn.fbx`와 원시/정규화 장면, `ReproductionCheck.json`만 생성한다. 이 명령으로 재현 통과 및 최종 파일 해시 유지도 확인했다.', '',
    '```powershell', '& "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python "Assets/B/Models/ProtectiveClothing/Source~/worn_node_fix.py" -- check', '```', '',
    '최초 수정 순서는 `candidate` → `candidate_import` → `finalize`였다. 완료 후 이 세 모드는 기존 결과 덮어쓰기를 차단한다. 새 편집을 검토할 때는 새 백업·별도 작업 사본을 사용한다. 실제 최종 내보내기에 사용한 코드 원문과 SHA는 `WornNodeFix/ExporterSourceAtDelivery.py.txt` 및 보고서에 보존했고, 현재 `worn_node_fix.py`에는 비파괴 `check` 진입점이 추가돼 있다. 원본 Blender 파일 안의 Text는 실제 내보내기 시점 코드를 기록한 것이다. 일반 stock exporter로 이 계층을 그대로 다시 내보내면 같은 결함이 재발할 수 있으므로 이 전용 방식을 사용한다.', '',
    '### 범위와 남은 사항', '',
    'Unity C#·씬·Prefab·설정·공유 자산, Pickup FBX, 원본 마네킹은 수정하지 않았다. 보호 대상 시작 해시와 종료 해시의 차이는 보고서 `protected_scope_observations`에 기록하고 외부 변경이 있으면 복구하지 않고 보존한다. 이 작업에서 확인한 보호 대상 파일 '+str(len(start))+'개 중 외부 변경 관찰 '+str(len(external))+'개다.', '',
    'h5의 관통·늘어남·실루엣 제한은 그대로다(가벼운 굽힘 관통 최대 7.95mm, 팔꿈치 최대 2.395배·손 가슴 2.476배 늘어남 등). 이번 수정은 좌표계 결함만 다뤘다. **Unity 실행·테스트·빌드·커밋·Push는 하지 않았다. Unity 임포트·1인칭·거울·VR 품질은 사용자 실행 미확인이다.**', '',
    '아래는 수정 전 결함 및 h5 모델 작업 이력이다.', '']
section='\n'.join(lines)
readme=S/'README_Model.md';old=readme.read_text(encoding='utf8')
assert '## 최신: Worn FBX 메시 원점·Attach 수정 완료' not in old
title,rest=old.split('\n',1);readme.write_text(title+'\n\n'+section+'\n'+rest,encoding='utf8')
assert not [target for target in re.findall(r'\]\(([^)]+)\)',section) if not target.startswith('http') and not (S/target).exists() and target!='WornNodeFix/DeliveryManifest.json']
files=[S/'ProtectiveClothing.blend',P/'ProtectiveClothing_Worn.fbx',P/'ProtectiveClothing_Worn.fbx.meta',P/'ProtectiveClothing_Pickup.fbx',P/'ProtectiveClothing_Pickup.fbx.meta',S/'FBX_Roundtrip_Review.blend',
    S/'worn_node_fix.py',S/'finalize_worn_node_documentation.py',readme,S/'JacketDeformFixReport.json',S/'JacketFinalReview.json',S/'AnchorManifest.json']
files+=list(W.glob('*'))
manifest={'version':r['version'],'raw_fbx_fail_1_fixed':True,'raw_fbx_fail_5_fixed':True,'h5_source_contract_and_pose_preserved':True,
    'generic_meta_and_guid_preserved':True,'raw_blender_parent_local_direct_check_passed':False,'normalized_review_equivalent_contract_passed':True,
    'unity_user_execution':'미확인','files':{}}
for p in sorted(set(files)):
    if p.is_file() and p.name!='DeliveryManifest.json':manifest['files'][str(p.relative_to(ROOT))]={'sha256':sha(p),'bytes':p.stat().st_size}
write(W/'DeliveryManifest.json',manifest)
assert all(sha(ROOT/p)==x['sha256'] for p,x in manifest['files'].items())
print('WORN_NODE_DOCUMENTED',len(manifest['files']),'files; external changes observed',len(external))
