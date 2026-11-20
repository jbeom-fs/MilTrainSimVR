"""Blender mesh production and independent FBX round-trip checks. Never invokes Unity."""
import bpy,bmesh,math,json,pathlib,re
from mathutils import Vector
from io_scene_fbx import parse_fbx
SRC=pathlib.Path(__file__).resolve().parent;BASE=SRC.parent
SPECS=[('AED',720,460),('TQ',720,640),('PB',720,600),('K1',720,480),('PC',720,520),('KD1',720,440),('Entrance',760,560)]
def filename(k,w,h):return f'Display_{k}_{w}x{h}'
def B(p):return Vector((p[0],-p[2],p[1]))
def U(p):return Vector((p[0],p[2],-p[1]))
def plain(n):return re.sub(r'\.\d{3}$','',n)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for container in [bpy.data.collections,bpy.data.materials,bpy.data.cameras,bpy.data.lights]:
    for item in list(container):
        if item.users==0:container.remove(item)
scene=bpy.context.scene;scene.name='TrainingDisplay_Production'
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1.;scene.unit_settings.length_unit='METERS'
def material(name,color,metal,rough):
    m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*color,1)
    n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    n.inputs['Base Color'].default_value=(*color,1);n.inputs['Metallic'].default_value=metal;n.inputs['Roughness'].default_value=rough
    return m
case_mat=material('TrainingDisplay_MatteBlackCase',(.018,.021,.024),.15,.72)
metal_mat=material('TrainingDisplay_MatteBracketMetal',(.065,.075,.082),.8,.68)
def make(name,verts,faces,col,mat,bevel=.0006):
    me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata([B(p) for p in verts],[],faces);me.update()
    ob=bpy.data.objects.new(name,me);col.objects.link(ob);me.materials.append(mat)
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    if bevel:
        mod=ob.modifiers.new('SmallEdgeBevel','BEVEL');mod.width=bevel;mod.segments=2;mod.limit_method='ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    tri=ob.modifiers.new('DeliveryTriangulation','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
    for p in me.polygons:p.use_smooth=False
    ob.select_set(False)
    ob['origin']='Existing UI rectangle center, Unity (0,0,0)'
    ob['unity_axes']='X right, Y up, front -Z, back +Z'
    ob['wall_contact_unity_z_m']=.070
    return ob
def rect_loop(w,h,r,n=4):
    out=[]
    for cx,cy,a in [(w/2-r,h/2-r,0),(-w/2+r,h/2-r,90),(-w/2+r,-h/2+r,180),(w/2-r,-h/2+r,270)]:
        for j in range(n+1):
            t=math.radians(a+90*j/n);out.append((cx+r*math.cos(t),cy+r*math.sin(t)))
    return out
def ring(name,w,h,col):
    outer=rect_loop(w+.032,h+.032,.004)
    inner=rect_loop(w+.004,h+.004,.002)
    n=len(outer);verts=[]
    for z,pts in [(-.0005,outer),(-.0005,inner),(.0395,outer),(.0395,inner)]:verts.extend((x,y,z) for x,y in pts)
    faces=[]
    for i in range(n):
        j=(i+1)%n
        faces.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
    return make(name,verts,faces,col,case_mat,.00035)
def box(name,lo,hi,col):
    x,y,z=lo;X,Y,Z=hi
    return make(name,[(x,y,z),(X,y,z),(X,Y,z),(x,Y,z),(x,y,Z),(X,y,Z),(X,Y,Z),(x,Y,Z)],[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],col,case_mat,.0006)
def bracket(name,sign,col):
    # Closed L-section prism: one folded metal bracket, no overlapping boxes/internal walls.
    xz=[(.100,.0425),(.103,.0425),(.103,.067),(.145,.067),(.145,.070),(.100,.070)]
    vertices=[(sign*x,y,z) for y in (-.110,.110) for x,z in xz];n=len(xz)
    faces=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return make(name,vertices,faces,col,metal_mat,.0005)
models={}
for key,pw,ph in SPECS:
    tag=filename(key,pw,ph);w=pw*.001;h=ph*.001
    col=bpy.data.collections.new(tag);scene.collection.children.link(col)
    obs=[ring(tag+'_FrameCase',w,h,col),box(tag+'_Backplate',(-w/2-.014,-h/2-.014,.0395),(w/2+.014,h/2+.014,.0425),col),bracket(tag+'_WallBracket_L',-1,col),bracket(tag+'_WallBracket_R',1,col)]
    models[key]=obs
    col['screen_width_m']=w;col['screen_height_m']=h;col['opening_width_m']=w+.004;col['opening_height_m']=h+.004

def matinfo(m):
    n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    return {'name':plain(m.name),'base_color_linear_rgba':list(n.inputs['Base Color'].default_value),'metallic':n.inputs['Metallic'].default_value,'roughness':n.inputs['Roughness'].default_value}
def signature(obs):
    return sorted((plain(o.name),plain(o.data.materials[p.material_index].name),tuple(sorted(tuple(round(c,5) for c in U(o.matrix_world@o.data.vertices[j].co)) for j in p.vertices))) for o in obs for p in o.data.polygons)
def clip(poly,normal,bound):
    if not poly:return []
    out=[];n=Vector(normal)
    for a,b in zip(poly,poly[1:]+poly[:1]):
        da=n.dot(a)-bound;db=n.dot(b)-bound
        if da<=0:out.append(a)
        if (da<0<db) or (db<0<da):out.append(a+(b-a)*(da/(da-db)))
    return out
def stats(obs,w,h):
    verts=[U(o.matrix_world@v.co) for o in obs for v in o.data.vertices]
    lo=[min(v[i] for v in verts) for i in range(3)];hi=[max(v[i] for v in verts) for i in range(3)]
    parts=[];allfaces=set();crossdups=0;intrusions=0;oblique=0;bounds={}
    for o in sorted(obs,key=lambda o:o.name):
        uv=[U(o.matrix_world@v.co) for v in o.data.vertices]
        bounds[o.name]=([min(v[i] for v in uv) for i in range(3)],[max(v[i] for v in uv) for i in range(3)])
        bm=bmesh.new();bm.from_mesh(o.data);bm.faces.ensure_lookup_table();bm.normal_update()
        normals=[f.normal.copy() for f in bm.faces]
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
        duplicate_vertices=len(bm.verts)-len({tuple(round(c,7) for c in v.co) for v in bm.verts})
        faces=[tuple(sorted(p.vertices)) for p in o.data.polygons]
        parts.append({'name':plain(o.name),'triangles':len(o.data.polygons),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'boundary_edges':sum(e.is_boundary for e in bm.edges),'loose_vertices':sum(not v.link_faces for v in bm.verts),'duplicate_vertices':duplicate_vertices,'duplicate_faces':len(faces)-len(set(faces)),'signed_volume_m3':bm.calc_volume(signed=True),'outward_normal_recalculation_changes':sum(n.dot(f.normal)<.999 for n,f in zip(normals,bm.faces)),'identity_transform':all(abs(o.matrix_world[i][j]-(1 if i==j else 0))<1e-6 for i in range(4) for j in range(4)),'origin_blender_m':list(o.location),'rotation_blender_deg':[math.degrees(v) for v in o.rotation_euler],'scale':list(o.scale),'materials':[plain(m.name) for m in o.data.materials]})
        bm.free()
        for p in o.data.polygons:
            coords=[uv[j] for j in p.vertices]
            key=tuple(sorted(tuple(round(c,7) for c in v) for v in coords))
            if key in allfaces:crossdups+=1
            allfaces.add(key)
            for slope,field in [(0,'front'),(math.tan(math.radians(60)),'oblique')]:
                poly=coords[:]
                for normal,bound in [((1,0,slope),w/2),((-1,0,slope),w/2),((0,1,slope),h/2),((0,-1,slope),h/2),((0,0,1),1e-8)]:poly=clip(poly,normal,bound)
                if poly:
                    if field=='front':intrusions+=1
                    else:oblique+=1
    overlaps=[]
    for i,a in enumerate(obs):
        for b in obs[i+1:]:
            al,ah=bounds[a.name];bl,bh=bounds[b.name]
            dims=[min(ah[j],bh[j])-max(al[j],bl[j]) for j in range(3)]
            if min(dims)>1e-6:overlaps.append([plain(a.name),plain(b.name)])
    mats={plain(m.name):matinfo(m) for o in obs for m in o.data.materials}
    return {'bounds_unity_m':{'min':lo,'max':hi,'size':[hi[i]-lo[i] for i in range(3)]},'part_count':len(obs),'triangles':sum(p['triangles'] for p in parts),'material_count':len(mats),'materials':list(mats.values()),'parts':parts,'surface_rectangle_intrusions_z_le_0':intrusions,'oblique_60deg_keepout_intersections':oblique,'duplicate_faces_across_parts':crossdups,'positive_volume_aabb_overlaps':overlaps}
def asserthealthy(s,identity_required=True):
    assert s['part_count']==4 and s['material_count']==2 and s['triangles']<=1500
    assert not s['surface_rectangle_intrusions_z_le_0'] and not s['oblique_60deg_keepout_intersections'] and not s['duplicate_faces_across_parts'] and not s['positive_volume_aabb_overlaps']
    for p in s['parts']:
        assert p['signed_volume_m3']>0,p
        assert not identity_required or p['identity_transform'],p
        assert max(abs(v) for v in p['origin_blender_m'])<1e-6
        assert max(abs(v-1) for v in p['scale'])<1e-6
        assert not any(p[k] for k in ['nonmanifold_edges','boundary_edges','loose_vertices','duplicate_vertices','duplicate_faces','outward_normal_recalculation_changes'])

report={'blender_version':bpy.app.version_string,'units':'meters; Blender unit scale 1; all object scales 1','coordinates':'Blender (X,Y,Z) = target (X,-Z,Y); target UI plane XY Z=0, front -Z, rear +Z','materials_linear_rgb':True,'models':{}}
for key,pw,ph in SPECS:
    tag=filename(key,pw,ph);obs=models[key];w=pw*.001;h=ph*.001
    s=stats(obs,w,h);asserthealthy(s)
    report['models'][key]={'file':tag+'.fbx','screen_m':[w,h],'opening_nominal_m':[w+.004,h+.004],'frame_width_m':.014,'case_depth_including_cover_m':.043,'origin_target_m':[0,0,0],'wall_contact_z_m':.070,'source':s}
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.export_scene.fbx(filepath=str(BASE/(tag+'.fbx')),use_selection=True,object_types={'MESH'},global_scale=1.,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,mesh_smooth_type='OFF',use_mesh_modifiers=True,use_triangles=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)

def child(e,key):return next((x for x in e.elems if x.id==key),None)
def properties(e):
    p=child(e,b'Properties70');return {x.props[0].decode():list(x.props[4:]) for x in p.elems} if p else {}
valid=bpy.data.scenes.new('TEMP_FBX_Validation');bpy.context.window.scene=valid
for key,pw,ph in SPECS:
    tag=filename(key,pw,ph);path=BASE/(tag+'.fbx')
    before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(path),use_custom_normals=True)
    added=list(set(bpy.data.objects)-before)
    assert all(o.type=='MESH' for o in added)
    # Blender restores its Z-up convention with an import rotation; compare world geometry.
    # Exported FBX node identity is checked independently below, before any import conversion.
    actual=stats(added,pw*.001,ph*.001);asserthealthy(actual,identity_required=False)
    expected=report['models'][key]['source']
    equal=signature(added)==signature(models[key]);assert equal
    assert actual['triangles']==expected['triangles']
    assert {p['name']:p['triangles'] for p in actual['parts']}=={p['name']:p['triangles'] for p in expected['parts']}
    sm={m['name']:m for m in expected['materials']};im={m['name']:m for m in actual['materials']}
    for n,m in sm.items():
        assert max(abs(a-b) for a,b in zip(m['base_color_linear_rgba'],im[n]['base_color_linear_rgba']))<1e-5
        assert abs(m['metallic']-im[n]['metallic'])<1e-5 and abs(m['roughness']-im[n]['roughness'])<1e-5
    report['models'][key]['roundtrip']={'geometry_and_material_assignment_equal_10um':equal,'stats':actual}
    for o in added:bpy.data.objects.remove(o,do_unlink=True)
    root,version=parse_fbx.parse(str(path));gs=properties(child(root,b'GlobalSettings'));objects=child(root,b'Objects')
    nodes=[];raw=[];object_types={}
    for e in objects.elems:
        t=e.id.decode();object_types[t]=object_types.get(t,0)+1
        if e.id==b'Model':
            p=properties(e);node={'name':e.props[1].split(b'\x00')[0].decode(),'type':e.props[2].decode(),'translation':p.get('Lcl Translation',[0,0,0]),'rotation':p.get('Lcl Rotation',[0,0,0]),'scale':p.get('Lcl Scaling',[1,1,1])}
            assert node['type']=='Mesh' and all(abs(v)<1e-6 for v in node['translation']+node['rotation']) and all(abs(v-1)<1e-6 for v in node['scale']);nodes.append(node)
        if e.id==b'Geometry':
            a=child(e,b'Vertices').props[0];raw.extend(zip(a[::3],a[1::3],a[2::3]))
    assert object_types=={'Geometry':4,'Material':2,'Model':4},object_types
    assert gs['UnitScaleFactor']==[100.] and gs['UpAxis']==[1] and gs['UpAxisSign']==[1]
    lo=[min(v[i] for v in raw) for i in range(3)];hi=[max(v[i] for v in raw) for i in range(3)]
    assert max(abs(lo[i]-expected['bounds_unity_m']['min'][i]) for i in range(3))<1e-6
    assert max(abs(hi[i]-expected['bounds_unity_m']['max'][i]) for i in range(3))<1e-6
    report['models'][key]['raw_fbx']={'version':version,'object_types':object_types,'nodes':nodes,'settings':{k:gs[k] for k in ['UpAxis','UpAxisSign','FrontAxis','FrontAxisSign','CoordAxis','CoordAxisSign','UnitScaleFactor']},'bounds_m':{'min':lo,'max':hi}}
    print('VALIDATED',tag,actual['triangles'],'tris, 4 parts, 2 materials',flush=True)
bpy.context.window.scene=scene;bpy.data.scenes.remove(valid)
bpy.data.orphans_purge(do_recursive=True)
for container in [bpy.data.materials,bpy.data.meshes,bpy.data.cameras,bpy.data.lights]:
    for item in list(container):
        if item.users==0:container.remove(item)
for key,pw,ph in SPECS:
    col=bpy.data.collections[filename(key,pw,ph)];col.hide_viewport=key!='AED';col.hide_render=key!='AED'
bpy.ops.object.select_all(action='DESELECT')
scene['delivery_notes']='Seven independent size collections share UI-center origin. Only AED is initially visible. Switch collection visibility; do not offset production geometry. UI guide/camera/lights are never saved in this source.'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.shading.type='SOLID';sp.shading.color_type='MATERIAL';sp.shading.background_type='VIEWPORT';sp.shading.background_color=(.28,.30,.32);sp.overlay.show_floor=False
            sp.region_3d.view_location=Vector((0,-.035,0));sp.region_3d.view_distance=1.25;sp.region_3d.view_rotation=Vector((.5,2,.4)).to_track_quat('Z','Y');sp.region_3d.view_perspective='ORTHO'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'TrainingDisplay.blend'))
(SRC/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('ALL_7_DISPLAY_MODELS_VALIDATED',flush=True)
