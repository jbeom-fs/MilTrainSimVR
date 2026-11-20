"""Finish supplemental images, refresh hashes, and verify the delivered records."""
from pathlib import Path
import json,hashlib,re
from PIL import Image,ImageDraw
S=Path(__file__).resolve().parent;P=S.parent;ROOT=P.parents[3]
W=S/'Previews/JacketDeformFix/FinalReview'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
A=json.loads((S/'JacketFinalReview.json').read_text(encoding='utf8'))
R=json.loads((S/'JacketDeformFixReport.json').read_text(encoding='utf8'))
C=json.loads((S/'JacketContractAudit.json').read_text(encoding='utf8'))
M=json.loads((S/'JacketMaterialAudit.json').read_text(encoding='utf8'))
assert A['decision']=='h5_local_clearance_3mm' and A['roundtrip_full']['passed'] and C['passed'] and M['passed']
for rel,h in A['artifacts'].items():assert sha(P/rel)==h,(rel,'artifact drift')
assert A['raw_blender_import']['passed'] is False
assert A['raw_fbx_node_contract']['passed'] is True
for rel,h in A['protected_files_start'].items():assert sha(P/rel)==h,(rel,'protected drift')
for p in S.glob('*.py'):compile(p.read_text(encoding='utf8'),str(p),'exec')
extra=[]
for side in ['Left','Right']:
    name='ElbowBend_BackElbow'+side
    imgs=[Image.open(W/(prefix+'_'+name+'.png')).convert('RGB') for prefix in ['Baseline','H4','Final']]
    w,h=imgs[0].size;sheet=Image.new('RGB',(3*w,h+32),'#18212b');d=ImageDraw.Draw(sheet)
    for col,(im,label) in enumerate(zip(imgs,['Earlier FBX','h4','Final h5'])):
        sheet.paste(im,(w*col,32));d.text((w*col+12,8),label+' | '+name,fill='white')
    dest=W/('Compare3_'+name+'.png');sheet.save(dest);extra.append(str(dest.relative_to(S)))
R['renders']['additional_elbow_back_comparisons']=extra
R['material_image_visibility_audit']='JacketMaterialAudit.json'
R['contract']['material_image_visibility_identical']=True
(S/'JacketDeformFixReport.json').write_text(json.dumps(R,ensure_ascii=False,indent=1),encoding='utf8')
notes='\n추가 확인: 좌우 팔꿈치 뒤도 동일 자세·카메라·조명으로 기존 FBX 기준/h4/최종 3열 비교했다. [왼쪽](Previews/JacketDeformFix/FinalReview/Compare3_ElbowBend_BackElbowLeft.png), [오른쪽](Previews/JacketDeformFix/FinalReview/Compare3_ElbowBend_BackElbowRight.png). h4→h5에서 팔꿈치 가중치와 해당 늘어남은 유지되며, 굽힘의 접힘·당김이 남는다. `JacketMaterialAudit.json`에서 h4/최종의 재질 노드·연결·이미지 경로·내장 텍스처 해시와 모든 메시 표시 상태가 동일함을 확인했다.\n'
readme=S/'README_Model.md';doc=ROOT/'Docs/B/1104_보호장비_미비작업3건_수정기록.md'
for p in [readme,doc]:
    t=p.read_text(encoding='utf8')
    t=t.replace('**383개(383개)**','**383개**').replace('**0.1mm 초과가 363개(363개)**','**0.1mm 초과가 363개**')
    t=t.replace('에 있다.\n- 팔 내림 재수입','에 있다.\n\n- 팔 내림 재수입')
    t=t.replace('모든 간선은 각 변형안 자신의 기준 자세 길이와 비교한다.','모든 간선은 각 변형안 자신의 기준 자세 길이와 비교한다. 깊이는 0.01mm, 늘어남은 0.001배 단위로 반올림 표시하고 개수는 반올림 전 값으로 판정했다.') if '개수는 반올림 전 값으로 판정했다.' not in t else t
    if '추가 확인: 좌우 팔꿈치 뒤도' not in t:
        add=notes if p==readme else notes.replace('](Previews/','](../../Assets/B/Models/ProtectiveClothing/Source~/Previews/')
        t+=add
    p.write_text(t,encoding='utf8')
start=json.loads((W/'UnityScopeStartHashes.json').read_text(encoding='utf-8-sig'))
drift=[{'path':x['Path'],'start_sha256':x['Hash'].lower(),'end_sha256':sha(Path(x['Path']))} for x in start if sha(Path(x['Path'])).lower()!=x['Hash'].lower()]
assert all(x['path'].endswith('B_ProtectiveClothing_Equipment.unity') for x in drift),drift
scope={'external_changes_observed':drift,'note':'No Unity execution or scene/script/settings write was issued by this modeling task. Existing scene changed externally during work; preserve it, do not attribute a cause or restore it.',
    'meta_observed_change':{'file':'ProtectiveClothing_Worn.fbx.meta','observed_lastwrite_kst':'2026-11-04 15:09:49','field':'animationType','start':0,'observed':2,
        'guid_unchanged':True,'observed_copy':'Backups/20261104_145838_h4_review_before/WornMeta_ObservedExternalChange_150949.meta',
        'restored_start_bytes':True,'restored_sha256':sha(P/'ProtectiveClothing_Worn.fbx.meta'),'cause':'unconfirmed; an already-running Unity Editor was present'}}
(S/'JacketExternalChangeObservations.json').write_text(json.dumps(scope,ensure_ascii=False,indent=2),encoding='utf8')
R['external_changes_observations']='JacketExternalChangeObservations.json'
(S/'JacketDeformFixReport.json').write_text(json.dumps(R,ensure_ascii=False,indent=1),encoding='utf8')
observation='\n최종 해시 재검사 중 외부 변경 관찰: Worn `.meta`의 `animationType`이 2026-11-04 15:09:49에 0→2로 바뀐 것을 감지했다. 변경본을 별도 보존한 뒤 시작 백업의 바이트로 복원해 GUID·SHA256을 유지했다. 같은 시각대에 `B_ProtectiveClothing_Equipment.unity`도 시작 해시와 달라졌으며, 이 모델 작업에서는 씬을 쓰거나 Unity를 실행·조작하지 않았다. 해당 씬의 외부 변경은 그대로 보존했고 실행 검증 결과로 해석하지 않았다. 변경 주체·원인은 확인되지 않았다. 시작/종료 해시와 보존 경로는 `JacketExternalChangeObservations.json`에 기록했다.\n'
for p in [readme,doc]:
    t=p.read_text(encoding='utf8')
    if '최종 해시 재검사 중 외부 변경 관찰:' not in t:p.write_text(t+observation,encoding='utf8')
manifest=json.loads((S/'JacketFinalDeliveryManifest.json').read_text(encoding='utf8'))
manifest['protected_meta_sha256']={str(p):sha(p) for p in P.glob('*.meta')}
manifest['checks']={'artifact_hashes_match_review':True,'pickup_and_meta_match_start':True,'unity_scope_hashes_unchanged':not drift,'external_scene_changes_preserved':bool(drift),
    'material_image_visibility_identical':True,'raw_blender_parent_local_check_passed':False,'raw_fbx_hierarchy_passed':True,
    'normalized_inspection_passed':True,'source_and_uv_contract_passed':True}
files=[ROOT/path for path in manifest['files']]
files+=list(S.glob('jacket_*.py'))+[S/'normalize_roundtrip_review.py',S/'inspect_fbx_parent.py',S/'render_jacket_elbow_review.py',S/'finalize_jacket_review.py',S/'verify_jacket_delivery.py',S/'audit_jacket_materials.py']
files+=list(S.glob('jacket_*.log'))+[S/'JacketMaterialAudit.json',S/'FBXRawParentAudit.json',S/'JacketExternalChangeObservations.json']
files+=list(W.glob('*.png'))+[W/'Candidate_ContactSheet.jpg',W/'RawImportBeforeNormalization.blend',W/'UnityScopeStartHashes.json']
manifest['files']={str(p.relative_to(ROOT)):{'sha256':sha(p),'bytes':p.stat().st_size} for p in sorted(set(files))}
(S/'JacketFinalDeliveryManifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
badlinks=[]
for p in [readme,doc]:
    section=p.read_text(encoding='utf8')
    if p==readme:section=section.split('아래 절은 앞선 모델 작업 이력이다.')[0]
    else:section=section.split('## 9. 최종 검토:')[1]
    for target in re.findall(r'\]\(([^)]+)\)',section):
        if not (p.parent/target).exists():badlinks.append(str(p)+':'+target)
assert not badlinks,badlinks
assert all(sha(ROOT/p)==x['sha256'] for p,x in manifest['files'].items())
assert all(sha(Path(p))==h for p,h in manifest['protected_meta_sha256'].items())
print('VERIFIED',len(manifest['files']),'files; model hashes, protected files, scripts, links, material/UV contracts match. External scene drift preserved:',bool(drift))
