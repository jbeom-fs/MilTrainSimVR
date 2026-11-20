"""Deterministic Blender-only mesh production, FBX export and round-trip audit.
Coordinates in the design functions are Unity (X right, Y up, front -Z).
Blender coordinates are (X, -Z, Y); FBX bakes the axis conversion.
"""
import bpy, bmesh, math, json, pathlib
from mathutils import Vector
BASE = pathlib.Path(__file__).resolve().parent.parent
SRC = BASE/'Source~'
PRE = SRC/'Previews'
PRE.mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene
scene.name='TrainingMirror_Production'
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1.0
scene.unit_settings.length_unit='METERS'
mat=bpy.data.materials.new('TrainingMirror_MatteDarkGrayMetal')
mat.use_nodes=True
bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
bs.inputs['Base Color'].default_value=(0.065,0.075,0.082,1)
bs.inputs['Metallic'].default_value=0.8
bs.inputs['Roughness'].default_value=0.68
mat.diffuse_color=(0.065,0.075,0.082,1)
SPECS=[('K1',.8,1.8,.95),('PC',.9,1.9,.98)]
models={}
def B(p): return (p[0],-p[2],p[1])
def U(p): return [p[0],p[2],-p[1]]
def make(name,verts,faces,col,bevel=.001,segments=2):
    mesh=bpy.data.meshes.new(name+'_Mesh')
    mesh.from_pydata([B(p) for p in verts],[],faces);mesh.update()
    ob=bpy.data.objects.new(name,mesh);col.objects.link(ob)
    ob.data.materials.append(mat)
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    if bevel:
        mod=ob.modifiers.new('SmallSafeEdgeBevel','BEVEL');mod.width=bevel;mod.segments=segments
        mod.limit_method='ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    # Explicit triangulation gives identical geometry in Blender and FBX.
    tri=ob.modifiers.new('DeliveryTriangulation','TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=tri.name)
    # Planar triangles remain flat; bevel strips provide the rounded edge highlights.
    for p in ob.data.polygons:p.use_smooth=False
    ob.select_set(False)
    ob['role']=name.split('_',1)[1]
    return ob
def box(name,lo,hi,col,bevel=.001):
    x,y,z=lo;X,Y,Z=hi
    v=[(x,y,z),(X,y,z),(X,Y,z),(x,Y,z),(x,y,Z),(X,y,Z),(X,Y,Z),(x,Y,Z)]
    f=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    return make(name,v,f,col,bevel)
def loop(w,h,r,n=3):
    points=[]
    for cx,cy,a in [(w/2-r,h/2-r,0),(-w/2+r,h/2-r,90),(-w/2+r,-h/2+r,180),(w/2-r,-h/2+r,270)]:
        for j in range(n+1):
            t=math.radians(a+j*90/n)
            points.append((cx+r*math.cos(t),cy+r*math.sin(t)))
    return points
def ring(prefix,w,h,col):
    outer=loop(w+.042,h+.042,.004)
    inner=loop(w+.002,h+.002,.002)
    n=len(outer);v=[]
    for z,pts in [(-.010,outer),(-.010,inner),(.018,outer),(.018,inner)]:
        v.extend((x,y,z) for x,y in pts)
    f=[]
    for i in range(n):
        j=(i+1)%n
        f.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
    return make(prefix+'_Frame',v,f,col,.0008,2)
for prefix,w,h,center in SPECS:
    col=bpy.data.collections.new(prefix+'_MirrorFrame');scene.collection.children.link(col)
    floor=-center
    obs=[ring(prefix,w,h,col)]
    obs.append(box(prefix+'_Backplate',(-w/2-.016,-h/2-.016,.018),(w/2+.016,h/2+.016,.020),col,.0005))
    for side,sign in [('L',-1),('R',1)]:
        x=sign*(w/2-.060)
        obs.append(box(prefix+'_RearSupport_'+side,(x-.009,floor+.008,.020),(x+.009,h/2-.065,.038),col,.0008))
        obs.append(box(prefix+'_Foot_'+side,(x-.035,floor,.014),(x+.035,floor+.008,.150),col,.001))
        # Rear gusset occupies a triangular volume behind the upright, no crossing bars.
        yz=[(floor+.008,.038),(floor+.600,.038),(floor+.008,.144)]
        v=[(xx,y,z) for xx in (x-.004,x+.004) for y,z in yz]
        f=[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)]
        obs.append(make(prefix+'_RearBrace_'+side,v,f,col,.0006,2))
    for o in obs:
        o['origin']='Existing Surface center; Unity local (0,0,0)'
        o['unity_axes']='X right, Y up, front -Z, rear +Z'
    models[prefix]=obs

def stats(obs):
    verts=[Vector(U(o.matrix_world@v.co)) for o in obs for v in o.data.vertices]
    lo=[min(v[i] for v in verts) for i in range(3)]
    hi=[max(v[i] for v in verts) for i in range(3)]
    parts=[]
    for o in obs:
        bm=bmesh.new();bm.from_mesh(o.data)
        tris=len(o.data.polygons)
        duplicates=len(bm.verts)-len({tuple(round(c,7) for c in v.co) for v in bm.verts})
        faces=[tuple(sorted(p.vertices)) for p in o.data.polygons]
        parts.append({'name':o.name,'triangles':tris,'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'boundary_edges':sum(e.is_boundary for e in bm.edges),'loose_vertices':sum(not v.link_faces for v in bm.verts),'duplicate_vertices':duplicates,'duplicate_faces':len(faces)-len(set(faces)),'signed_volume_blender_m3':bm.calc_volume(signed=True),'identity_transform':all(abs(o.matrix_world[i][j]-(1 if i==j else 0))<1e-6 for i in range(4) for j in range(4))})
        bm.free()
    return {'bounds_unity_m':{'min':lo,'max':hi,'size':[hi[i]-lo[i] for i in range(3)]},'triangles':sum(p['triangles'] for p in parts),'material_count':len({m.name for o in obs for m in o.data.materials}),'parts':parts}

report={'coordinate_mapping':'Blender (X,Y,Z) = Unity (X,-Z,Y). FBX axis conversion baked; all mesh origins at Surface center, identity object transforms.','models':{}}
for prefix,w,h,center in SPECS:
    obs=models[prefix]
    report['models'][prefix]={'surface_m':[w,h],'surface_center_above_floor_m':center,'nominal_opening_m':[w+.002,h+.002],'frame_width_m':.020,'frame_depth_m':.028,'source':stats(obs)}
    assert report['models'][prefix]['source']['triangles']<=2000
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.export_scene.fbx(filepath=str(BASE/(prefix+'_MirrorFrame.fbx')),use_selection=True,object_types={'MESH'},global_scale=1.,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,mesh_smooth_type='OFF',use_mesh_modifiers=True,use_triangles=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)

# Save production scene before importing validation copies. No cameras, lights or surface.
bpy.ops.object.select_all(action='DESELECT')
for o in models['K1']:o.select_set(True)
bpy.context.view_layer.objects.active=models['K1'][0]
# Both deliverables share the required origin; PC is hidden only for convenient initial viewing.
bpy.data.collections['PC_MirrorFrame'].hide_viewport=True
bpy.data.collections['PC_MirrorFrame'].hide_render=True
scene['delivery_notes']='Two independent collections share the reflection-center origin. Toggle collection visibility; do not offset production geometry. Preview scenes are separate.'
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'TrainingMirror.blend'))

# True FBX reimport into a clean scene, standard Blender axis interpretation.
validation=bpy.data.scenes.new('FBX_RoundTrip_Validation')
bpy.context.window.scene=validation
for prefix,w,h,center in SPECS:
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(BASE/(prefix+'_MirrorFrame.fbx')),use_custom_normals=True)
    imported=[o for o in set(bpy.data.objects)-before if o.type=='MESH']
    actual=stats(imported)
    expected=report['models'][prefix]['source']
    # Comparing triangle positions catches missing parts, changes in dimensions and pivots.
    def signature(objects):
        return sorted(tuple(sorted(tuple(round(c,5) for c in U(o.matrix_world@o.data.vertices[i].co)) for i in p.vertices)) for o in objects for p in o.data.polygons)
    same=signature(imported)==signature(models[prefix])
    assert same,(prefix,'roundtrip triangle mismatch')
    assert len(imported)==len(models[prefix])
    assert actual['triangles']==expected['triangles']
    assert all(p['nonmanifold_edges']==0 and p['duplicate_faces']==0 and p['duplicate_vertices']==0 and p['signed_volume_blender_m3']>0 for p in actual['parts'])
    report['models'][prefix]['roundtrip']={'geometry_equal_to_10_micrometers':same,'stats':actual}
    for o in imported:bpy.data.objects.remove(o,do_unlink=True)
bpy.context.window.scene=scene
bpy.data.scenes.remove(validation)
(SRC/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('MIRROR_BUILD_OK',json.dumps({k:{'triangles':v['source']['triangles'],'bounds':v['source']['bounds_unity_m']} for k,v in report['models'].items()}))
