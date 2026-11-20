"""Preserve raw import failure; verify FBX node parents then normalize inspector-only parenting."""
from pathlib import Path
import bpy,json,sys,hashlib
from io_scene_fbx import parse_fbx
S=Path(__file__).resolve().parent
sys.argv=[sys.argv[0]]
exec(compile((S/'jacket_final_review.py').read_text(encoding='utf8').split('# ---------- review entry point ----------')[0],str(S/'jacket_final_review.py'),'exec'))
A=json.loads(AUDIT.read_text(encoding='utf8'))
current()
expected=objects_state(list(wc.objects));bones=bone_state(pa)
rest={p:evaluated(bpy.data.objects[p+'_Worn'])[0] for p in PARTS}
posed={}
for name in POSES:
    pose(name);posed[name]={p:evaluated(bpy.data.objects[p+'_Worn'])[0] for p in PARTS}
clear_pose()
tree,version=parse_fbx.parse(str(P/'ProtectiveClothing_Worn.fbx'))
objects=next(e for e in tree.elems if e.id==b'Objects')
names={e.props[0]:e.props[1].decode().split('\x00')[0] for e in objects.elems if e.id==b'Model'}
connections=next(e for e in tree.elems if e.id==b'Connections')
parents={names[e.props[1]]:names.get(e.props[2]) for e in connections.elems if e.props[0]==b'OO' and e.props[1] in names and (e.props[2] in names or e.props[2]==0)}
expected_parents={n:x['parent'] for n,x in expected.items()}
expected_parents.update({n:x['parent'] or 'PC_Armature' for n,x in bones.items()})
fbx_fail=[n for n,p in expected_parents.items() if n not in parents or parents[n]!=p]
assert not fbx_fail,fbx_fail
A['raw_fbx_node_contract']={'model_nodes':len(names),'expected_nodes':len(expected_parents),
    'missing':sorted(set(expected_parents)-set(parents)),'extra':sorted(set(parents)-set(expected_parents)),
    'parent_errors':fbx_fail,'parents':parents,'passed':not fbx_fail and set(parents)==set(expected_parents)}
bpy.ops.wm.open_mainfile(filepath=str(S/'FBX_Roundtrip_Review.blend'))
sc=bpy.context.scene;vl=bpy.context.view_layer;pa=bpy.data.objects['PC_Armature']
assert pa.parent.name=='ProtectiveClothing_Worn'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'RawImportBeforeNormalization.blend'),copy=True)
raw=A['roundtrip_full']
A['raw_blender_import']=raw
normalized=[]
for n,x in expected.items():
    o=bpy.data.objects[n]
    if (o.parent.name if o.parent else None)!=x['parent']:
        assert n.endswith('_Worn') and o.type=='MESH' and o.parent==pa
        world=o.matrix_world.copy()
        o.parent=bpy.data.objects.get(x['parent']);o.parent_type=x['parent_type'];o.parent_bone=x['parent_bone']
        o.matrix_parent_inverse=Matrix.Identity(4);o.matrix_world=world
        normalized.append(n)
vl.update()
rt=json.loads(json.dumps(raw));rt['normalization_applied_to_inspection_only']=normalized
rt['raw_import_passed']=False
rt['raw_import_failures']=raw['failures']
rt['normalization_reason']='Blender FBX importer reparented skinned meshes to PC_Armature with compensating local 100 scale. Raw FBX node hierarchy matches source exactly. Restore only inspector mesh parents preserving world transforms; source and FBX bytes stay unchanged.'
rt['parent_errors']=[];rt['object_local_matrix_max']=0;rt['object_world_matrix_max']=0
for n,x in expected.items():
    o=bpy.data.objects[n]
    if (o.parent.name if o.parent else None)!=x['parent']:rt['parent_errors'].append(n)
    rt['object_local_matrix_max']=max(rt['object_local_matrix_max'],matrix_error(x['local'],o.matrix_local))
    rt['object_world_matrix_max']=max(rt['object_world_matrix_max'],matrix_error(x['world'],o.matrix_world))
def maxd(a,b):return max((x-y).length for x,y in zip(a,b))
rt['normalized_rest_index_max_m']={p:maxd(evaluated(bpy.data.objects[p+'_Worn'])[0],rest[p]) for p in PARTS}
rt['normalized_posed_index_max_m']={}
for name in POSES:
    for pb in pa.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    vl.update();POSES[name](pa)
    rt['normalized_posed_index_max_m'][name]={p:maxd(evaluated(bpy.data.objects[p+'_Worn'])[0],posed[name][p]) for p in PARTS}
for pb in pa.pose.bones:pb.matrix_basis=Matrix.Identity(4)
vl.update()
fail=[]
for k in ['missing_objects','extra_objects','missing_bones','extra_bones','parent_errors','bone_parent_errors']:
    if rt[k]:fail.append(k)
for k in ['object_local_matrix_max','object_world_matrix_max','bone_world_rest_matrix_max','bone_head_max_m']:
    if rt[k]>1e-5:fail.append(k)
if max(rt['normalized_rest_index_max_m'].values())>1e-5:fail.append('normalized_rest')
if max(v for x in rt['normalized_posed_index_max_m'].values() for v in x.values())>1e-5:fail.append('normalized_pose')
rt['failures']=fail;rt['passed']=not fail and A['raw_fbx_node_contract']['passed']
A['roundtrip_full']=rt
A['decision']='h5_local_clearance_3mm' if rt['passed'] else 'h5_roundtrip_failed'
sc['RawImportParentDifferences']='See JacketFinalReview.json/raw_blender_import and FinalReview/RawImportBeforeNormalization.blend. This review normalizes only mesh parents, world matrices unchanged.'
bpy.ops.wm.save_as_mainfile(filepath=str(S/'FBX_Roundtrip_Review.blend'))
A['artifacts'][str((S/'FBX_Roundtrip_Review.blend').relative_to(P))]=digest(S/'FBX_Roundtrip_Review.blend')
dump(A)
print('NORMALIZED_INSPECTOR',rt['passed'],rt['object_local_matrix_max'],rt['normalized_rest_index_max_m'],flush=True)
