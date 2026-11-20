"""Review existing h4 without rerunning the historical final pipeline.
Run Blender -b --factory-startup --python this.py -- experiments|deliver
All work stays in the model source directory; historical files are backed up first.
"""
from pathlib import Path
import sys, json, hashlib, shutil, time
import bpy
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
import numpy as np

S = Path(__file__).resolve().parent
P = S.parent
MODE_REVIEW = sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'experiments'
# Reuse pose/measurement/render definitions, never the destructive historical run block.
oldargv = sys.argv[:]
sys.argv = [sys.argv[0]]
exec(compile((S/'jacket_deform_fix.py').read_text(encoding='utf8').split('# ---------- run ----------')[0], str(S/'jacket_deform_fix.py'), 'exec'))
sys.argv = oldargv
P = S.parent
R = json.loads((S/'JacketDeformFixReport.json').read_text(encoding='utf8'))
AUDIT = S/'JacketFinalReview.json'
WORK = S/'Previews'/'JacketDeformFix'/'FinalReview'
WORK.mkdir(exist_ok=True)

def current():
    global sc, vl, wc, pc, pa, arm, body, jo, src_basis
    bpy.ops.wm.open_mainfile(filepath=str(S/'ProtectiveClothing.blend'))
    sc=bpy.context.scene; vl=bpy.context.view_layer
    wc=bpy.data.collections['Worn']; pc=bpy.data.collections['Pickup']
    pa=bpy.data.objects['PC_Armature']; arm=bpy.data.objects['Armature']
    body=bpy.data.objects['Ch36']; jo=bpy.data.objects['Jacket_Worn']
    src_basis={pb.name:pb.matrix_basis.copy() for pb in arm.pose.bones}
    clear_pose()

def digest(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def dump(x):
    AUDIT.write_text(json.dumps(x,ensure_ascii=False,indent=2),encoding='utf8')

def jacket_metrics(tag):
    return {k:v['Jacket'] for k,v in metrics(tag).items()}

def regressions(base,trial):
    bad=[]
    for pose_ in POSES:
        for k in ('all_vertices_inside_over_5mm','inside_body_over_5mm','edges_over_1_5x','edge_stretch_max','edge_stretch_p99'):
            if trial[pose_][k]>base[pose_][k]: bad.append([pose_,k,base[pose_][k],trial[pose_][k]])
        for k in ('all_vertices_min_signed_distance_mm','min_signed_distance_mm'):
            if trial[pose_][k]<base[pose_][k]: bad.append([pose_,k,base[pose_][k],trial[pose_][k]])
    return bad

def experiment():
    current()
    audit={'source_h4_sha256':digest(S/'ProtectiveClothing.blend'),'baseline_report_sha256':digest(S/'JacketDeformFixReport.json'),
           'started_kst':'2026-11-04','candidates':{},'protected_files_start':{str(p.relative_to(P)):digest(p) for p in [P/'ProtectiveClothing_Pickup.fbx',P/'ProtectiveClothing_Pickup.fbx.meta',P/'ProtectiveClothing_Worn.fbx.meta']}}
    audit['h4_measured']=base=jacket_metrics('h4_review')
    audit['h4_matches_report']=base=={k:v['Jacket'] for k,v in R['metrics_after'].items()}
    coords=np.array([v.co[:] for v in jo.data.vertices]); n=len(coords)
    audit['modifier_inventory']=[{'type':m.type,'name':m.name} for m in jo.modifiers]
    # Numerical local skinning Jacobian, same body/rig/poses as the existing report.
    cache=[]
    for name in ['Rest']+list(POSES):
        if name=='Rest': clear_pose()
        else: pose(name)
        bp,bf=evaluated(body); tree=BVHTree.FromPolygons(bp,bf)
        q=np.array([v[:] for v in evaluated(jo)[0]])
        jac=np.zeros((n,3,3)); eps=.0005
        for axis in range(3):
            for v,c in zip(jo.data.vertices,coords):
                v.co=c; v.co[axis]+=eps
            jo.data.update(); vl.update()
            jac[:,:,axis]=(np.array([v[:] for v in evaluated(jo)[0]])-q)/eps
        for v,c in zip(jo.data.vertices,coords): v.co=c
        jo.data.update(); vl.update()
        sd=np.array([(Vector(p)-loc).dot(no) for p in q for loc,no,_,_ in [tree.find_nearest(Vector(p))]])
        cache.append((name,tree,q,jac,sd))
    clear_pose()
    # Only points with existing deep penetration, plus a spatially tapered 18mm neighborhood.
    worst=np.min(np.stack([c[-1] for c in cache]),axis=0)
    seeds=np.flatnonzero(worst<-.0045)
    distances=np.min(np.linalg.norm(coords[:,None,:]-coords[seeds][None,:,:],axis=2),axis=1)
    taper=np.clip(1-distances/.018,0,1)
    adj=adjacency(jo.data)
    for label,bound in [('h5_local_clearance_3mm',.003),('h6_local_clearance_6mm',.006)]:
        delta=np.zeros_like(coords)
        for iteration in range(15):
            for name,tree,q,jac,sd in cache:
                new=q+np.einsum('nij,nj->ni',jac,delta)
                for i in np.flatnonzero(taper>0):
                    loc,no,_,_=tree.find_nearest(Vector(new[i]))
                    distance=(Vector(new[i])-loc).dot(no)
                    target=max(sd[i],-.0045) if name!='Rest' else min(sd[i],.002)
                    if distance<target:
                        grad=np.asarray(no)@jac[i]
                        delta[i]+=(target-distance)*grad/(grad@grad+1e-12)*.7
            # Taper, preserve near-coincident shell layers / attached details, soften displacement field.
            for _ in range(2):
                delta=np.array([.6*delta[i]+.4*np.mean(delta[js],axis=0) if js else delta[i] for i,js in enumerate(adj)])
            length=np.linalg.norm(delta,axis=1)
            delta*=np.minimum(1,bound*taper/(length+1e-12))[:,None]
        for v,c in zip(jo.data.vertices,coords+delta): v.co=c
        jo.data.update(); vl.update()
        trial=jacket_metrics(label)
        bad=regressions(base,trial)
        audit['candidates'][label]={'description':'Local rest position displacement from all seven poses, fixed h4 weights, tapered 18mm neighborhood; no body or bone edits.',
            'max_move_mm':float(np.linalg.norm(delta,axis=1).max()*1000),'moved_over_0_1mm':int(np.sum(np.linalg.norm(delta,axis=1)>.0001)),
            'jacket':trial,'regressions':bad,'accepted':not bad}
        # Save as review-only candidate, never overwrite production h4 on a failed numeric comparison.
        bpy.ops.wm.save_as_mainfile(filepath=str(WORK/(label+'.blend')),copy=True)
        dump(audit)
        for v,c in zip(jo.data.vertices,coords): v.co=c
        jo.data.update(); vl.update()
    # Local extra smoothing behind the elbow, with h4 positions retained.
    weights=[{short(jo.vertex_groups[g.group].name):g.weight for g in v.groups} for v in jo.data.vertices]
    sel=[i for i,c in enumerate(coords) if .40<abs(c[0])<.52 and c[1]>.005]
    w=smooth(weights,adj,6,sel,.35)
    write_weights(w,[m for m in jo.modifiers if m.type=='ARMATURE'])
    trial=jacket_metrics('h7_elbow_weights')
    bad=regressions(base,trial)
    audit['candidates']['h7_elbow_weights']={'description':'Six 35% neighbor passes only behind elbows, h4 shape unchanged.','selected':len(sel),'jacket':trial,'regressions':bad,'accepted':not bad}
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'h7_elbow_weights.blend'),copy=True)
    audit['decision']='pending_visual_review'
    dump(audit)
    print('EXPERIMENTS_DONE',AUDIT,flush=True)

def render_candidates():
    global PREV
    PREV=WORK
    for label in ['h5_local_clearance_3mm','h7_elbow_weights']:
        global sc,vl,wc,pc,pa,arm,body,jo,src_basis
        bpy.ops.wm.open_mainfile(filepath=str(WORK/(label+'.blend')))
        sc=bpy.context.scene; vl=bpy.context.view_layer
        wc=bpy.data.collections['Worn']; pc=bpy.data.collections['Pickup']
        pa=bpy.data.objects['PC_Armature']; arm=bpy.data.objects['Armature']; body=bpy.data.objects['Ch36']; jo=bpy.data.objects['Jacket_Worn']
        src_basis={pb.name:pb.matrix_basis.copy() for pb in arm.pose.bones}
        cam=setup_render()
        for p,view in [('Rest','Front'),('ArmsDown','SideL'),('ElbowBend','ThreeQuarter'),('HandToChest','SideR'),('LightBend','ArmpitL'),('LightBend','SideL')]:
            if p=='Rest':clear_pose()
            else:pose(p)
            eye,target,scale,_=SHOTS[view]
            shot(cam,label+'_'+p+'_'+view,eye,target,scale)
    print('CANDIDATE_RENDERS_DONE',flush=True)

def mesh_state(o):
    return {'co':[tuple(v.co) for v in o.data.vertices],
        'weights':[sorted((o.vertex_groups[g.group].name,g.weight) for g in v.groups) for v in o.data.vertices],
        'faces':[tuple(f.vertices) for f in o.data.polygons],
        'uv':{uv.name:[tuple(x.uv) for x in uv.data] for uv in o.data.uv_layers},
        'materials':[m.name if m else None for m in o.data.materials],
        'material_indices':[f.material_index for f in o.data.polygons],
        'mesh_name':o.data.name}

def bone_state(a):
    return {b.name:{'parent':b.parent.name if b.parent else None,'matrix':[list(r) for r in b.matrix_local],
        'world':a.matrix_world@b.matrix_local,'head':a.matrix_world@b.head_local,'tail':a.matrix_world@b.tail_local,
        'deform':b.use_deform} for b in a.data.bones}

def objects_state(objs):
    return {o.name:{'parent':o.parent.name if o.parent else None,'parent_type':o.parent_type,'parent_bone':o.parent_bone,
        'local':o.matrix_local.copy(),'world':o.matrix_world.copy(),'basis':o.matrix_basis.copy()} for o in objs}

def matrix_error(a,b):
    return max(abs(a[i][j]-b[i][j]) for i in range(4) for j in range(4))

def deliver():
    global pa,sc,vl,PREV
    audit=json.loads(AUDIT.read_text(encoding='utf8'))
    assert audit['h4_matches_report']
    assert not audit['candidates']['h5_local_clearance_3mm']['regressions']
    # Obtain only the candidate coordinates; reopen h4 to retain all original links/settings.
    bpy.ops.wm.open_mainfile(filepath=str(WORK/'h5_local_clearance_3mm.blend'))
    chosen=[v.co.copy() for v in bpy.data.objects['Jacket_Worn'].data.vertices]
    current()
    before={o.name:mesh_state(o) for o in bpy.data.objects if o.type=='MESH'}
    before_obj=objects_state(list(bpy.data.objects))
    before_bones={a.name:bone_state(a) for a in bpy.data.objects if a.type=='ARMATURE'}
    original_co=[v.co.copy() for v in jo.data.vertices]
    for v,c in zip(jo.data.vertices,chosen): v.co=c
    jo.data.update(); vl.update()
    problems=[]
    for o in bpy.data.objects:
        if o.type=='MESH':
            state=mesh_state(o)
            for key,value in before[o.name].items():
                if o.name=='Jacket_Worn' and key=='co':continue
                if value!=state[key]:problems.append(o.name+': '+key)
    now_obj=objects_state(list(bpy.data.objects))
    for n,x in before_obj.items():
        for key,value in x.items():
            y=now_obj[n][key]
            if (matrix_error(value,y)>0 if isinstance(value,Matrix) else value!=y):problems.append(n+': '+key)
    for a in bpy.data.objects:
        if a.type=='ARMATURE':
            now=bone_state(a)
            for n,x in before_bones[a.name].items():
                for key,value in x.items():
                    y=now[n][key]
                    if (matrix_error(value,y)>0 if isinstance(value,Matrix) else value!=y):problems.append(a.name+'/'+n+': '+key)
    audit['source_contract']={'problems':problems,'mesh_count':len(before),'objects_checked':len(before_obj),
       'jacket_only_positions_changed':not problems,'body_and_original_armature_unchanged':not problems,
       'h4_to_final_moved_nonzero':sum((a-b).length>0 for a,b in zip(original_co,chosen)),
       'h4_to_final_moved_over_0_1mm':sum((a-b).length>.0001 for a,b in zip(original_co,chosen)),
       'h4_to_final_max_move_mm':max((a-b).length for a,b in zip(original_co,chosen))*1000}
    assert not problems,problems
    audit['final_measured']=jacket_metrics('final')
    audit['rest_final']=report['rest_jacket_final']
    assert audit['final_measured']==audit['candidates']['h5_local_clearance_3mm']['jacket']
    clear_pose()
    expected_obj=objects_state(list(wc.objects))
    expected_bones=bone_state(pa)
    expected_mesh={p:mesh_state(bpy.data.objects[p+'_Worn']) for p in PARTS}
    rest_src={p:evaluated(bpy.data.objects[p+'_Worn'])[0] for p in PARTS}
    posed_src={}
    for name in POSES:
        pose(name)
        posed_src[name]={p:evaluated(bpy.data.objects[p+'_Worn'])[0] for p in PARTS}
    clear_pose()
    # Keep a serialized source expectation independent of the reimported file.
    audit['expected_bone_names']=list(expected_bones)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(S/'ProtectiveClothing.blend'),relative_remap=False)
    audit['save_complete']=True
    dump(audit)
    pc.hide_viewport=False
    bpy.ops.object.select_all(action='DESELECT')
    for o in wc.objects:
        if o.type in {'MESH','EMPTY','ARMATURE'}:o.hide_set(False); o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(P/'ProtectiveClothing_Worn.fbx'),use_selection=True,object_types={'MESH','EMPTY','ARMATURE'},
        global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        use_space_transform=True,bake_space_transform=False,bake_anim=False,add_leaf_bones=False,use_custom_props=True,
        path_mode='RELATIVE',embed_textures=False,mesh_smooth_type='FACE',use_armature_deform_only=False,
        primary_bone_axis='Y',secondary_bone_axis='X',armature_nodetype='NULL')
    audit['export_settings']={'use_armature_deform_only':False,'add_leaf_bones':False,'ignore_leaf_bones_on_reimport':False,
        'reason':'Preserve all 65 existing bones including 13 terminal bones; no generated extra leaf bones.'}
    audit['export_complete']=True
    dump(audit)
    # Render from exact saved source; all materials/visibility changes are review-only and never saved into the source.
    PREV=WORK
    audit['final_render_shots']=renders('Final')
    bpy.ops.wm.read_homefile(use_empty=True)
    sc=bpy.context.scene; vl=bpy.context.view_layer
    def import_group(label,path):
        coll=bpy.data.collections.new(label); sc.collection.children.link(coll)
        vl.active_layer_collection=vl.layer_collection.children[label]
        old=set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=str(path),use_custom_props=True,ignore_leaf_bones=False,automatic_bone_orientation=False)
        return {o.name.split('.')[0]:o for o in bpy.data.objects if o not in old}
    imp=import_group('Reimport_Worn',P/'ProtectiveClothing_Worn.fbx')
    ia=imp['PC_Armature']; pa=ia
    got_bones=bone_state(ia)
    rt={'missing_objects':sorted(set(expected_obj)-set(imp)),'extra_objects':sorted(set(imp)-set(expected_obj)),
        'missing_bones':sorted(set(expected_bones)-set(got_bones)),'extra_bones':sorted(set(got_bones)-set(expected_bones)),
        'object_count':len(imp),'source_bones':len(expected_bones),'imported_bones':len(got_bones),
        'parent_errors':[],'bone_parent_errors':[],'object_local_matrix_max':0,'object_world_matrix_max':0,
        'bone_world_rest_matrix_max':0,'bone_head_max_m':0,'bone_tail_max_m':0,'parts':{},'posed_max_m':{}}
    for n,x in expected_obj.items():
        if n not in imp:continue
        o=imp[n]; parent=o.parent.name if o.parent else None
        if parent!=x['parent'] or o.parent_type!=x['parent_type'] or o.parent_bone!=x['parent_bone']:rt['parent_errors'].append(n)
        rt['object_local_matrix_max']=max(rt['object_local_matrix_max'],matrix_error(x['local'],o.matrix_local))
        rt['object_world_matrix_max']=max(rt['object_world_matrix_max'],matrix_error(x['world'],o.matrix_world))
    for n,x in expected_bones.items():
        if n not in got_bones:continue
        y=got_bones[n]
        if x['parent']!=y['parent']:rt['bone_parent_errors'].append(n)
        rt['bone_world_rest_matrix_max']=max(rt['bone_world_rest_matrix_max'],matrix_error(x['world'],y['world']))
        rt['bone_head_max_m']=max(rt['bone_head_max_m'],(x['head']-y['head']).length)
        rt['bone_tail_max_m']=max(rt['bone_tail_max_m'],(x['tail']-y['tail']).length)
    pairs={}
    for p in PARTS:
        o=imp[p+'_Worn']; src=expected_mesh[p]; kd=KDTree(len(rest_src[p]))
        for i,co in enumerate(rest_src[p]):kd.insert(co,i)
        kd.balance()
        mapping=[]; worst_w=0; max_d=0
        # FBX splits UV/material seams; choose coincident source point with closest weight vector.
        for v in o.data.vertices:
            co=o.matrix_world@v.co; _,j,d=kd.find(co)
            wi={o.vertex_groups[g.group].name:g.weight for g in v.groups if g.weight>0}
            near=kd.find_range(co,max(1e-6,d+1e-8))
            def wd(index):
                ws=dict(src['weights'][index]);return max([0]+[abs(wi.get(k,0)-ws.get(k,0)) for k in set(wi)|set(ws)])
            if near:j=min(near,key=lambda hit:(wd(hit[1]),hit[2]))[1]
            max_d=max(max_d,(co-rest_src[p][j]).length);worst_w=max(worst_w,wd(j)); mapping.append(j)
        pairs[p]=mapping
        reverse=max(kd_.length for kd_ in []) if False else 0
        ikd=KDTree(len(o.data.vertices))
        for i,v in enumerate(o.data.vertices):ikd.insert(o.matrix_world@v.co,i)
        ikd.balance(); reverse=max(ikd.find(v)[2] for v in rest_src[p])
        rt['parts'][p]={'source_vertices':len(src['co']),'imported_vertices':len(o.data.vertices),
            'rest_max_m':max_d,'rest_reverse_max_m':reverse,'weight_max':worst_w,
            'triangles_source':len(src['faces']),'triangles_imported':len(o.data.polygons),
            'materials_source':src['materials'],'materials_imported':[m.name if m else None for m in o.data.materials],
            'armature_modifier':any(m.type=='ARMATURE' and m.object==ia for m in o.modifiers)}
    for name in POSES:
        for pb in ia.pose.bones:pb.matrix_basis=Matrix.Identity(4)
        vl.update();POSES[name](ia)
        rt['posed_max_m'][name]={p:max((q-posed_src[name][p][j]).length for q,j in zip(evaluated(imp[p+'_Worn'])[0],pairs[p])) for p in PARTS}
    for pb in ia.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    vl.update()
    pick=import_group('Reimport_Pickup',P/'ProtectiveClothing_Pickup.fbx')
    rt['pickup_imported_objects']=len(pick)
    rt['tolerances']={'point_m':1e-5,'matrix_element':1e-5,'weight':1e-6,'names_and_parents':'exact','counts':'exact except documented UV seam vertex splitting'}
    failures=[]
    for k in ['missing_objects','extra_objects','missing_bones','extra_bones','parent_errors','bone_parent_errors']:
        if rt[k]:failures.append(k)
    for k in ['object_local_matrix_max','object_world_matrix_max','bone_world_rest_matrix_max','bone_head_max_m']:
        if rt[k]>1e-5:failures.append(k)
    # Bone tails are inspected separately: FBX stores bone nodes, not Blender display lengths.
    for p,x in rt['parts'].items():
        if max(x['rest_max_m'],x['rest_reverse_max_m'])>1e-5 or x['weight_max']>1e-6 or not x['armature_modifier'] or x['triangles_source']!=x['triangles_imported']:failures.append(p)
    if max(x for v in rt['posed_max_m'].values() for x in v.values())>1e-5:failures.append('posed')
    rt['failures']=failures;rt['passed']=not failures
    audit['roundtrip_full']=rt
    audit['protected_files_end']={str(p.relative_to(P)):digest(p) for p in [P/'ProtectiveClothing_Pickup.fbx',P/'ProtectiveClothing_Pickup.fbx.meta',P/'ProtectiveClothing_Worn.fbx.meta']}
    audit['protected_hashes_unchanged']=audit['protected_files_start']==audit['protected_files_end']
    # Keep pickup inspectable without overlapping the worn model in the viewport.
    bpy.data.collections['Reimport_Pickup'].hide_viewport=True
    bpy.data.collections['Reimport_Pickup'].hide_render=True
    bpy.ops.wm.save_as_mainfile(filepath=str(S/'FBX_Roundtrip_Review.blend'))
    audit['decision']='h5_local_clearance_3mm' if not failures else 'h5_saved_roundtrip_requires_correction'
    audit['artifacts']={str(p.relative_to(P)):digest(p) for p in [S/'ProtectiveClothing.blend',P/'ProtectiveClothing_Worn.fbx',S/'FBX_Roundtrip_Review.blend']}
    dump(audit)
    print('DELIVER_DONE',json.dumps(rt),flush=True)

# ---------- review entry point ----------
if MODE_REVIEW in {'experiments','deliver'} and (S/'JacketFinalDeliveryManifest.json').exists():
    raise RuntimeError('Final delivery exists. Review/reproduce in a separate preserved h4 workspace; do not overwrite the delivered model.')
if MODE_REVIEW=='experiments': experiment()
elif MODE_REVIEW=='render_candidates': render_candidates()
elif MODE_REVIEW=='deliver':deliver()
