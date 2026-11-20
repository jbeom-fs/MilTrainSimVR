import bpy, bmesh, json, pathlib, math
from mathutils import Vector
from io_scene_fbx import parse_fbx
SRC=pathlib.Path(__file__).resolve().parent
BASE=SRC.parent
bpy.ops.wm.open_mainfile(filepath=str(SRC/'TrainingMirror.blend'))
prod=bpy.context.scene
report=json.loads((SRC/'validation.json').read_text(encoding='utf-8'))
def U(p):return Vector((p[0],p[2],-p[1]))
def B(p):return Vector((p[0],-p[2],p[1]))
def child(e,key):return next((x for x in e.elems if x.id==key),None)
def props(e):
    p=child(e,b'Properties70')
    return {x.props[0].decode():list(x.props[4:]) for x in p.elems} if p else {}
def clip(poly,axis,bound,sign):
    out=[]
    if not poly:return out
    for a,b in zip(poly,poly[1:]+poly[:1]):
        da=sign*(a[axis]-bound);db=sign*(b[axis]-bound)
        if da<=0:out.append(a)
        if (da<0 and db>0) or (da>0 and db<0):out.append(a+(b-a)*(da/(da-db)))
    return out
for prefix,w,h,center in [('K1',.8,1.8,.95),('PC',.9,1.9,.98)]:
    col=bpy.data.collections[prefix+'_MirrorFrame'];obs=list(col.objects)
    intrusion=[];normals=[];duplicate_faces=[];allfaces=set();bounds={}
    for o in obs:
        uv=[U(o.matrix_world@v.co) for v in o.data.vertices]
        bounds[o.name]=([min(v[i] for v in uv) for i in range(3)],[max(v[i] for v in uv) for i in range(3)])
        for p in o.data.polygons:
            key=tuple(sorted(tuple(round(c,7) for c in uv[j]) for j in p.vertices))
            if key in allfaces:duplicate_faces.append(o.name)
            allfaces.add(key)
            poly=[uv[j] for j in p.vertices]
            # Any material inside the exact rectangular Surface, at or ahead of its plane?
            for axis,bound,sign in [(0,w/2,1),(0,-w/2,-1),(1,h/2,1),(1,-h/2,-1),(2,1e-7,1)]:
                poly=clip(poly,axis,bound,sign)
            if poly:intrusion.append([o.name,p.index])
        bm=bmesh.new();bm.from_mesh(o.data);bm.faces.ensure_lookup_table()
        old=[f.normal.copy() for f in bm.faces]
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
        bad=sum(a.dot(f.normal)<.999 for a,f in zip(old,bm.faces))
        normals.append({'name':o.name,'faces_changed_by_outward_normal_recalculation':bad})
        bm.free()
    overlap=[]
    for i,a in enumerate(obs):
        for b in obs[i+1:]:
            alo,ahi=bounds[a.name];blo,bhi=bounds[b.name]
            dims=[min(ahi[j],bhi[j])-max(alo[j],blo[j]) for j in range(3)]
            if min(dims)>1e-6:overlap.append([a.name,b.name,dims])
    # Raw FBX inspection, independent of Blender's inverse axis interpretation.
    root,version=parse_fbx.parse(str(BASE/(prefix+'_MirrorFrame.fbx')))
    gs=props(child(root,b'GlobalSettings'));objects=child(root,b'Objects')
    fbx_models=[];vraw=[];types=[]
    for e in objects.elems:
        if e.id==b'Model':
            p=props(e)
            name=e.props[1].split(b'\x00')[0].decode()
            fbx_models.append({'name':name,'type':e.props[2].decode(),'translation':p.get('Lcl Translation',[0,0,0]),'rotation':p.get('Lcl Rotation',[0,0,0]),'scale':p.get('Lcl Scaling',[1,1,1])})
            assert all(abs(v)<1e-6 for v in p.get('Lcl Translation',[0,0,0])+p.get('Lcl Rotation',[0,0,0]))
            assert all(abs(v-1)<1e-6 for v in p.get('Lcl Scaling',[1,1,1]))
            assert e.props[2]==b'Mesh'
        if e.id==b'Geometry':
            a=child(e,b'Vertices').props[0]
            vraw.extend(zip(a[::3],a[1::3],a[2::3]))
    rawlo=[min(v[i] for v in vraw) for i in range(3)];rawhi=[max(v[i] for v in vraw) for i in range(3)]
    expected=report['models'][prefix]['source']['bounds_unity_m']
    assert max(abs(rawlo[i]-expected['min'][i]) for i in range(3))<1e-6
    assert max(abs(rawhi[i]-expected['max'][i]) for i in range(3))<1e-6
    assert gs['UnitScaleFactor']==[100.0] and gs['UpAxis']==[1] and gs['UpAxisSign']==[1]
    assert not intrusion and not duplicate_faces and not overlap
    assert all(x['faces_changed_by_outward_normal_recalculation']==0 for x in normals)
    report['models'][prefix]['extended_checks']={'surface_rectangle_intrusions_at_or_in_front_of_z0':intrusion,'duplicate_faces_across_parts':duplicate_faces,'strict_aabb_volume_overlaps_between_parts':overlap,'outward_normal_check':normals,'floor_y_m':-center,'frame_bottom_clearance_m':bounds[prefix+'_Frame'][0][1]+center,'backplate_front_z_m':bounds[prefix+'_Backplate'][0][2],'fbx_raw':{'version':version,'global_settings':{k:gs[k] for k in ['UpAxis','UpAxisSign','FrontAxis','FrontAxisSign','CoordAxis','CoordAxisSign','UnitScaleFactor']},'models':fbx_models,'bounds_m':{'min':rawlo,'max':rawhi}}}
(SRC/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('EXTENDED_AUDIT_PASS',flush=True)

def material(name,color,metal=0,rough=.7):
    m=bpy.data.materials.new(name);m.use_nodes=True
    n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    n.inputs['Base Color'].default_value=(*color,1);n.inputs['Metallic'].default_value=metal;n.inputs['Roughness'].default_value=rough
    return m
guide=material('PREVIEW_ONLY_SurfacePosition',(.26,.46,.52),0,.9)
floor_mat=material('PREVIEW_ONLY_Ground',(.64,.67,.68),0,.85)
for prefix,w,h,center in [('K1',.8,1.8,.95),('PC',.9,1.9,.98)]:
    sc=bpy.data.scenes.new('PREVIEW_ONLY_'+prefix);bpy.context.window.scene=sc
    sc.unit_settings.system='METRIC'
    for o in bpy.data.collections[prefix+'_MirrorFrame'].objects:
        dup=o.copy();dup.data=o.data;sc.collection.objects.link(dup)
    me=bpy.data.meshes.new('PREVIEW_ONLY_SurfaceGuide')
    me.from_pydata([B((-w/2,-h/2,0)),B((w/2,-h/2,0)),B((w/2,h/2,0)),B((-w/2,h/2,0))],[],[(0,3,2,1)]);me.materials.append(guide)
    ob=bpy.data.objects.new('PREVIEW_ONLY_SurfaceGuide_NOT_IN_FBX',me);sc.collection.objects.link(ob)
    me=bpy.data.meshes.new('PREVIEW_ONLY_Ground')
    me.from_pydata([(-100,-100,-center-.0002),(100,-100,-center-.0002),(100,100,-center-.0002),(-100,100,-center-.0002)],[],[(0,1,2,3)]);me.materials.append(floor_mat)
    ground=bpy.data.objects.new('PREVIEW_ONLY_Ground',me);sc.collection.objects.link(ground)
    world=bpy.data.worlds.new('PREVIEW_ONLY_World');world.use_nodes=True
    bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.65,.69,.74,1);bg.inputs['Strength'].default_value=.6;sc.world=world
    for name,loc,power,size in [('Key',(-3,4,4),650,4),('Fill',(3,2,1),350,3),('Rim',(1,-3,3),700,3)]:
        data=bpy.data.lights.new('PREVIEW_ONLY_'+name,'AREA');data.energy=power;data.shape='DISK';data.size=size
        light=bpy.data.objects.new('PREVIEW_ONLY_'+name,data);sc.collection.objects.link(light);light.location=loc;light.rotation_euler=(Vector((0,0,0))-light.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('PREVIEW_ONLY_Camera');camera=bpy.data.objects.new('PREVIEW_ONLY_Camera',data);sc.collection.objects.link(camera);sc.camera=camera
    data.type='ORTHO';data.ortho_scale=2.20 if prefix=='K1' else 2.30
    sc.render.engine='BLENDER_EEVEE';sc.render.resolution_x=850;sc.render.resolution_y=1050;sc.render.resolution_percentage=100
    sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGB'
    sc.view_settings.view_transform='AgX'
    target=B((0,(-center+h/2+.021)/2,.050))
    for view,d in [('Front',(0,0,-1)),('Side',(1,0,0)),('Rear',(0,0,1)),('Oblique',(1.15,.28,-1.8))]:
        camera.location=target+B(d).normalized()*4
        camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
        sc.render.filepath=str(SRC/'Previews'/(prefix+'_'+view+'.png'))
        bpy.ops.render.render(write_still=True)
        print('PREVIEW_SAVED',prefix,view,flush=True)
# Preview staging is deliberately not saved into the production blend.
print('PREVIEWS_COMPLETE',flush=True)
