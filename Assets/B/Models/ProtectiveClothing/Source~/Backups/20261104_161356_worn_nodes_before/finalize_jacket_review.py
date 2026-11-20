"""Assemble the audited h5 delivery records and three-column comparison sheets."""
from pathlib import Path
import json,hashlib,shutil
from PIL import Image,ImageDraw
S=Path(__file__).resolve().parent; P=S.parent
ROOT=P.parents[3]
A=json.loads((S/'JacketFinalReview.json').read_text(encoding='utf8'))
C=json.loads((S/'JacketContractAudit.json').read_text(encoding='utf8'))
assert A['roundtrip_full']['passed'] and A['protected_hashes_unchanged'] and C['passed']
assert A['decision']=='h5_local_clearance_3mm'
B=S/'Backups/20261104_145838_h4_review_before'
H=json.loads((B/'JacketDeformFixReport.json').read_text(encoding='utf8'))
shutil.copy2(B/'JacketDeformFixReport.json',S/'JacketDeformFixH4Report.json')
W=S/'Previews/JacketDeformFix/FinalReview'
names=H['renders']['shots']
for name in names:
    images=[Image.open(path).convert('RGB') for path in [W.parent/('Before_'+name+'.png'),W.parent/('After_'+name+'.png'),W/('Final_'+name+'.png')]]
    assert all(x.size==images[0].size for x in images)
    w,h=images[0].size
    sheet=Image.new('RGB',(3*w,h+32),'#18212b');d=ImageDraw.Draw(sheet)
    for col,(im,label) in enumerate(zip(images,['Earlier Worn FBX baseline','h4','Final h5'])):
        sheet.paste(im,(col*w,32));d.text((col*w+12,8),label+' | '+name,fill='white')
    sheet.save(W/('Compare3_'+name+'.png'))
overview=['Rest_Front']+[p+'_Front' for p in H['poses']]
sheet=Image.new('RGB',(900,385*len(overview)),'#18212b');d=ImageDraw.Draw(sheet)
for row,name in enumerate(overview):
    for col,(folder,prefix,label) in enumerate([(W.parent,'Before_','Earlier FBX'),(W.parent,'After_','h4'),(W,'Final_','Final h5')]):
        im=Image.open(folder/(prefix+name+'.png')).resize((300,355))
        sheet.paste(im,(300*col,row*385+30));d.text((300*col+7,row*385+8),label+' | '+name,fill='white')
sheet.save(W/'Final_Overview.jpg',quality=94)
E=json.loads((S/'JacketDeformFixExperiments.json').read_text(encoding='utf8'))
for name,data in A['candidates'].items():
    E['variants'][name]={**data,'adopted':name=='h5_local_clearance_3mm',
        'decision_reason':'No measured regression in six review poses; small visible change without observed new tear or detached detail in inspected views.' if name=='h5_local_clearance_3mm' else ('Equivalent to h5 within 0.000003mm; retain smaller displacement cap.' if name.startswith('h6') else 'ElbowBend stretched edges 112->118, max 2.395->2.436; rejected.')}
E['adopted']='h5_local_clearance_3mm'
(S/'JacketDeformFixExperiments.json').write_text(json.dumps(E,ensure_ascii=False,indent=1),encoding='utf8')
R={**H,'delivery_version':'2026-11-04-h5','adopted_variant':'h5_local_clearance_3mm',
    'h4_report':'JacketDeformFixH4Report.json','review_report':'JacketFinalReview.json','contract_audit':'JacketContractAudit.json',
    'metrics_h4':H['metrics_after'],'metrics_after':C['final_detail_metrics'],
    'detail_h4':H['detail_after'],'detail_after':C['final_detail'],
    'rest_jacket_h4':H['rest_jacket_after'],'rest_jacket_after':C['final_rest'],
    'fix_h4':H['fix'],'fix':{'h4':H['fix'],'h5':A['source_contract'],'h5_weights':'unchanged from h4'},
    'rest_shape_change':C['movement'],'roundtrip_h4':H['roundtrip'],'roundtrip':A['roundtrip_full'],
    'contract':{'source':A['source_contract'],'audit_passed':C['passed'],'protected_hashes_unchanged':A['protected_hashes_unchanged']},
    'renders':{'baseline':'Previews/JacketDeformFix/Before_*.png','h4':'Previews/JacketDeformFix/After_*.png',
        'final':'Previews/JacketDeformFix/FinalReview/Final_*.png','comparison':'Previews/JacketDeformFix/FinalReview/Compare3_*.png','shots':names},
    'completion':{'blend_saved':True,'worn_fbx_exported':True,'separate_review_blend_saved':True,'equivalent_contract_checks_passed':True,
        'raw_blender_import_parent_local_check_passed':False,'raw_fbx_node_contract_passed':True,'normalized_inspection_contract_passed':True,
        'unity_import_first_person_mirror_vr_quality_performance':'사용자 실행 미확인'},
    'artifact_sha256':A['artifacts'],
    'measurement_note':'Signed nearest-face-normal distance to unchanged posed body. 2785 every-third samples and all 8354 vertices are separate. Edge ratios use each variant own rest lengths (>0.01mm); this is not an exact mesh intersection or cloth strain proof.'}
(S/'JacketDeformFixReport.json').write_text(json.dumps(R,ensure_ascii=False,indent=1),encoding='utf8')
ko={'Rest':'기준 자세','ArmsDown':'팔 내림','ElbowBend':'팔꿈치 굽힘','HandToChest':'손 가슴','HandToWaist':'손 허리','TorsoTwist':'몸통 회전','LightBend':'가벼운 굽힘'}
def values(p,k):return ' → '.join(str(x['metrics_'+tag][p]['Jacket'][k]) for x,tag in [(H,'previous'),(H,'after'),(R,'after')])
lines=['## 최종 검토: h5 채택 및 전체 본 재수입 (2026-11-04)',
    '', '**이 절과 `JacketDeformFixReport.json`의 h5 결과가 아래 h4/이전 작업 기록보다 우선한다.** 이번 후속 작업은 Blender 모델·FBX·검사·기록에 한정했다. 기존 Unity 변경은 보존했으며 C#·씬·Prefab·설정 수정, Unity 실행·테스트·빌드·커밋·Push를 하지 않았다.',
    '', '최종 `.blend` 저장, Worn FBX 내보내기, 별도 `FBX_Roundtrip_Review.blend` 재수입 저장·대조 완료. h4 결과가 이미 생성된 상태를 확인한 뒤 보완했다. 현재 파일과 기존 백업을 보존했으며 시작 상태는 `Backups/20261104_145838_h4_review_before/`와 `BackupSHA256.json`에 추가 보관했다.',
    '', 'h4의 옆구리 축소·가중치 재전송·칼라 어깨 가중치·짧은 간선 평활은 유지했다. h5는 소매 아래·겨드랑이 주변 깊은 관통점과 반경 18mm 안의 인접 정점 위치만 부드럽게 조정했다. 기준 자세와 6개 자세의 인체 표면을 같은 조건으로 대조해 변위를 정했으며, h4 가중치는 그대로다. 인체·기준 골격은 변경하지 않았다.',
    '', f"h4→최종: 실제 좌표 변화 {C['movement']['h4_to_final']['nonzero']}개, 0.1mm 초과 {C['movement']['h4_to_final']['over_0_1mm']}개, 최대 {C['movement']['h4_to_final']['max_mm']:.4f}mm. 피부·의복 메시를 숨기는 변경이나 새 천 시뮬레이션·런타임 보정은 없다.",
    '', '### 채택·제외 판단', '',
    '- h5: 아래 모든 자세에서 관통 깊이·5mm 초과 정점·1.5배 초과 간선·최대 늘어남·p99의 악화 없음. 수치 개선과 별도로 비교 렌더를 보고 채택했다.',
    '- h6(변위 상한 6mm): h5와 결과가 실질적으로 같아 별도 채택하지 않았다.',
    '- h7(팔꿈치 뒤 추가 가중치 평활): 팔꿈치 굽힘의 1.5배 초과 간선 112→118, 최대 2.395→2.436배로 악화해 제외했다. 최종에는 적용하지 않았다.',
    '', '### 자세별 수치: 기존 Worn FBX 기준 → h4 → 최종 h5', '',
    '전체 정점 8,354개 기준. 관통 깊이는 양수 mm로 읽으며 원시 JSON은 음의 최소 부호 거리다. 모든 간선은 각 변형안 자신의 기준 자세 길이와 비교한다.', '',
    '| 자세 | 최대 관통 mm | 5mm 초과 정점 | 1.5배 초과 간선 | 최대 늘어남 |',
    '|---|---|---|---|---|']
lines.append(f"| 기준 자세 | 0 → 0 → 0 | 0 → 0 → 0 | 0 → 0 → 0 | 1 → 1 → 1 |")
for p in H['poses']:
    depth=' → '.join(f"{-x['metrics_'+tag][p]['Jacket']['all_vertices_min_signed_distance_mm']:.2f}" for x,tag in [(H,'previous'),(H,'after'),(R,'after')])
    lines.append(f"| {ko[p]} | {depth} | {values(p,'all_vertices_inside_over_5mm')} | {values(p,'edges_over_1_5x')} | {values(p,'edge_stretch_max')} |")
lines+=['', '기존 표본 방식(정점 3개마다, 2,785개)을 별도로 보존한다. 간선 수는 표본이 아니라 전체 간선 기준이다.', '',
    '| 자세 | 표본 최대 관통 mm | 표본 5mm 초과 정점 |', '|---|---|---|']
for p in H['poses']:
    depth=' → '.join(f"{-x['metrics_'+tag][p]['Jacket']['min_signed_distance_mm']:.2f}" for x,tag in [(H,'previous'),(H,'after'),(R,'after')])
    lines.append(f"| {ko[p]} | {depth} | {values(p,'inside_body_over_5mm')} |")
mv=C['movement']
lines+=['', f"기준 자세 최소 피부 여유: 기존 {H['rest_jacket_previous']['min_signed_distance_mm']:.2f}mm, h4 {H['rest_jacket_after']['min_signed_distance_mm']:.2f}mm, 최종 {C['final_rest']['min_signed_distance_mm']:.2f}mm. 모두 피부 안쪽 정점 없음. 최근접 면 법선의 부호 거리이므로 완전한 메시 교차 검사나 실제 직물 변형률을 뜻하지 않는다.",
    '', '### 363개와 383개', '',
    f"h4에서 기존 FBX 기준 좌표가 조금이라도 바뀐 정점은 **{mv['baseline_to_h4']['nonzero']}개(383개)**이고, 그중 **0.1mm 초과가 {mv['baseline_to_h4']['over_0_1mm']}개(363개)**다. 두 값의 차이는 0~0.1mm 이동을 포함하는지의 차이다. h4의 부속 섬 추종 9개는 383개에 포함된다. 최종은 기존 FBX 기준 총 {mv['baseline_to_final']['nonzero']}개, 0.1mm 초과 {mv['baseline_to_final']['over_0_1mm']}개, 최대 {mv['baseline_to_final']['max_mm']:.4f}mm다. h4→h5 수치와 혼용하지 않는다.",
    '', '### 비교 렌더 및 외형 판단', '',
    '`Previews/JacketDeformFix/Before_*`는 기존 FBX에 수치 대조한 재구성 기준, `After_*`는 h4다. 기존 72장과 새 최종 24장 모두 보존한다. `FinalReview/Final_*`는 최종 h5, `FinalReview/Compare3_*`는 **왼쪽 기존 FBX 기준 / 가운데 h4 / 오른쪽 최종**이다. 24개 동일 자세·카메라·조명, Cycles 12 samples, 본문 이미지 760×900을 사용했다.',
    '', '[7자세 전체 비교](Previews/JacketDeformFix/FinalReview/Final_Overview.jpg), [국소 검토 시트](Previews/JacketDeformFix/FinalReview/Candidate_ContactSheet.jpg). 아래 경로는 Source~ 기준이다.', '',
    '| 자세 | 3열 비교 이미지 |','|---|---|']
for p in ['Rest']+list(H['poses']):lines.append(f"| {ko[p]} | [정면](Previews/JacketDeformFix/FinalReview/Compare3_{p}_Front.png) |")
lines+=['', '정면·사선·겨드랑이·좌우 옆구리를 검토했다. h4→h5의 외형 차이는 작고, 검토 시점에서 새로 눈에 띄는 주름 단절·포켓/부속물 이탈은 보이지 않았다. 겨드랑이의 접힘·좁아진 실루엣은 남는다. 수치가 줄어도 전 자세에서 피부 비침이 해소됐다는 판정은 아니다.',
    '', f"h5에서 부속 섬의 0.1mm 초과 이동 {C['h5_attached_vertices_moved_over_0_1mm']}개, 부속 정점의 최근접 몸판 거리 변화 최대 {C['attachment_nearest_shell_gap_change_max_mm']:.4f}mm. 기준 자세 간선 길이 비율 최대 {C['h5_rest_edge_length_ratio_max']:.4f}배(h4 대비). 이는 부착 품질의 보조 수치이며 렌더 검토와 구분한다.",
    '', '### 연결 계약·재수입 대조', '',
    '이전 h4 검사는 `ignore_leaf_bones=True`로 말단 본 13개가 검사 장면에서 빠졌다. 당시 52개 결과를 전체 본 통과로 보지 않는다. 최종은 `use_armature_deform_only=False`, `add_leaf_bones=False`, `ignore_leaf_bones=False`로 기존 65개를 모두 재수입해 대조했다.', '',
    '| 검사 | 허용 기준 | 실제 결과 |','|---|---|---|']
T=A['roundtrip_full']
maxrest=max(max(v['rest_max_m'],v['rest_reverse_max_m']) for v in T['parts'].values())
maxw=max(v['weight_max'] for v in T['parts'].values());maxpose=max(v for p in T['posed_max_m'].values() for v in p.values())
lines += [f"| 오브젝트·본 이름/개수·부모 | 정확 일치 | 오브젝트 {T['object_count']}개, 본 {T['source_bones']}→{T['imported_bones']}개, 누락·추가·부모 차이 0 |",
    f"| 로컬/월드 오브젝트 행렬 | 원소 차이 ≤1e-5 | {T['object_local_matrix_max']:.9g} / {T['object_world_matrix_max']:.9g} |",
    f"| 본 월드 rest 행렬 / 머리 기준점 | ≤1e-5 / ≤1e-5m | {T['bone_world_rest_matrix_max']:.9g} / {T['bone_head_max_m']:.9g}m |",
    f"| 기준 자세 정점(양방향) / 가중치 | ≤1e-5m / ≤1e-6 | {maxrest:.9g}m / {maxw:.9g} |",
    f"| 6자세 변형 | ≤1e-5m | 최대 {maxpose:.9g}m |",
    f"| UV / 삼각형·면 재질 배정 | ≤1e-6 / 정확 일치 | UV 최대 {max(v['corner_uv_max'] for v in C['roundtrip_corners'].values()):.9g}, 삼각형 누락·재질 배정 차이 0 |",
    '| 원시 FBX 노드 부모 | 원본과 정확 일치 | 79개(오브젝트14+본65) 이름·부모 일치 |',
    '| 원본 본 이름·부모·rest, 공통 원점·Grip·Attach 이름/부모/변환 | 변경 없음 | 재구성 기준 및 h4 대비 일치 |',
    '| 다른 Worn 5종·Pickup·인체·기준 골격 | 형상·가중치 변경 없음 | 원본 데이터 대조 일치, Pickup FBX 해시 동일 |',
    '| Jacket UV·위상·위장 재질 연결 | 변경 없음 | 원본 데이터 및 재수입 UV·면 재질 대조 일치 |',
    '| Worn/Pickup .meta SHA256·GUID | 바이트 동일 | 보존 확인 |',
    '', '**원시 Blender 재수입의 부모·로컬 행렬 직접 대조는 실패했다.** importer가 메시 6개를 `PC_Armature` 아래로 재부모화하고 로컬 축척 100으로 보상해 로컬 행렬 최대 차이 99.999977이 발생했다. 원시 FBX 바이너리의 79개 Model 노드 이름·부모는 원본과 정확히 일치했다. 원시 재수입 파일은 `FinalReview/RawImportBeforeNormalization.blend`에 보존했다. 최종 검사 장면에서만 월드 행렬을 유지한 채 메시 부모를 원본 루트로 복원해 대조했고 위 표의 로컬 행렬은 그 정규화 후 결과다. 정규화 후 기준 자세와 6자세 변형도 다시 검사했다. 원시 실패는 `JacketFinalReview.json/raw_blender_import`에 남겼으며 통과로 바꾸지 않았다. 원본 모델과 FBX에는 이 검사 장면 정규화를 적용하지 않았다.',
    '', f"정규화 후 기준 자세(동일 정점 인덱스) 최대 {max(T['normalized_rest_index_max_m'].values()):.9g}m, 6자세 최대 {max(v for x in T['normalized_posed_index_max_m'].values() for v in x.values()):.9g}m. 허용치는 1e-5m다.",
    '', f"본 꼬리 표시점은 별도 수치 {T['bone_tail_max_m']:.9g}m로 기록한다. FBX는 Blender 편집용 본 길이를 직접 저장하지 않으므로 연결 계약 판정은 이름·부모·월드 rest 행렬·머리와 실제 스킨 변형을 기준으로 한다. 원본 `.blend` 본 꼬리·rest 데이터는 변경 없음. 상세 허용치·부품별/자세별 최대 오차는 `JacketFinalReview.json`, UV·모든 메시·과거 입력 검사는 `JacketContractAudit.json`에 있다."]
for p,vals in T['posed_max_m'].items():lines.append(f"- {ko[p]} 재수입 변형 최대 차이: {max(vals.values()):.9g}m.")
lines+=['', '### 남은 제한과 원본 이력', '',
    '소매 아래·겨드랑이·옆구리 관통은 현재 형상·가중치 보완 범위에서 잔존한다. 최종 전체 정점 최대 관통은 가벼운 굽힘 7.95mm, 팔 내림 5.82mm, 회전 5.67mm다. 팔꿈치 굽힘 최대 늘어남 2.395배, 손 가슴 2.476배는 h4와 같아 과도한 늘어남이 남는다. h4의 최대 18.5mm 옆구리 축소에 의한 실루엣 변화도 유지된다. 향후 해결 가능 범위나 필요한 기법을 이번 검토만으로 단정하지 않는다.',
    '', '보존된 11/03 18:38 `.blend`는 두건·어깨 덮개가 있는 전환 이전 형상(19,282면), `PC_Armature`와 상의 가중치가 없고, 전환 이전 백업과 목록·메시가 일치함을 다시 확인했다. 이것은 파일 내용에 관한 사실이다. 이전 상태를 연 세션이 나중에 저장했을 가능성은 추정이며, 저장한 주체·프로세스·이유는 확인되지 않았다. 최종은 이를 작업 기준으로 쓰지 않았다.',
    '', '**Unity 임포트·1인칭·거울·VR 품질·성능은 사용자 실행 미확인이다.** Blender 저장·내보내기·재수입 통과와 구분한다.',
    '', '### 결과 파일과 재현 기록', '',
    '- 최종: `ProtectiveClothing.blend`, `../ProtectiveClothing_Worn.fbx`, `FBX_Roundtrip_Review.blend`.',
    '- 최종 집계: `JacketDeformFixReport.json`; h4 원보고서: `JacketDeformFixH4Report.json`.',
    '- 후보·대조: `JacketDeformFixExperiments.json`, `JacketFinalReview.json`, `JacketContractAudit.json`.',
    '- 렌더: `Previews/JacketDeformFix/FinalReview/`; 해시: `JacketFinalDeliveryManifest.json`.',
    '- `jacket_deform_fix.py`는 h4 역사 재현용이다. `final`은 h5를 덮어쓸 수 있어 현재 작업 폴더에서 재실행하지 않는다. 새 검토는 보존한 h4 복사본에서 `jacket_final_review.py`의 experiments → render_candidates → deliver 순서를 사용하고 `normalize_roundtrip_review.py`로 원시 FBX 부모 확인 및 검사 장면 정규화를 수행한다. 이력/UV 대조는 `jacket_contract_audit.py`, 문서·시트 집계는 `finalize_jacket_review.py`다. h5의 실제 로컬 변위도 `JacketContractAudit.json`에 정점별 보존했다.',
    '', '아래 절은 앞선 모델 작업 이력이다.', '']
section='\n'.join(lines)
readme=S/'README_Model.md';text=readme.read_text(encoding='utf8')
marker='## 최종 검토: h5 채택 및 전체 본 재수입 (2026-11-04)'
assert marker not in text,'Already finalized; review new changes before repeating.'
first,rest=text.split('\n',1)
text=first+'\n\n'+section+'\n'+rest
text=text.replace('기준 인체 팔이 몸통에 파고드는 한계다.','현재 형상·가중치 보완 범위에서 잔존한다.')
readme.write_text(text,encoding='utf8')
doc=ROOT/'Docs/B/1104_보호장비_미비작업3건_수정기록.md'
text=doc.read_text(encoding='utf8')
text=text.replace('즉 사용자의 새 편집이 아니라, 사용자 몸 전환 전 상태를 연 Blender 세션이 18:38에 다시 저장한 것으로 판단했다.',
    '내용이 사용자 몸 전환 전 상태와 같다는 점은 확인됐다. 이전 상태를 연 Blender 세션이 18:38에 다시 저장했을 가능성은 추정이며, 저장 주체·프로세스·이유는 확인되지 않았다.')
text=text.replace('굽힘에서 생기는 선형 스키닝 늘어남이라 천 시뮬레이션·보정 형상 없이는 남는다.',
    '현재 형상·가중치 보완 범위에서 잔존한다. 다른 보완 기법의 필요성이나 해결 가능 범위는 이번 검토로 단정하지 않는다.')
text=text.replace('기준 인체의 팔이 몸통에 파고드는 구조적 한계다.','현재 형상·가중치 보완 범위에서 잔존한다.')
text=text.replace('최소 여유가 가장 커지는 값으로 고른다. 1 mm 안이면 작은 이동을 택한다.',
    '최소 여유가 가장 커지는 값으로 고른다. 실제 코드는 최선에서 1mm 이내인 비율 중 가장 큰 비율을 택한다.')
# Add the final independent record while retaining all prior sections and historical code work.
section_doc=section.replace('## 최종 검토:', '## 9. 최종 검토:',1)
section_doc=section_doc.replace('](Previews/','](../../Assets/B/Models/ProtectiveClothing/Source~/Previews/')
text+='\n\n'+section_doc
doc.write_text(text,encoding='utf8')
protected={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in P.glob('*.meta')}
manifest={'version':'2026-11-04-h5','equivalent_contract_checks_passed':True,'raw_blender_import_parent_local_check_passed':False,'unity_user_execution':'미확인',
    'source_report':'JacketDeformFixReport.json','protected_meta_sha256':protected,
    'files':{}}
files=[S/'ProtectiveClothing.blend',P/'ProtectiveClothing_Worn.fbx',P/'ProtectiveClothing_Pickup.fbx',S/'FBX_Roundtrip_Review.blend',
    S/'JacketDeformFixReport.json',S/'JacketDeformFixH4Report.json',S/'JacketFinalReview.json',S/'JacketContractAudit.json',S/'JacketDeformFixExperiments.json',readme,doc]
files+=sorted(W.glob('Final_*.png'))+sorted(W.glob('Compare3_*.png'))+[W/'Final_Overview.jpg']
for p in files:manifest['files'][str(p.relative_to(ROOT))]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':p.stat().st_size}
(S/'JacketFinalDeliveryManifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf8')
print('FINALIZED',len(manifest['files']),'artifacts')
