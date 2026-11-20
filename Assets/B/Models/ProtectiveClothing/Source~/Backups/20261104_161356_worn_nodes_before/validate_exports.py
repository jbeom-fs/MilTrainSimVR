import bpy,json,math,hashlib
import bmesh
from pathlib import Path
from mathutils import Vector
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
P=Path(__file__).resolve().parent.parent;S=P/'Source~'
bpy.ops.wm.open_mainfile(filepath=str(S/'ProtectiveClothing.blend'))
bpy.data.collections['Pickup'].hide_viewport=False
bpy.context.view_layer.update()
src={}
for cname in ['Worn','Pickup']:
    for o in bpy.data.collections[cname].objects:
        src[o.name]={'type':o.type,'matrix':o.matrix_world.copy(),'parent':o.parent.name if o.parent else None}
        if o.type=='MESH':
            src[o.name].update(vertices=[o.matrix_world@v.co for v in o.data.vertices],triangles=len(o.data.polygons),uv=len(o.data.uv_layers),materials=[m.name for m in o.data.materials])
body=bpy.data.objects['Ch36'];dg=bpy.context.evaluated_depsgraph_get();ev=body.evaluated_get(dg);me=ev.to_mesh()
bmverts=[ev.matrix_world@v.co for v in me.vertices];bmfaces=[list(f.vertices) for f in me.polygons]
bvh=BVHTree.FromPolygons(bmverts,bmfaces,all_triangles=False);ev.to_mesh_clear()
fit={}
for n,d in src.items():
    if d['type']!='MESH' or not n.endswith('_Worn'):continue
    inside=[];md=10
    for p in d['vertices']:
        loc,no,idx,dist=bvh.find_nearest(p); signed=(p-loc).dot(no);md=min(md,signed)
        if signed<-.001:inside.append({'p':list(p),'depth_m':-signed})
    fit[n]={'samples':len(d['vertices']),'samples_inside_reference_over_1mm':len(inside),'deepest_signed_distance_m':md,'worst_samples':sorted(inside,key=lambda x:-x['depth_m'])[:12]}
report={'source':'ProtectiveClothing.blend','fit_method':'Clothing vertices nearest reference surface signed normal; diagnostic only, not proof of watertight containment. Original reference normals used.','fit':fit,'roundtrip':{},'original_hashes':{}}
report['mesh_health']={}
for n,d in src.items():
    if d['type']!='MESH':continue
    o=bpy.data.objects[n];bm=bmesh.new();bm.from_mesh(o.data)
    seen=set();duplicates=0
    for f in bm.faces:
        key=tuple(sorted(tuple(round(a,6) for a in v.co) for v in f.verts))
        if key in seen:duplicates+=1
        seen.add(key)
    # Closed connected shells should have positive volume; label quads are deliberately open.
    remaining=set(bm.faces);volumes=[]
    while remaining:
        seed=remaining.pop();part={seed};queue=[seed]
        while queue:
            f=queue.pop()
            for e in f.edges:
                for t in e.link_faces:
                    if t in remaining:remaining.remove(t);part.add(t);queue.append(t)
        edges={e for f in part for e in f.edges}
        if all(len(e.link_faces)==2 for e in edges):
            vol=sum(f.verts[0].co.dot(f.verts[1].co.cross(f.verts[2].co))/6 for f in part)
            volumes.append(vol)
    report['mesh_health'][n]={'spatial_duplicate_faces_1um':duplicates,'closed_island_signed_volumes_m3':volumes,'inward_closed_islands':sum(v<-1e-9 for v in volumes),'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_nonboundary_edges':sum(not e.is_manifold and not e.is_boundary for e in bm.edges),'degenerate_faces':sum(f.calc_area()<1e-12 for f in bm.faces)}
    bm.free()
anchors={n:{'parent':d['parent'],'matrix_world_blender_m':[list(r) for r in d['matrix']]} for n,d in src.items() if d['type']=='EMPTY'}
(S/'AnchorManifest.json').write_text(json.dumps(anchors,indent=2))
for k,expected in json.loads((S/'OriginalAssetHashes.json').read_text()).items():report['original_hashes'][k]={'expected':expected,'actual':hashlib.sha256(Path(k).read_bytes()).hexdigest(),'unchanged':hashlib.sha256(Path(k).read_bytes()).hexdigest()==expected}
bpy.ops.wm.read_factory_settings(use_empty=True)
for state in ['Worn','Pickup']:
    scene=bpy.data.scenes.new('Reimport_'+state);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    bpy.ops.import_scene.fbx(filepath=str(P/('ProtectiveClothing_'+state+'.fbx')),use_anim=False)
    objects=list(scene.objects);errors=[];details={}
    for o in objects:
        if o.name not in src:errors.append('Unexpected object '+o.name);continue
        d=src[o.name];err=max(abs(o.matrix_world[i][j]-d['matrix'][i][j]) for i in range(4) for j in range(4))
        out={'type':o.type,'matrix_max_abs_error':err,'parent_matches':(o.parent.name if o.parent else None)==d['parent'],'scale':list(o.scale)}
        if err>1e-5:errors.append(o.name+' matrix mismatch')
        if not out['parent_matches']:errors.append(o.name+' parent mismatch')
        if o.type=='MESH':
            kd=KDTree(len(d['vertices']))
            for i,p in enumerate(d['vertices']):kd.insert(p,i)
            kd.balance();delta=max(kd.find(o.matrix_world@v.co)[2] for v in o.data.vertices)
            reverse=KDTree(len(o.data.vertices))
            for i,v in enumerate(o.data.vertices):reverse.insert(o.matrix_world@v.co,i)
            reverse.balance();delta=max(delta,max(reverse.find(p)[2] for p in d['vertices']))
            out.update(triangles=len(o.data.polygons),triangles_match=len(o.data.polygons)==d['triangles'],max_vertex_error_m=delta,uv_layers=len(o.data.uv_layers),material_slots=len(o.data.materials))
            if delta>1e-5 or not out['triangles_match']:errors.append(o.name+' geometry mismatch')
        details[o.name]=out
    expected={n for n in src if n.endswith('_'+state) or n=='ProtectiveClothing_'+state}
    missing=expected-set(o.name for o in objects)
    if missing:errors.append('Missing '+str(missing))
    report['roundtrip'][state]={'passed':not errors,'object_count':len(objects),'mesh_count':sum(o.type=='MESH' for o in objects),'errors':errors,'details':details,'no_reference_rig_camera_light':all(o.type in {'MESH','EMPTY'} for o in objects)}
    from io_scene_fbx import parse_fbx
    tree,version=parse_fbx.parse(str(P/('ProtectiveClothing_'+state+'.fbx')))
    gs=next(e for e in tree.elems if e.id==b'GlobalSettings')
    props=next(e for e in gs.elems if e.id==b'Properties70')
    wanted={b'UnitScaleFactor',b'OriginalUnitScaleFactor',b'UpAxis',b'UpAxisSign',b'FrontAxis',b'FrontAxisSign',b'CoordAxis',b'CoordAxisSign'}
    report['roundtrip'][state]['fbx_global_settings']={e.props[0].decode():e.props[-1] for e in props.elems if e.id==b'P' and e.props[0] in wanted}
    # Make reference-free round-trip inspection scene easy to inspect.
    scene.world=bpy.data.worlds.new('Reimport_World_'+state)
(S/'ValidationReport.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(S/'FBX_Roundtrip_Review.blend'))
print(json.dumps({'roundtrip':{k:{a:v for a,v in d.items() if a!='details'} for k,d in report['roundtrip'].items()},'fit':fit}))
