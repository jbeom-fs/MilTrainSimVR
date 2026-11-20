"""Blender 5.2 Worn FBX export: keep skin Model transforms in their actual parent space.
No geometry, weights, rest bones, object transforms, AnchorManifest or Unity settings edits.
Usage: blender -b --factory-startup --python worn_node_fix.py -- candidate|candidate_import|finalize|check
"""
import bpy,sys,json,math,hashlib,struct,shutil
from pathlib import Path
from mathutils import Matrix,Vector,Euler
from io_scene_fbx import parse_fbx,fbx_utils
import numpy as np
S=Path(__file__).resolve().parent;P=S.parent;ROOT=P.parents[3]
MODE=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'candidate'
BACKUP=S/'Backups/20261104_161356_worn_nodes_before'
W=S/'WornNodeFix';W.mkdir(exist_ok=True)
JSON=W/'WornNodeFixReport.json'
PARTS=['Jacket','Trousers','Glove_L','Glove_R','Overboot_L','Overboot_R']
MESHES={p+'_Worn' for p in PARTS}
PFX='mixamorig1:'
# Import definitions only. Never run the old h4/h5 rebuilding or export entry points.
saved_argv=sys.argv[:];sys.argv=[sys.argv[0]]
exec(compile((S/'jacket_deform_fix.py').read_text(encoding='utf8').split('# ---------- run ----------')[0],str(S/'jacket_deform_fix.py'),'exec'))
sys.argv=saved_argv
MODE_NODE=MODE if MODE in {'candidate','finalize','check'} else (saved_argv[-1] if saved_argv[-1] in {'candidate','finalize','check'} else 'candidate')
# Definitions above reset MODE/S/P/OUT but do not load a blend. Recover this task's values.
MODE_NODE=saved_argv[saved_argv.index('--')+1] if '--' in saved_argv else 'candidate'
W=S/'WornNodeFix';JSON=W/'WornNodeFixReport.json'

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def hd(x):return hashlib.sha256(json.dumps(x,sort_keys=True,separators=(',',':'),default=str).encode()).hexdigest()
def rows(m):return [list(r) for r in m]
def err(a,b):return max(abs(a[i][j]-b[i][j]) for i in range(4) for j in range(4))
def save_report(r):JSON.write_text(json.dumps(r,ensure_ascii=False,indent=2,default=lambda x:x.decode('utf8',errors='replace') if isinstance(x,bytes) else str(x)),encoding='utf8')

def source(path=S/'ProtectiveClothing.blend'):
    global sc,vl,wc,pc,pa,arm,body,jo,src_basis
    bpy.ops.wm.open_mainfile(filepath=str(path))
    sc=bpy.context.scene;vl=bpy.context.view_layer
    wc=bpy.data.collections['Worn'];pc=bpy.data.collections['Pickup']
    pa=bpy.data.objects['PC_Armature'];arm=bpy.data.objects['Armature'];body=bpy.data.objects['Ch36'];jo=bpy.data.objects['Jacket_Worn']
    src_basis={pb.name:pb.matrix_basis.copy() for pb in arm.pose.bones}
    clear_pose()

def contract():
    meshes={}
    for o in bpy.data.objects:
        if o.type!='MESH':continue
        me=o.data
        meshes[o.name]={'vertices':len(me.vertices),'polygons':len(me.polygons),
            'positions':hd([v.co[:] for v in me.vertices]),
            'faces':hd([list(f.vertices) for f in me.polygons]),'edges':hd([list(e.vertices) for e in me.edges]),
            'weights':hd([sorted((o.vertex_groups[g.group].name,g.weight) for g in v.groups) for v in me.vertices]),
            'group_names':[g.name for g in o.vertex_groups],
            'uv':{uv.name:hd([x.uv[:] for x in uv.data]) for uv in me.uv_layers},
            'materials':[m.name if m else None for m in me.materials],
            'material_indices':hd([f.material_index for f in me.polygons]),
            'modifiers':[(m.name,m.type,m.object.name if hasattr(m,'object') and m.object else None,m.show_viewport,m.show_render) for m in o.modifiers]}
    objects={o.name:{'type':o.type,'parent':o.parent.name if o.parent else None,'parent_type':o.parent_type,'parent_bone':o.parent_bone,
            'local':rows(o.matrix_local),'world':rows(o.matrix_world),'basis':rows(o.matrix_basis),
            'parent_inverse':rows(o.matrix_parent_inverse),'hidden':(o.hide_render,o.hide_viewport,o.hide_get())} for o in bpy.data.objects}
    bones={o.name:{b.name:{'parent':b.parent.name if b.parent else None,'rest_local':rows(b.matrix_local),
            'head':b.head_local[:],'tail':b.tail_local[:],'deform':b.use_deform,'world':rows(o.matrix_world@b.matrix_local)} for b in o.data.bones} for o in bpy.data.objects if o.type=='ARMATURE'}
    mats={}
    for m in bpy.data.materials:
        nodes=[]
        if m.node_tree:
            for n in m.node_tree.nodes:
                inputs=[]
                for sock in n.inputs:
                    if not hasattr(sock,'default_value'):continue
                    val=sock.default_value
                    try:val=list(val)
                    except TypeError:pass
                    if isinstance(val,(str,float,int,bool,list)):inputs.append((sock.identifier,val))
                nodes.append((n.name,n.type,inputs,n.image.name if n.type=='TEX_IMAGE' and n.image else None))
        mats[m.name]={'nodes':nodes,'links':[(l.from_node.name,l.from_socket.identifier,l.to_node.name,l.to_socket.identifier) for l in m.node_tree.links] if m.node_tree else []}
    images={i.name:(i.filepath,sha_bytes(i.packed_file.data) if i.packed_file else None) for i in bpy.data.images if i.type!='RENDER_RESULT'}
    return {'objects':objects,'meshes':meshes,'bones':bones,'materials':mats,'images':images}

def sha_bytes(data):return hashlib.sha256(bytes(data)).hexdigest()
def capture_poses():
    arrays={}
    for name in ['Rest']+list(POSES):
        if name=='Rest':clear_pose()
        else:pose(name)
        for p in PARTS:arrays[name+'__'+p]=np.asarray([tuple(x) for x in evaluated(bpy.data.objects[p+'_Worn'])[0]])
    clear_pose();return arrays

def export(path,correct=True):
    pc.hide_viewport=False
    bpy.ops.object.select_all(action='DESELECT')
    for o in wc.objects:
        if o.type in {'MESH','EMPTY','ARMATURE'}:o.hide_set(False);o.select_set(True)
    original=fbx_utils.ObjectWrapper.fbx_object_tx
    corrected=[]
    def parent_space_tx(wrapper,scene_data,rest=False,rot_euler_compat=None):
        result=original(wrapper,scene_data,rest=rest,rot_euler_compat=rot_euler_compat)
        if correct and wrapper.is_object and wrapper.name in MESHES and wrapper.parented_to_armature and wrapper.parent and wrapper.parent.name=='ProtectiveClothing_Worn':
            # Stock helper returns an FBX GLOBAL transform for every skinned mesh. Here the
            # actual OO connection retains a non-armature parent, so Model Lcl must be relative
            # to that parent's FBX GLOBAL matrix. Bind poses keep the stock GLOBAL calculation.
            local=wrapper.parent.fbx_object_matrix(scene_data,rest=rest,global_space=True).inverted()@result[3]
            loc,quat,scale=local.decompose();rot=quat.to_euler('XYZ',rot_euler_compat) if rot_euler_compat else quat.to_euler('XYZ')
            corrected.append(wrapper.name)
            return loc,rot,scale,local,quat.to_matrix()
        return result
    fbx_utils.ObjectWrapper.fbx_object_tx=parent_space_tx
    try:
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY','ARMATURE'},
            global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
            use_space_transform=True,bake_space_transform=False,bake_anim=False,add_leaf_bones=False,use_custom_props=True,
            path_mode='RELATIVE',embed_textures=False,mesh_smooth_type='FACE',use_armature_deform_only=False,
            primary_bone_axis='Y',secondary_bone_axis='X',armature_nodetype='NULL')
    finally:fbx_utils.ObjectWrapper.fbx_object_tx=original
    assert not correct or set(corrected)==MESHES,corrected
    return sorted(set(corrected))

def matarray(element):return fbx_utils.array_to_matrix4(element.props[0])
def raw(path):
    tree,version=parse_fbx.parse(str(path))
    objs=next(e for e in tree.elems if e.id==b'Objects')
    byid={e.props[0]:e for e in objs.elems}
    models={i:e for i,e in byid.items() if e.id==b'Model'}
    names={i:e.props[1].decode().split('\x00')[0] for i,e in models.items()}
    props={}
    for i,e in models.items():
        prop=next((c for c in e.elems if c.id==b'Properties70'),None)
        props[i]={c.props[0].decode():list(c.props[4:]) for c in prop.elems} if prop else {}
    cons=next(e for e in tree.elems if e.id==b'Connections')
    oo=[tuple(c.props[1:3]) for c in cons.elems if c.props[0]==b'OO']
    parent={a:b for a,b in oo if a in models and (b in models or b==0)}
    def lcl(i):
        pr=props[i]
        # Export settings use XYZ local Euler; fail visibly if an unsupported pivot/pre/post transform appears.
        for k in ['PreRotation','PostRotation','RotationOffset','RotationPivot','ScalingOffset','ScalingPivot','GeometricTranslation','GeometricRotation']:
            assert all(abs(float(v))<1e-8 for v in pr.get(k,[0,0,0])),(names[i],k,pr[k])
        assert pr.get('RotationOrder',[0])[0]==0,(names[i],'rotation order')
        t=Vector(pr.get('Lcl Translation',[0,0,0]));r=Euler([math.radians(x) for x in pr.get('Lcl Rotation',[0,0,0])],'XYZ').to_matrix().to_4x4()
        scale=Matrix.Diagonal(tuple(pr.get('Lcl Scaling',[1,1,1]))+(1,))
        return Matrix.Translation(t)@r@scale
    cache={}
    def world(i):
        if i==0:return Matrix.Identity(4)
        if i not in cache:cache[i]=world(parent.get(i,0))@lcl(i)
        return cache[i]
    idof={n:i for i,n in names.items()};rootid=idof['ProtectiveClothing_Worn'];rootmat=world(rootid)
    anchors=json.loads((S/'AnchorManifest.json').read_text(encoding='utf8'))
    mesh={};attach={};geometry={};cluster_fingerprints={};bone_info={}
    for p in PARTS:
        n=p+'_Worn';i=idof[n]
        geos=[a for a,b in oo if b==i and a in byid and byid[a].id==b'Geometry']
        assert len(geos)==1
        gid=geos[0]
        skins=[a for a,b in oo if b==gid and a in byid and byid[a].id==b'Deformer' and byid[a].props[2]==b'Skin']
        assert len(skins)==1
        clusters=[a for a,b in oo if b==skins[0] and a in byid and byid[a].id==b'Deformer']
        bind=[];cf={}
        for cid in clusters:
            e=byid[cid];children={c.id:c for c in e.elems}
            T=matarray(children[b'Transform']);L=matarray(children[b'TransformLink'])
            bind.append(L@T)
            boneids=[a for a,b in oo if b==cid and a in models];assert len(boneids)==1
            cf[names[boneids[0]]]={k.decode():list(c.props[0]) for k,c in children.items() if k in {b'Transform',b'TransformLink',b'TransformAssociateModel',b'Indexes',b'Weights'}}
        mesh[n]={'parent':names.get(parent[i]),'Lcl':props[i],
            'root_relative':rows(rootmat.inverted()@world(i)),
            'root_relative_identity_error':err(rootmat.inverted()@world(i),Matrix.Identity(4)),
            'global':rows(world(i)),'clusters':len(clusters),
            'node_vs_skin_bind_max':max(err(world(i),b) for b in bind),
            'skin_bind_vs_root_max':max(err(rootmat,b) for b in bind)}
        cluster_fingerprints[n]=hd(cf)
        def encode(e,top=False):
            def val(v):
                if isinstance(v,bytes):return {'bytes':v.hex()}
                if isinstance(v,(str,int,float)):return v
                return list(v)
            return [e.id.decode(),[val(v) for v in (e.props[1:] if top else e.props)], [encode(c) for c in e.elems]]
        geometry[n]=hd(encode(byid[gid],True))
        an='Attach_'+n;aid=idof[an];actual=rootmat.inverted()@world(aid)
        expected=Matrix(anchors[an]['matrix_world_blender_m'])
        attach[an]={'parent':names.get(parent[aid]),'expected_parent':anchors[an]['parent'],
            'Lcl':props[aid],'common_model_matrix':rows(actual),'expected_model_matrix':rows(expected),
            'position_error_mm':(actual.translation-expected.translation).length*1000,
            'matrix_error':err(actual,expected)}
    for i,n in names.items():
        if models[i].props[2]==b'LimbNode':bone_info[n]={'parent':names.get(parent[i]),'Lcl':props[i],'global':rows(world(i))}
    gs=next(e for e in tree.elems if e.id==b'GlobalSettings')
    pr=next(e for e in gs.elems if e.id==b'Properties70')
    settings={c.props[0].decode():list(c.props[4:]) for c in pr.elems}
    return {'path':str(path),'sha256':sha(path),'version':version,'root':{'Lcl':props[rootid],'global':rows(rootmat)},
        'armature':{'Lcl':props[idof['PC_Armature']],'global':rows(world(idof['PC_Armature']))},
        'settings':settings,'model_parents':{n:names.get(parent.get(i,0)) for i,n in names.items()},
        'meshes':mesh,'attach':attach,'bones':bone_info,'geometry_fingerprints':geometry,'skin_cluster_fingerprints':cluster_fingerprints}

def raw_valid(r):
    return len(r['bones'])==65 and all(x['parent']=='ProtectiveClothing_Worn' and x['root_relative_identity_error']<=1e-5 and x['node_vs_skin_bind_max']<=1e-5 and x['skin_bind_vs_root_max']<=1e-5 for x in r['meshes'].values()) and all(x['parent']==x['expected_parent'] and x['position_error_mm']<=1 and x['matrix_error']<=1e-5 for x in r['attach'].values())

def candidate():
    original=raw(P/'ProtectiveClothing_Worn.fbx')
    r={'version':'2026-11-04-worn-node-fix','backup':str(BACKUP.relative_to(S)),
       'tolerances':{'matrix_element':1e-5,'attach_mm':1,'posed_position_m':1e-5},'before':original}
    r['start_hashes']={str(p.relative_to(ROOT)):sha(p) for p in [S/'ProtectiveClothing.blend',P/'ProtectiveClothing_Worn.fbx',P/'ProtectiveClothing_Worn.fbx.meta',P/'ProtectiveClothing_Pickup.fbx',P/'ProtectiveClothing_Pickup.fbx.meta',S/'AnchorManifest.json']}
    source();base=contract()
    (W/'H5SourceContract.json').write_text(json.dumps(base,ensure_ascii=False,indent=1),encoding='utf8')
    np.savez_compressed(W/'H5PoseReference.npz',**capture_poses())
    r['export_corrected_meshes']=export(W/'Candidate_Worn.fbx')
    after=raw(W/'Candidate_Worn.fbx');r['candidate']=after
    r['raw_candidate_passed']=raw_valid(after)
    r['candidate_preserves_fbx_payload']={k:original[k]==after[k] for k in ['model_parents','bones','geometry_fingerprints','skin_cluster_fingerprints']}
    r['root_conversion_preserved']=original['root']==after['root'];r['armature_compensation_preserved']=original['armature']==after['armature']
    r['source_data_untouched_by_export']=base['meshes']==contract()['meshes'] and base['bones']==contract()['bones']
    save_report(r)
    print('NODE_CANDIDATE',r['raw_candidate_passed'],r['candidate_preserves_fbx_payload'],flush=True)

def verify_import(path,outfile,normalized_file):
    global sc,vl,pa
    base=json.loads((W/'H5SourceContract.json').read_text(encoding='utf8'))
    refs=np.load(W/'H5PoseReference.npz')
    bpy.ops.wm.read_homefile(use_empty=True);sc=bpy.context.scene;vl=bpy.context.view_layer
    bpy.ops.import_scene.fbx(filepath=str(path),ignore_leaf_bones=False,automatic_bone_orientation=False,use_custom_props=True)
    pa=bpy.data.objects['PC_Armature'];root=bpy.data.objects['ProtectiveClothing_Worn']
    expected_objects={n:x for n,x in base['objects'].items() if n=='ProtectiveClothing_Worn' or n=='PC_Armature' or n in MESHES or n.startswith('Attach_') and n.endswith('_Worn')}
    out={'path':str(path),'missing_objects':sorted(set(expected_objects)-set(bpy.data.objects.keys())),
        'extra_objects':sorted(set(bpy.data.objects.keys())-set(expected_objects)),
        'source_bones':len(base['bones']['PC_Armature']),'imported_bones':len(pa.data.bones),
        'missing_bones':sorted(set(base['bones']['PC_Armature'])-set(pa.data.bones.keys())),
        'extra_bones':sorted(set(pa.data.bones.keys())-set(base['bones']['PC_Armature'])),
        'bone_parent_errors':[],'bone_rest_world_matrix_max':0,'parts':{},'poses':{},'attach':{},'raw_object_parent_differences':[],
        'raw_local_matrix_max':0,'raw_world_matrix_max':0}
    for n,x in expected_objects.items():
        o=bpy.data.objects[n]
        parent=o.parent.name if o.parent else None
        if parent!=x['parent']:out['raw_object_parent_differences'].append({'object':n,'expected':x['parent'],'actual':parent})
        out['raw_local_matrix_max']=max(out['raw_local_matrix_max'],err(o.matrix_local,Matrix(x['local'])))
        out['raw_world_matrix_max']=max(out['raw_world_matrix_max'],err(o.matrix_world,Matrix(x['world'])))
    for b in pa.data.bones:
        x=base['bones']['PC_Armature'][b.name]
        if (b.parent.name if b.parent else None)!=x['parent']:out['bone_parent_errors'].append(b.name)
        out['bone_rest_world_matrix_max']=max(out['bone_rest_world_matrix_max'],err(pa.matrix_world@b.matrix_local,Matrix(x['world'])))
    anchors=json.loads((S/'AnchorManifest.json').read_text(encoding='utf8'))
    for n,x in anchors.items():
        if not n.endswith('_Worn') or not n.startswith('Attach_'):continue
        o=bpy.data.objects[n];common=root.matrix_world.inverted()@o.matrix_world
        out['attach'][n]={'parent':o.parent.name if o.parent else None,'expected_parent':x['parent'],
            'position_error_mm':(common.translation-Matrix(x['matrix_world_blender_m']).translation).length*1000,
            'common_model_matrix':rows(common)}
    for p in PARTS:
        o=bpy.data.objects[p+'_Worn'];me=o.data;x=base['meshes'][o.name]
        weights=[sorted((o.vertex_groups[g.group].name,g.weight) for g in v.groups if g.weight>0) for v in me.vertices]
        out['parts'][p]={'vertices':len(me.vertices),'source_vertices':x['vertices'],'polygons':len(me.polygons),'source_polygons':x['polygons'],
            'weight_fingerprint_equal':hd(weights)==x['weights'],
            'uv_fingerprints_equal':{uv.name:hd([u.uv[:] for u in uv.data]) for uv in me.uv_layers}==x['uv'],
            'material_names_equal':[m.name if m else None for m in me.materials]==x['materials'],
            'material_indices_equal':hd([f.material_index for f in me.polygons])==x['material_indices'],
            'armature_modifier':any(m.type=='ARMATURE' and m.object==pa for m in o.modifiers)}
    def measure():
        res={}
        for name in ['Rest']+list(POSES):
            for pb in pa.pose.bones:pb.matrix_basis=Matrix.Identity(4)
            vl.update()
            if name!='Rest':POSES[name](pa)
            res[name]={}
            for p in PARTS:
                q=np.asarray([tuple(x) for x in evaluated(bpy.data.objects[p+'_Worn'])[0]])
                ref=refs[name+'__'+p]
                assert q.shape==ref.shape,(p,q.shape,ref.shape)
                res[name][p]=float(np.linalg.norm(q-ref,axis=1).max())
        for pb in pa.pose.bones:pb.matrix_basis=Matrix.Identity(4)
        vl.update();return res
    out['poses']=measure()
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(outfile))
    out['raw_scene']=str(outfile.relative_to(S))
    # Keep original importer output above. A second review, if needed, restores source parents only.
    for item in out['raw_object_parent_differences']:
        o=bpy.data.objects[item['object']]
        assert o.type=='MESH' and o.parent==pa
        world=o.matrix_world.copy();o.parent=bpy.data.objects[item['expected']]
        o.matrix_parent_inverse=Matrix.Identity(4);o.matrix_world=world
    vl.update()
    out['normalized_local_matrix_max']=max(err(bpy.data.objects[n].matrix_local,Matrix(x['local'])) for n,x in expected_objects.items())
    out['normalized_world_matrix_max']=max(err(bpy.data.objects[n].matrix_world,Matrix(x['world'])) for n,x in expected_objects.items())
    out['normalized_poses']=measure()
    bpy.ops.wm.save_as_mainfile(filepath=str(normalized_file))
    out['normalized_scene']=str(normalized_file.relative_to(S))
    failures=[]
    for key in ['missing_objects','extra_objects','missing_bones','extra_bones','bone_parent_errors']:
        if out[key]:failures.append(key)
    if out['imported_bones']!=65:failures.append('bone_count')
    for key in ['bone_rest_world_matrix_max','raw_world_matrix_max','normalized_local_matrix_max','normalized_world_matrix_max']:
        if out[key]>1e-5:failures.append(key)
    for p,x in out['parts'].items():
        if x['vertices']!=x['source_vertices'] or x['polygons']!=x['source_polygons'] or not all(x[k] for k in ['weight_fingerprint_equal','uv_fingerprints_equal','material_names_equal','material_indices_equal','armature_modifier']):failures.append(p)
    if any(v>1e-5 for m in ['poses','normalized_poses'] for x in out[m].values() for v in x.values()):failures.append('deformation')
    if any(x['position_error_mm']>1 or x['parent']!=x['expected_parent'] for x in out['attach'].values()):failures.append('attach')
    out['raw_parent_local_check_passed']=not out['raw_object_parent_differences'] and out['raw_local_matrix_max']<=1e-5
    out['failures']=failures;out['equivalent_contract_passed']=not failures
    return out

def candidate_import():
    r=json.loads(JSON.read_text(encoding='utf8'))
    # Re-evaluate all binary payload fingerprints, including the complete geometry arrays.
    r['before']=raw(BACKUP/'ProtectiveClothing_Worn.fbx');r['candidate']=raw(W/'Candidate_Worn.fbx')
    r['candidate_preserves_fbx_payload']={k:r['before'][k]==r['candidate'][k] for k in ['model_parents','bones','geometry_fingerprints','skin_cluster_fingerprints']}
    r['candidate_import']=verify_import(W/'Candidate_Worn.fbx',W/'Candidate_RawImport.blend',W/'Candidate_NormalizedReview.blend')
    save_report(r)
    print('CANDIDATE_IMPORT',r['candidate_import']['equivalent_contract_passed'],r['candidate_import']['failures'],flush=True)

def finalize():
    r=json.loads(JSON.read_text(encoding='utf8'))
    assert r['raw_candidate_passed'] and all(r['candidate_preserves_fbx_payload'].values()) and r['candidate_import']['equivalent_contract_passed']
    for p in [S/'ProtectiveClothing.blend',P/'ProtectiveClothing_Worn.fbx']:
        assert sha(p)==r['start_hashes'][str(p.relative_to(ROOT))],('External source/FBX change; preserved without overwrite',str(p))
    meta=(P/'ProtectiveClothing_Worn.fbx.meta').read_text(encoding='utf8')
    assert '\n  animationType: 2\n' in meta,'Generic setting changed externally; do not change .meta'
    source();base=json.loads((W/'H5SourceContract.json').read_text(encoding='utf8'))
    assert hd(contract())==hd(base),'Source semantic data changed since candidate snapshot'
    export_contract={'exporter':'worn_node_fix.py','script_sha256':sha(S/'worn_node_fix.py'),
        'method':'Only skinned non-armature-parent Model Lcl is computed in the actual FBX parent space. Bind pose/geometry/bones unchanged.',
        'normal_axis_conversion':'root -90 X; PC_Armature +90 X, scale .01 preserved',
        'do_not_use_stock_export_for_this_hierarchy':True,
        'reproduce':'Blender CLI worn_node_fix.py -- candidate, then -- candidate_import, then -- finalize',
        'source_contract':'WornNodeFix/H5SourceContract.json','pose_reference':'WornNodeFix/H5PoseReference.npz'}
    text=bpy.data.texts.get('Worn_FBX_Export_Contract.json') or bpy.data.texts.new('Worn_FBX_Export_Contract.json')
    text.clear();text.write(json.dumps(export_contract,ensure_ascii=False,indent=2))
    sc['Worn_FBX_Export_Method']='worn_node_fix.py: actual-parent-space Model Lcl; see Worn_FBX_Export_Contract.json text'
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(S/'ProtectiveClothing.blend'),relative_remap=False)
    r['source_saved']=True;save_report(r)
    r['export_corrected_meshes_final']=export(P/'ProtectiveClothing_Worn.fbx')
    r['after']=raw(P/'ProtectiveClothing_Worn.fbx')
    r['raw_final_passed']=raw_valid(r['after'])
    r['final_preserves_fbx_payload']={k:r['before'][k]==r['after'][k] for k in ['model_parents','bones','geometry_fingerprints','skin_cluster_fingerprints']}
    r['final_root_conversion_preserved']=r['before']['root']==r['after']['root']
    r['final_armature_compensation_preserved']=r['before']['armature']==r['after']['armature']
    r['export_complete']=True;save_report(r)
    # Reopen exactly the delivered source before validating its semantic data and poses.
    source();now=contract();r['saved_source_contract_equal']=hd(now)==hd(base)
    current_poses=capture_poses();refs=np.load(W/'H5PoseReference.npz')
    r['saved_source_pose_max_m']={k:float(np.linalg.norm(v-refs[k],axis=1).max()) for k,v in current_poses.items()}
    r['final_import']=verify_import(P/'ProtectiveClothing_Worn.fbx',S/'FBX_Roundtrip_Review.blend',W/'Final_NormalizedReview.blend')
    r['protected_end_hashes']={k:sha(ROOT/k) for k in r['start_hashes'] if not k.endswith('ProtectiveClothing.blend') and not k.endswith('ProtectiveClothing_Worn.fbx')}
    r['protected_files_unchanged']=all(v==r['start_hashes'][k] for k,v in r['protected_end_hashes'].items())
    r['external_changes_observed']=[{'path':k,'start':r['start_hashes'][k],'end':v,'action':'preserved; no restoration'} for k,v in r['protected_end_hashes'].items() if v!=r['start_hashes'][k]]
    r['complete']=r['raw_final_passed'] and all(r['final_preserves_fbx_payload'].values()) and r['final_import']['equivalent_contract_passed'] and r['saved_source_contract_equal'] and max(r['saved_source_pose_max_m'].values())<=1e-5 and r['protected_files_unchanged']
    r['artifacts']={str(p.relative_to(ROOT)):{'sha256':sha(p),'bytes':p.stat().st_size} for p in [S/'ProtectiveClothing.blend',P/'ProtectiveClothing_Worn.fbx',S/'FBX_Roundtrip_Review.blend',W/'Final_NormalizedReview.blend']}
    r['unity_user_execution']='미확인: Unity 임포트·1인칭·거울·VR 품질. Unity 실행·테스트·빌드·커밋·Push 하지 않음.'
    save_report(r)
    print('WORN_NODE_FINAL',r['complete'],r['final_import']['failures'],flush=True)

def check():
    """Reproduce to additional review files only; never overwrite the delivered source or FBX."""
    r=json.loads(JSON.read_text(encoding='utf8'));assert r['complete']
    expected_artifacts={k:v['sha256'] for k,v in r['artifacts'].items()}
    assert all(sha(ROOT/k)==v for k,v in expected_artifacts.items()),'Delivered artifact changed externally; preserve and inspect before reproducing'
    source();base=json.loads((W/'H5SourceContract.json').read_text(encoding='utf8'))
    out={'source_contract_equal':hd(contract())==hd(base)}
    poses=capture_poses();refs=np.load(W/'H5PoseReference.npz')
    out['source_pose_max_m']=max(float(np.linalg.norm(v-refs[k],axis=1).max()) for k,v in poses.items())
    export(W/'Reproduced_Worn.fbx')
    reproduced=json.loads(json.dumps(raw(W/'Reproduced_Worn.fbx'),default=lambda x:x.decode('utf8',errors='replace') if isinstance(x,bytes) else str(x)))
    out['property_comparison_encoding']='FBX byte string properties decoded as UTF-8, matching persisted JSON; numeric arrays and matrices compared without rounding.'
    out['raw_fbx_passed']=raw_valid(reproduced)
    out['reproduces_delivered_payload']={k:reproduced[k]==r['after'][k] for k in ['root','armature','model_parents','bones','geometry_fingerprints','skin_cluster_fingerprints','meshes','attach']}
    out['reimport']=verify_import(W/'Reproduced_Worn.fbx',W/'Reproduced_RawImport.blend',W/'Reproduced_NormalizedReview.blend')
    out['delivered_files_unchanged']=all(sha(ROOT/k)==v for k,v in expected_artifacts.items())
    out['passed']=out['source_contract_equal'] and out['source_pose_max_m']<=1e-5 and out['raw_fbx_passed'] and all(out['reproduces_delivered_payload'].values()) and out['reimport']['equivalent_contract_passed'] and out['delivered_files_unchanged']
    (W/'ReproductionCheck.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf8')
    print('REPRODUCTION_CHECK',out['passed'],flush=True)

if __name__=='__main__':
    if MODE_NODE in {'candidate','candidate_import','finalize'} and JSON.exists() and json.loads(JSON.read_text(encoding='utf8')).get('complete'):
        raise RuntimeError('Completed delivery exists. Use -- check for non-destructive reproduction; start a fresh backed-up workspace for new changes.')
    if MODE_NODE=='candidate':candidate()
    elif MODE_NODE=='candidate_import':candidate_import()
    elif MODE_NODE=='finalize':finalize()
    elif MODE_NODE=='check':check()
