import bpy,bmesh,json,hashlib,struct
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
S=Path(__file__).resolve().parent;P=S.parent;B=Path((S/'UpgradeBackupPath.txt').read_text());base=json.loads((S/'UpgradeBaseline.json').read_text())
expected_anchors=json.loads((B/'Source~'/'AnchorManifest.json').read_text())
backup=json.loads((B/'BackupManifest.json').read_text());report={'contract':{},'health':{},'fit':{},'roundtrip':{},'preservation':{}}
bpy.ops.wm.open_mainfile(filepath=str(S/'ProtectiveClothing.blend'));bpy.data.collections['Pickup'].hide_viewport=False;bpy.context.view_layer.update()
names=[k+'_'+state for state in ['Worn','Pickup'] for k in ['Jacket','Trousers','Glove_L','Glove_R','Overboot_L','Overboot_R']]
def digest(o):
    if o.type=='MESH':return hashlib.sha256(b''.join(struct.pack('<3f',*v.co) for v in o.data.vertices)).hexdigest()
    if o.type=='ARMATURE':return hashlib.sha256(str([(b.name,b.parent.name if b.parent else None,list(b.head_local),list(b.tail_local)) for b in o.data.bones]).encode()).hexdigest()
    return None
def materr(a,b):return max(abs(a[i][j]-b[i][j]) for i in range(4) for j in range(4))
errs=[];src={};actual_anchors={}
for n,d in base['objects'].items():
    o=bpy.data.objects.get(n)
    if o is None:errs.append(n+' missing');continue
    parent=o.parent.name if o.parent else None
    if parent!=d['parent']:errs.append(n+' parent changed')
    if materr(o.matrix_local,d['matrix_local'])>1e-8:errs.append(n+' local matrix changed')
    if o.type!=d['type']:errs.append(n+' object type changed')
    if o.type=='MESH' and d.get('mesh_name') and o.data.name!=d['mesh_name']:errs.append(n+' mesh datablock name changed')
    if n not in names and digest(o)!=d['geometry_digest']:errs.append(n+' non-equipment geometry changed')
(S/'MeshNamePreservation.json').write_text(json.dumps({n:{'before':base['objects'][n].get('mesh_name'),'after':bpy.data.objects[n].data.name} for n in names},indent=2))
for n,d in expected_anchors.items():
    o=bpy.data.objects[n];error=materr(o.matrix_world,d['matrix_world_blender_m'])
    if error>1e-8 or (o.parent.name if o.parent else None)!=d['parent']:errs.append(n+' anchor changed')
    actual_anchors[n]={'parent':o.parent.name if o.parent else None,'matrix_world_blender_m':[list(r) for r in o.matrix_world]}
if set(bpy.data.objects.keys())!=set(base['objects']):errs.append('Object inventory changed')
report['contract']={'passed':not errs,'errors':errs,'equipment_mesh_count':len(names),'all_original_objects':len(base['objects']),'anchor_count':len(expected_anchors),'anchor_max_error':max(materr(bpy.data.objects[n].matrix_world,d['matrix_world_blender_m']) for n,d in expected_anchors.items()),'units':{'system':bpy.context.scene.unit_settings.system,'scale_length':bpy.context.scene.unit_settings.scale_length}}
(S/'ActualAnchorManifest_Upgrade.json').write_text(json.dumps(actual_anchors,indent=2))
body=bpy.data.objects['Ch36'];ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());me=ev.to_mesh();rv=[ev.matrix_world@v.co for v in me.vertices];rf=[tuple(p.vertices) for p in me.polygons];ref=BVHTree.FromPolygons(rv,rf);ev.to_mesh_clear()
for cname in ['Worn','Pickup']:
    for o in bpy.data.collections[cname].objects:
        src[o.name]={'type':o.type,'parent':o.parent.name if o.parent else None,'matrix':o.matrix_world.copy()}
        if o.type!='MESH':continue
        src[o.name].update(vertices=[o.matrix_world@v.co for v in o.data.vertices],triangles=len(o.data.polygons),uvs=[tuple(l.uv) for l in o.data.uv_layers.active.data])
        bm=bmesh.new();bm.from_mesh(o.data);seen=set();dup=0
        for f in bm.faces:
            key=tuple(sorted(tuple(round(a,6) for a in v.co) for v in f.verts))
            if key in seen:dup+=1
            seen.add(key)
        remaining=set(bm.faces);volumes=[]
        while remaining:
            seed=remaining.pop();part={seed};q=[seed]
            while q:
                for edge in q.pop().edges:
                    for f in edge.link_faces:
                        if f in remaining:remaining.remove(f);part.add(f);q.append(f)
            if all(len(e.link_faces)==2 for f in part for e in f.edges):volumes.append(sum(f.verts[0].co.dot(f.verts[1].co.cross(f.verts[2].co))/6 for f in part))
        report['health'][o.name]={'duplicate_faces_1um':dup,'degenerate_faces':sum(f.calc_area()<1e-12 for f in bm.faces),'nonmanifold_nonboundary_edges':sum(not e.is_manifold and not e.is_boundary for e in bm.edges),'boundary_edges':sum(e.is_boundary for e in bm.edges),'inward_closed_islands':sum(v<-1e-9 for v in volumes),'negative_scale':any(v<0 for v in o.scale),'material_slots':len(o.data.materials),'uv_layers':len(o.data.uv_layers),'active_modifiers':[m.type for m in o.modifiers]};bm.free()
        if o.name.endswith('_Worn'):
            inside=[];minimum=10
            for v in o.data.vertices:
                loc,no,idx,dist=ref.find_nearest(v.co);signed=(v.co-loc).dot(no);minimum=min(minimum,signed)
                if signed<-.001:inside.append({'point':list(v.co),'depth_m':-signed})
            report['fit'][o.name]={'samples':len(o.data.vertices),'inside_over_1mm':len(inside),'minimum_signed_m':minimum,'worst_samples':sorted(inside,key=lambda p:-p['depth_m'])[:12]}
# Preserve original metadata byte for byte, and the old AnchorManifest itself.
for rel,record in backup.items():
    if rel.endswith('.meta') or rel.replace('\\','/')=='Source~/AnchorManifest.json':
        f=P/rel;report['preservation'][rel]={'unchanged':f.exists() and hashlib.sha256(f.read_bytes()).hexdigest()==record['sha256']}
originalhash=json.loads((S/'OriginalAssetHashes.json').read_text())
for f in [P.parent/'Mannequin'/'Mannequin.fbx',P.parent/'K1GasMask'/'K1_GasMask.fbx']:
    expected=originalhash[str(f).replace('\\','/')] if str(f).replace('\\','/') in originalhash else originalhash.get(str(f))
    report['preservation'][str(f)]={'unchanged':hashlib.sha256(f.read_bytes()).hexdigest()==expected}
report['source_textures']={i.name:{'path':i.filepath,'size':list(i.size),'packed':bool(i.packed_file)} for i in bpy.data.images if i.name.startswith('PC_')}
bpy.ops.wm.read_factory_settings(use_empty=True)
for state in ['Worn','Pickup']:
    scene=bpy.data.scenes.new('Reimport_'+state);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    bpy.ops.import_scene.fbx(filepath=str(P/('ProtectiveClothing_'+state+'.fbx')),use_anim=False)
    errors=[];items={};expected={n for n in src if n.endswith('_'+state)}
    if set(o.name for o in scene.objects)!=expected:errors.append('Object name/inventory mismatch')
    for o in scene.objects:
        if o.name not in src:continue
        d=src[o.name];error=materr(o.matrix_world,d['matrix']);parent=o.parent.name if o.parent else None
        item={'type':o.type,'parent':parent,'max_matrix_error':error}
        if error>1e-5 or parent!=d['parent']:errors.append(o.name+' transform/parent mismatch')
        if o.type=='MESH':
            kd=KDTree(len(d['vertices']))
            for i,v in enumerate(d['vertices']):kd.insert(v,i)
            kd.balance();delta=max(kd.find(o.matrix_world@v.co)[2] for v in o.data.vertices)
            rev=KDTree(len(o.data.vertices))
            for i,v in enumerate(o.data.vertices):rev.insert(o.matrix_world@v.co,i)
            rev.balance();delta=max(delta,max(rev.find(v)[2] for v in d['vertices']))
            uv_source=set((round(u,5),round(v,5)) for u,v in d['uvs']);uv_new=set((round(l.uv.x,5),round(l.uv.y,5)) for l in o.data.uv_layers.active.data)
            uv_ok=uv_source==uv_new
            item.update(triangles=len(o.data.polygons),max_vertex_error_m=delta,uv_values_match=uv_ok,material_slots=len(o.data.materials),image_paths=list(set(n.image.filepath for m in o.data.materials if m.use_nodes for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image)))
            if len(o.data.polygons)!=d['triangles'] or delta>1e-5:errors.append(o.name+' mesh mismatch')
            if not uv_ok:errors.append(o.name+' UV mismatch')
            if len(o.data.materials)!=1:errors.append(o.name+' material slot mismatch')
            if not all(Path(bpy.path.abspath(p)).exists() for p in item['image_paths']):errors.append(o.name+' texture path missing')
        items[o.name]=item
    from io_scene_fbx import parse_fbx
    tree,ver=parse_fbx.parse(str(P/('ProtectiveClothing_'+state+'.fbx')));gs=next(e for e in tree.elems if e.id==b'GlobalSettings');props=next(e for e in gs.elems if e.id==b'Properties70')
    units={e.props[0].decode():e.props[-1] for e in props.elems if e.id==b'P' and e.props[0] in {b'UnitScaleFactor',b'UpAxis',b'UpAxisSign',b'FrontAxis',b'FrontAxisSign'}}
    report['roundtrip'][state]={'passed':not errors,'errors':errors,'objects':items,'mesh_count':sum(o.type=='MESH' for o in scene.objects),'fbx_settings':units,'no_reference_camera_lights_rig':all(o.type in {'MESH','EMPTY'} for o in scene.objects)}
(S/'UpgradeValidation.json').write_text(json.dumps(report,indent=2))
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(S/'FBX_Roundtrip_Review.blend'))
print(json.dumps({'contract':report['contract'],'roundtrip':{n:{k:v for k,v in r.items() if k!='objects'} for n,r in report['roundtrip'].items()},'fit':report['fit'],'preservation_all':all(d['unchanged'] for d in report['preservation'].values()),'health':report['health']}))
