"""Read-only follow-up against preserved source states and delivered FBX review."""
from pathlib import Path
import bpy,sys,json,hashlib,collections
from mathutils import Vector
from mathutils.kdtree import KDTree
S=Path(__file__).resolve().parent
sys.argv=[sys.argv[0]]
exec(compile((S/'jacket_final_review.py').read_text(encoding='utf8').split('# ---------- review entry point ----------')[0],str(S/'jacket_final_review.py'),'exec'))
OUT=S/'JacketContractAudit.json'
out={}

def signature(mesh):
    return hashlib.sha256(repr(mesh).encode()).hexdigest()

def raw_inventory(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    return {'objects':{o.name:{'type':o.type,'parent':o.parent.name if o.parent else None,
               'local':[list(r) for r in o.matrix_local],
               'mesh':signature(mesh_state(o)) if o.type=='MESH' else None} for o in bpy.data.objects},
        'jacket_vertices':len(bpy.data.objects['Jacket_Worn'].data.vertices),
        'jacket_faces':len(bpy.data.objects['Jacket_Worn'].data.polygons),
        'jacket_groups':len(bpy.data.objects['Jacket_Worn'].vertex_groups),
        'PC_Armature':'PC_Armature' in bpy.data.objects}

stale=raw_inventory(S/'Backups/20261104_jacket_deform_before/Model/Source~/ProtectiveClothing.blend')
older=raw_inventory(S/'Backups/20261103_userwear_before/Model/Source~/ProtectiveClothing.blend')
out['stale_source']={'summary':{k:v for k,v in stale.items() if k!='objects'},'content_matches_pre_userwear_inventory':stale==older,
    'cause_fact':'Preserved later-saved blend contains pre-userwear geometry and no PC_Armature.',
    'cause_inference':'An older Blender session may have saved the old state. Saving actor/process and reason are not established by these files.'}
current()
final_co=[v.co.copy() for v in jo.data.vertices]
final_mesh={o.name:mesh_state(o) for o in bpy.data.objects if o.type=='MESH'}
final_bones={o.name:bone_state(o) for o in bpy.data.objects if o.type=='ARMATURE'}
final_obj=objects_state(list(bpy.data.objects))
out['final_detail_metrics']=metrics('audit_final',detail=True)
out['final_detail']=report['detail_audit_final']
out['final_rest']=report['rest_jacket_audit_final']
load(); previous_fix()
previous_co=[v.co.copy() for v in jo.data.vertices]
out['unchanged_meshes_vs_rebuilt_baseline']={o.name:mesh_state(o)==final_mesh[o.name] for o in bpy.data.objects if o.type=='MESH' and o.name!='Jacket_Worn'}
out['jacket_preserved_fields_vs_rebuilt_baseline']={k:v==final_mesh['Jacket_Worn'][k] for k,v in mesh_state(jo).items() if k not in ('co','weights')}
out['bone_contract_vs_rebuilt_baseline']={a.name:all(all((matrix_error(x[k],bone_state(a)[n][k])==0 if isinstance(x[k],Matrix) else x[k]==bone_state(a)[n][k]) for k in x) for n,x in final_bones[a.name].items()) for a in bpy.data.objects if a.type=='ARMATURE'}
now=objects_state(list(bpy.data.objects))
out['object_contract_vs_rebuilt_baseline']={n:all((matrix_error(v,now[n][k])==0 if isinstance(v,Matrix) else v==now[n][k]) for k,v in x.items()) for n,x in final_obj.items() if n in now}
fix_jacket2()
h4_co=[v.co.copy() for v in jo.data.vertices]
out['h4_weights_equal_final']=mesh_state(jo)['weights']==final_mesh['Jacket_Worn']['weights']
def moves(a,b):
    ds=[(x-y).length for x,y in zip(a,b)]
    return {'nonzero':sum(d>0 for d in ds),'over_0_1mm':sum(d>.0001 for d in ds),'max_mm':max(ds)*1000}
out['movement']={'baseline_to_h4':moves(previous_co,h4_co),'h4_to_final':moves(h4_co,final_co),'baseline_to_final':moves(previous_co,final_co)}
out['h5_displacement_local_m']=[{'vertex':i,'delta':list(b-a)} for i,(a,b) in enumerate(zip(h4_co,final_co)) if (a-b).length>0]
island,sizes=islands_by_vertex(jo.data); main=max(range(len(sizes)),key=lambda k:sizes[k])
out['h5_islands_moved']={str(k):sum(island[i]==k and (a-b).length>1e-7 for i,(a,b) in enumerate(zip(h4_co,final_co))) for k in range(len(sizes))}
out['h5_attached_vertices_moved_over_0_1mm']=sum(island[i]!=main and (a-b).length>.0001 for i,(a,b) in enumerate(zip(h4_co,final_co)))
def attachment_gaps(co):
    kd=KDTree(sizes[main])
    for i,c in enumerate(co):
        if island[i]==main:kd.insert(c,i)
    kd.balance()
    return {i:kd.find(c)[2] for i,c in enumerate(co) if island[i]!=main}
gap0=attachment_gaps(h4_co);gap1=attachment_gaps(final_co)
out['attachment_nearest_shell_gap_change_max_mm']=max(abs(gap1[i]-gap0[i]) for i in gap0)*1000
out['h5_rest_edge_length_ratio_max']=max((final_co[e.vertices[0]]-final_co[e.vertices[1]]).length/(h4_co[e.vertices[0]]-h4_co[e.vertices[1]]).length for e in jo.data.edges if (h4_co[e.vertices[0]]-h4_co[e.vertices[1]]).length>1e-5)
# source corner records for UV/topology/material validation; preserve exact triangle and corner data.
current()
source={}
for p in PARTS:
    o=bpy.data.objects[p+'_Worn']; uv=o.data.uv_layers.active
    source[p]={'co':[o.matrix_world@v.co for v in o.data.vertices],
        'triangles':[[{'co':o.matrix_world@o.data.vertices[o.data.loops[l].vertex_index].co,'uv':Vector(uv.data[l].uv)} for l in f.loop_indices] for f in o.data.polygons],
        'materials':[f.material_index for f in o.data.polygons],
        'material_names':[m.name for m in o.data.materials]}
bpy.ops.wm.open_mainfile(filepath=str(S/'FBX_Roundtrip_Review.blend'))
rt={}
for p in PARTS:
    o=bpy.data.objects[p+'_Worn']; uv=o.data.uv_layers.active; src=source[p]
    centers=[sum((c['co'] for c in t),Vector())/3 for t in src['triangles']]
    kd=KDTree(len(centers))
    for i,c in enumerate(centers):kd.insert(c,i)
    kd.balance(); max_uv=0.; max_co=0.; missing=0; matbad=0; used=set()
    import itertools
    for f in o.data.polygons:
        corners=[{'co':o.matrix_world@o.data.vertices[o.data.loops[l].vertex_index].co,'uv':Vector(uv.data[l].uv)} for l in f.loop_indices]
        center=sum((c['co'] for c in corners),Vector())/3
        candidates=kd.find_range(center,1e-5)
        best=None
        for _,idx,_ in candidates:
            if idx in used:continue
            for perm in itertools.permutations(src['triangles'][idx]):
                dc=max((a['co']-b['co']).length for a,b in zip(corners,perm))
                du=max((a['uv']-b['uv']).length for a,b in zip(corners,perm))
                score=(dc>1e-5,du,dc)
                if best is None or score<best[0]:best=(score,idx,dc,du)
        if best is None:missing+=1;continue
        _,idx,dc,du=best;used.add(idx);max_co=max(max_co,dc);max_uv=max(max_uv,du)
        matbad+=f.material_index!=src['materials'][idx]
    rt[p]={'unmatched_imported_triangles':missing,'unmatched_source_triangles':len(src['triangles'])-len(used),
        'corner_position_max_m':max_co,'corner_uv_max':max_uv,'material_index_mismatches':matbad,
        'material_names_equal':[m.name for m in o.data.materials]==src['material_names']}
out['roundtrip_corners']=rt
out['roundtrip_corner_tolerance']={'point_m':1e-5,'uv':1e-6,'triangle_and_material_assignment':'exact'}
out['passed']=all(out['unchanged_meshes_vs_rebuilt_baseline'].values()) and all(out['jacket_preserved_fields_vs_rebuilt_baseline'].values()) and all(out['bone_contract_vs_rebuilt_baseline'].values()) and all(out['object_contract_vs_rebuilt_baseline'].values()) and out['h4_weights_equal_final'] and all(not x['unmatched_imported_triangles'] and not x['unmatched_source_triangles'] and x['corner_position_max_m']<=1e-5 and x['corner_uv_max']<=1e-6 and not x['material_index_mismatches'] and x['material_names_equal'] for x in rt.values())
OUT.write_text(json.dumps(out,indent=2,ensure_ascii=False,default=str),encoding='utf8')
print('CONTRACT_AUDIT_DONE',out['passed'],out['movement'],rt,flush=True)
