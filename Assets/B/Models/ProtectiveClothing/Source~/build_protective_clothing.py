"""Deterministic Blender-native asset authoring; no Unity execution or edits."""
import bpy,bmesh,math,json,os,hashlib
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
P=Path(__file__).resolve().parent.parent; S=P/'Source~'; PRE=S/'Previews'; PRE.mkdir(exist_ok=True)
REF=P.parent/'Mannequin'/'Mannequin.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene; sc.name='ProtectiveClothing_Review'; sc.unit_settings.system='METRIC'; sc.unit_settings.scale_length=1
def collection(name):
    c=bpy.data.collections.new(name); sc.collection.children.link(c); return c
refcol=collection('Reference'); wc=collection('Worn'); pc=collection('Pickup'); studio=collection('Review_Studio')
bpy.ops.import_scene.fbx(filepath=str(REF),use_anim=False)
refs=list(bpy.context.selected_objects)
for o in refs:
    for c in list(o.users_collection):c.objects.unlink(o)
    refcol.objects.link(o)
arm=next(o for o in refs if o.type=='ARMATURE'); body=next(o for o in refs if o.type=='MESH')
dg=bpy.context.evaluated_depsgraph_get(); ev=body.evaluated_get(dg); em=ev.to_mesh()
refverts=[ev.matrix_world@v.co for v in em.vertices]; reffaces=[list(p.vertices) for p in em.polygons]
ev.to_mesh_clear()
baseline={str(REF):hashlib.sha256(REF.read_bytes()).hexdigest()}
for f in [P.parent/'K1GasMask'/'K1_GasMask.fbx', P.parents[1]/'Scene'/'B_K1_Tutorial.unity', P.parents[1]/'Scripts'/'K1GasMask'/'MirrorAvatar'/'K1UserAvatar.cs']:
    baseline[str(f)]=hashlib.sha256(f.read_bytes()).hexdigest()
(S/'OriginalAssetHashes.json').write_text(json.dumps(baseline,indent=2))
mat=bpy.data.materials.new('PC_Shared'); mat.use_nodes=True
nodes=mat.node_tree.nodes; links=mat.node_tree.links; bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
for filename,socket,noncolor in [('PC_BaseColor_2K.png','Base Color',False),('PC_Roughness_1K.png','Roughness',True),('PC_Normal_1K.png',None,True)]:
    tex=nodes.new('ShaderNodeTexImage'); tex.image=bpy.data.images.load(str(P/'Textures'/filename))
    if noncolor:tex.image.colorspace_settings.name='Non-Color'
    if socket:links.new(tex.outputs['Color'],bs.inputs[socket])
    else:
        norm=nodes.new('ShaderNodeNormalMap'); norm.inputs['Strength'].default_value=.23
        links.new(tex.outputs['Color'],norm.inputs['Color']); links.new(norm.outputs['Normal'],bs.inputs['Normal'])
bs.inputs['Metallic'].default_value=0
def move(o,c):
    for x in list(o.users_collection):x.objects.unlink(o)
    c.objects.link(o)
def activate(o):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
def mesh(name,v,f,c=wc):
    me=bpy.data.meshes.new(name+'_Mesh'); me.from_pydata(v,[],f); me.update(); o=bpy.data.objects.new(name,me); c.objects.link(o); return o
def recalc(o):
    bm=bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(o.data); bm.free()
def uv(o,region='fabric'):
    activate(o)
    if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.uv.smart_project(angle_limit=1.15,island_margin=.025); bpy.ops.object.mode_set(mode='OBJECT')
    if region=='fabric':lo,hi=(.02,.02),(.98,.72)
    elif region=='rubber':lo,hi=(.02,.76),(.24,.85)
    elif region=='trim':lo,hi=(.27,.76),(.48,.98)
    elif region=='sole':lo,hi=(.02,.76),(.24,.85)
    for l in o.data.uv_layers.active.data:l.uv=(lo[0]+l.uv.x*(hi[0]-lo[0]),lo[1]+l.uv.y*(hi[1]-lo[1]))
    o.data.materials.clear(); o.data.materials.append(mat)
    for p in o.data.polygons:p.use_smooth=True
def apply(o,mod):activate(o); bpy.ops.object.modifier_apply(modifier=mod.name)
def join(parts,name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join(); parts[0].name=name; return parts[0]
def tube(name,rings,axis='z',n=40,cap=True):
    # rings = (axial position, centre u, centre v, radius u, radius v)
    vs=[]; fs=[]
    for j,(t,cu,cv,ru,rv) in enumerate(rings):
        for i in range(n):
            a=2*math.pi*i/n; wobble=1+.017*math.cos(6*a+j*.9)
            u=cu+ru*math.cos(a)*wobble; v=cv+rv*math.sin(a)*wobble
            vs.append((u,v,t) if axis=='z' else (t,u,v))
    for j in range(len(rings)-1):
        for i in range(n):a=j*n+i;b=j*n+(i+1)%n;fs.append((a,b,b+n,a+n))
    if cap:fs.extend([tuple(reversed(range(n))),tuple((len(rings)-1)*n+i for i in range(n))])
    o=mesh(name,vs,fs); recalc(o); return o
def remesh(o,voxel,ratio):
    m=o.modifiers.new('Union','REMESH'); m.mode='VOXEL'; m.voxel_size=voxel; m.use_smooth_shade=True; apply(o,m)
    m=o.modifiers.new('ClothRelax','SMOOTH'); m.factor=.65; m.iterations=4; apply(o,m)
    m=o.modifiers.new('Reduction','DECIMATE'); m.ratio=ratio; apply(o,m); recalc(o)
def cut(o,point,normal):
    bm=bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,plane_co=point,plane_no=normal,clear_outer=True,clear_inner=False)
    bm.to_mesh(o.data); bm.free(); recalc(o)
def shell(o):
    m=o.modifiers.new('ThinClothRims','SOLIDIFY'); m.thickness=.002; m.offset=-1; m.use_rim=True; apply(o,m); recalc(o)
def box(name,loc,size,bevel=.008,c=wc,region='trim'):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc); o=bpy.context.object; move(o,c); o.name=name; o.scale=size
    activate(o); bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        m=o.modifiers.new('SoftCorners','BEVEL'); m.width=bevel; m.segments=3; apply(o,m)
    uv(o,region); return o
def label(name,center,w,h,side='L',plane='front',c=wc):
    x,y,z=center
    if plane=='front':v=[(x-w/2,y,z-h/2),(x+w/2,y,z-h/2),(x+w/2,y,z+h/2),(x-w/2,y,z+h/2)]
    elif plane=='top':v=[(x-w/2,y-h/2,z),(x+w/2,y-h/2,z),(x+w/2,y+h/2,z),(x-w/2,y+h/2,z)]
    o=mesh(name,v,[(0,1,2,3)],c); recalc(o); o.data.materials.append(mat); u=o.data.uv_layers.new(name='UVMap')
    xmin,xmax=(32/2048,224/2048) if side=='L' else (280/2048,472/2048)
    for l,co in zip(u.data,[(xmin,1-224/2048),(xmax,1-224/2048),(xmax,1-32/2048),(xmin,1-32/2048)]):l.uv=co
    return o
def trimtube(name,rings,axis='z',region='trim'):
    o=tube(name,rings,axis,n=32,cap=False); shell(o); uv(o,region); return o
# Loose torso and sleeves, merged before removing neck, wrist and hem caps.
torso=tube('JacketShell',[(.91,0,-.018,.200,.17),(.98,0,-.025,.200,.17),(1.08,0,-.034,.183,.16),(1.18,0,-.03,.183,.16),(1.29,0,-.005,.204,.154),(1.38,0,.014,.214,.136),(1.435,0,.023,.218,.118),(1.475,0,.029,.205,.108),(1.50,0,.029,.183,.102),(1.54,0,.029,.081,.076)])
parts=[torso]
for s in [1,-1]:
    rings=[]
    for t,ry,rz in [(.135,.112,.078),(.21,.105,.078),(.3,.087,.082),(.42,.075,.073),(.46,.073,.073),(.55,.063,.064),(.63,.053,.053),(.695,.049,.049)]:
        pos=arm.matrix_world@arm.data.bones['mixamorig1:'+('Left' if s==1 else 'Right')+'Arm'].head_local
        z=1.435-(t-.186)*.024 if t<.44 else 1.413+(t-.44)*.037
        y=.03+(min(t,.44)-.186)*.055 if t<.44 else .045-(t-.44)*.04
        rings.append((s*t,y,z,ry,rz))
    parts.append(tube('Sleeve',rings,'x',32))
jacket=join(parts,'Jacket_Worn'); remesh(jacket,.006,.033)
cut(jacket,(0,0,.943),(0,0,-1)); cut(jacket,(0,0,1.524),(0,0,1)); cut(jacket,(.678,0,0),(1,0,0)); cut(jacket,(-.678,0,0),(-1,0,0)); shell(jacket); uv(jacket)
details=[jacket]
# Broad storm flap and pockets are fixed surface details in the same final mesh.
flap_profile=[(.963,-.200),(1.00,-.201),(1.08,-.201),(1.18,-.196),(1.29,-.166),(1.38,-.129),(1.435,-.103),(1.48,-.071),(1.506,-.064)]
v=[]
for z,y in flap_profile:v.extend([(-.014,y,z),(.014,y,z)])
flap=mesh('StormFlap',v,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(flap_profile)-1)]);recalc(flap);shell(flap);uv(flap,'trim');details.append(flap)
for s in [-1,1]:
    details.append(box('ChestPocket',(s*.106,-.159,1.295),(.098,.018,.105),.009,region='fabric'))
    details.append(box('PocketFlap',(s*.106,-.172,1.327),(.105,.011,.029),.004,region='trim'))
    details.append(box('HipPocket',(s*.11,-.184,1.032),(.085,.015,.078),.006,region='fabric'))
for s in [-1,1]:
    t=.653; z=1.413+(t-.44)*.037; y=.045-(t-.44)*.04
    details.append(trimtube('Cuff',[(s*.635,y,z,.054,.055),(s*.675,y,z,.051,.052)],'x'))
jacket=join(details,'Jacket_Worn')
# Trousers: pelvis joined to two leg envelopes, natural room at hips/knees.
pelvis=tube('Pelvis',[(.735,0,-.016,.136,.105),(.82,0,-.019,.179,.133),(.94,0,-.025,.184,.151),(1.015,0,-.025,.172,.143)])
parts=[pelvis]
for s in [1,-1]:
    parts.append(tube('Leg',[(.20,s*.144,.035,.069,.073),(.29,s*.137,.043,.076,.087),(.41,s*.126,.03,.08,.089),(.52,s*.118,.001,.08,.092),(.62,s*.11,-.006,.087,.108),(.75,s*.098,-.018,.094,.123),(.86,s*.088,-.021,.105,.128)]))
trousers=join(parts,'Trousers_Worn'); remesh(trousers,.006,.038)
cut(trousers,(0,0,.23),(0,0,-1)); cut(trousers,(0,0,1.003),(0,0,1)); shell(trousers); uv(trousers)
details=[trousers,trimtube('Waistband',[(.971,0,-.025,.181,.153),(1.002,0,-.025,.177,.15)])]
trousers=join(details,'Trousers_Worn')
worn={'Jacket':jacket,'Trousers':trousers}
for s,side in [(1,'L'),(-1,'R')]:
    # Glove shell follows the original hand, expanded for protective rubber thickness.
    v=[tuple(p) for p in refverts]; f=[f for f in reffaces if all(s*refverts[i].x>.628 for i in f)]
    g=mesh('Glove_'+side+'_Worn',v,f)
    bm=bmesh.new(); bm.from_mesh(g.data); bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS'); bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0002)
    bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
    for v in bm.verts:v.co+=v.normal*.009
    bm.to_mesh(g.data); bm.free()
    remesh(g,.0023,.033); cut(g,(s*.665,0,0),(s*-1,0,0)); shell(g); uv(g,'rubber')
    # Forearm gauntlet overlaps sleeve by 41 mm with a larger outer radius.
    cy=.045-(.65-.44)*.04; cz=1.413+(.65-.44)*.037
    cuff=trimtube('Gauntlet',[(s*.632,cy,cz,.061,.062),(s*.66,cy,cz,.06,.061),(s*.693,cy,cz,.05,.05)],'x',region='rubber')
    lab=label('SideMark',(s*.657,cy-.0615,cz),.029,.029,side)
    g=join([g,cuff,lab],'Glove_'+side+'_Worn'); worn['Glove_'+side]=g
    boot=tube('Overboot_'+side+'_Worn',[(.006,s*.169,-.043,.074,.151),(.024,s*.169,-.043,.077,.151),(.052,s*.166,-.035,.075,.143),(.084,s*.16,-.019,.071,.122),(.12,s*.15,.019,.065,.084),(.17,s*.147,.038,.075,.082),(.23,s*.143,.04,.087,.100),(.295,s*.138,.044,.089,.107)],n=40)
    cut(boot,(0,0,.288),(0,0,1)); shell(boot); uv(boot,'fabric')
    sole=trimtube('Sole',[(.006,s*.169,-.043,.079,.156),(.027,s*.169,-.043,.081,.157)],region='sole')
    collar=trimtube('BootCollar',[(.262,s*.14,.043,.094,.113),(.289,s*.138,.044,.095,.114)],region='trim')
    lab=label('SideMark',(s*.142,-.072,.275),.032,.032,side)
    boot=join([boot,sole,collar,lab],'Overboot_'+side+'_Worn'); worn['Overboot_'+side]=boot
# Remove the few local protrusions found by signed-nearest-surface inspection.
refbvh=BVHTree.FromPolygons(refverts,reffaces,all_triangles=False)
adjusted={}
for k,o in worn.items():
    count=0
    for v in o.data.vertices:
        loc,no,idx,dist=refbvh.find_nearest(v.co)
        if (v.co-loc).dot(no)<.003:
            v.co=loc+no*.005;count+=1
    recalc(o);adjusted[k]=count
(S/'FitAdjustments.json').write_text(json.dumps(adjusted,indent=2))
def empty(name,c,loc=(0,0,0),parent=None):
    o=bpy.data.objects.new(name,None); c.objects.link(o); o.empty_display_type='ARROWS'; o.empty_display_size=.045; o.location=loc; o.parent=parent; return o
wr=empty('ProtectiveClothing_Worn',wc); pr=empty('ProtectiveClothing_Pickup',pc)
attach={'Jacket':(0,-.025,1.29),'Trousers':(0,-.025,.965),'Glove_L':(.675,.035,1.422),'Glove_R':(-.675,.035,1.422),'Overboot_L':(.15,.031,.105),'Overboot_R':(-.15,.031,.105)}
for k,o in worn.items():
    activate(o); bpy.ops.object.transform_apply(location=True,rotation=True,scale=True); o.parent=wr
    empty('Attach_'+k+'_Worn',wc,attach[k],o)
    o['component']=k; o['state']='Worn'; o['fit_pose']='Original FBX without animation; static bind reference'; o['left_right']='wearer perspective'
# Folded cloth pickups use physical folded layers and recognisable panels.
pickup={}
for k,width,depth in [('Jacket',.34,.28),('Trousers',.29,.32)]:
    parts=[]
    for j in range(3):parts.append(box('FoldedLayer',(0,0,.015+j*.021),(width-(j%2)*.018,depth,.027),.012,c=pc,region='fabric'))
    if k=='Jacket':
        parts.append(box('FoldedStormFlap',(0,0,.073),(.026,depth-.028,.009),.003,c=pc,region='trim'))
        for s in [-1,1]:
            p=box('FoldedSleeve',(s*.104,.007,.082),(.103,.232,.025),.009,c=pc,region='fabric'); p.rotation_euler.z=s*.19; parts.append(p)
            parts.append(box('PocketFlap',(s*.103,-.034,.099),(.078,.036,.008),.004,c=pc,region='trim'))
    else:
        parts.append(box('WaistFold',(0,.115,.078),(.283,.046,.024),.007,c=pc,region='trim'))
        parts.append(box('LegFold',(0,-.01,.081),(.008,.265,.005),.002,c=pc,region='trim'))
    o=join(parts,k+'_Pickup'); pickup[k]=o
# Left and right gloves/boots keep actual handed silhouettes in their own local table frame.
for k in ['Glove_L','Glove_R','Overboot_L','Overboot_R']:
    src=worn[k]; o=src.copy(); o.data=src.data.copy(); o.name=k+'_Pickup'; pc.objects.link(o); o.parent=None
    if k.startswith('Glove'):
        s=1 if k.endswith('L') else -1
        rot=Matrix.Rotation(s*math.pi/2,4,'Z')
        pts=[rot@v.co for v in o.data.vertices]
    else:pts=[v.co.copy() for v in o.data.vertices]
    lo=Vector([min(v[a] for v in pts) for a in range(3)]); hi=Vector([max(v[a] for v in pts) for a in range(3)])
    center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    for v,p in zip(o.data.vertices,pts):v.co=p-center
    o.matrix_world=Matrix.Identity(4); pickup[k]=o
for k,o in pickup.items():
    activate(o); bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # Origin centered on horizontal footprint and bottom of the prop.
    lo=Vector([min(v.co[a] for v in o.data.vertices) for a in range(3)]);hi=Vector([max(v.co[a] for v in o.data.vertices) for a in range(3)])
    center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    for v in o.data.vertices:v.co-=center
    if k.startswith('Glove'):
        mark=label('PickupSideMark',(0,-.095,hi.z-lo.z+.001),.027,.027,k[-1],'top',pc)
        o=join([o,mark],k+'_Pickup');pickup[k]=o
    o.parent=pr; o.location=(0,0,0)
    grip=Vector((0,0,(hi.z-lo.z)*.62)); empty('Grip_'+k+'_Pickup',pc,grip,o)
    empty('Attach_'+k+'_Pickup',pc,(0,0,(hi.z-lo.z)*.5),o)
    o['component']=k; o['state']='Pickup'; o['pivot']='bottom centre; +Z up, -Y front'
# Final explicit triangulation and mesh diagnostics.
stats={}
for k,o in list(worn.items())+[(k+'_Pickup',o) for k,o in pickup.items()]:
    recalc(o)
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001);bmesh.ops.dissolve_degenerate(bm,dist=.000001,edges=list(bm.edges));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    m=o.modifiers.new('ExportTriangles','TRIANGULATE'); apply(o,m)
    bm=bmesh.new();bm.from_mesh(o.data)
    stats[o.name]={'triangles':len(o.data.polygons),'vertices':len(o.data.vertices),'material_slots':len(o.data.materials),'materials':list(set(m.name for m in o.data.materials)), 'bounds_m':[[min(v.co[a] for v in o.data.vertices),max(v.co[a] for v in o.data.vertices)] for a in range(3)], 'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_nonboundary_edges':sum(not e.is_manifold and not e.is_boundary for e in bm.edges),'degenerate_faces':sum(f.calc_area()<1e-12 for f in bm.faces),'negative_scale':any(x<0 for x in o.scale)}
    bm.free()
(S/'MeshStatistics.json').write_text(json.dumps(stats,indent=2))
# Export ONLY explicit model collections, with no original body, rig, lights or cameras.
for c,file in [(wc,'ProtectiveClothing_Worn.fbx'),(pc,'ProtectiveClothing_Pickup.fbx')]:
    bpy.ops.object.select_all(action='DESELECT')
    for o in c.objects:o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(P/file),use_selection=True,object_types={'MESH','EMPTY'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=False,bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='RELATIVE',embed_textures=False,mesh_smooth_type='FACE')
# Camera-ready review set. Pickup staging transforms happen AFTER export and are not runtime defaults.
for i,(k,o) in enumerate(pickup.items()):o.location=((i%3-1)*.46,1.00+(i//3)*.49,.0)
pc.hide_render=True; pc.hide_viewport=True
world=bpy.data.worlds.new('ReviewWorld');sc.world=world;world.use_nodes=True;next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(.12,.15,.18,1)
for name,loc,power,size in [('Key',(-3,-4,5),700,4),('Fill',(3,-2,3),500,3),('Rim',(0,3,4),850,3)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;o=bpy.data.objects.new(name,data);studio.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',camdata);studio.objects.link(cam);sc.camera=cam;camdata.type='ORTHO'
floor=box('ReviewFloor',(0,0,-.018),(20,20,.018),0,c=studio)
floor.data.materials.clear();fm=bpy.data.materials.new('ReviewFloor');fm.use_nodes=True;next(n for n in fm.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'].default_value=(.055,.067,.078,1);floor.data.materials.append(fm)
sc.render.engine='CYCLES';sc.cycles.samples=24;sc.cycles.use_denoising=True
sc.render.resolution_x=1100;sc.render.resolution_y=1100;sc.render.resolution_percentage=100
sc.view_settings.view_transform='AgX';sc.render.image_settings.file_format='PNG'
def render(name,eye,target,scale):
    cam.location=eye;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();camdata.ortho_scale=scale;sc.render.filepath=str(PRE/(name+'.png'));bpy.ops.render.render(write_still=True)
# Give the reference a neutral review material via object-linked override only, original datablock retained.
reviewmat=bpy.data.materials.new('Reference_ReviewGrey');reviewmat.use_nodes=True;next(n for n in reviewmat.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'].default_value=(.33,.38,.4,1)
for ms in body.material_slots:ms.link='OBJECT';ms.material=reviewmat
for name,eye in [('Worn_Front',(0,-4,.92)),('Worn_Back',(0,4,.92)),('Worn_Left',(4,0,.92)),('Worn_Right',(-4,0,.92)),('Worn_ThreeQuarter',(3,-4,2.3))]:render(name,eye,(0,0,.89),2.13)
for s,side in [(1,'L'),(-1,'R')]:
    render('Wrist_'+side,(s*.75,-1.7,1.95),(s*.69,.02,1.418),.47)
    render('Ankle_'+side,(s*.8,-1.5,.8),(s*.15,-.015,.17),.58)
wc.hide_render=True;refcol.hide_render=True;pc.hide_render=False;pc.hide_viewport=False
render('Pickup_All',(1.7,-1.8,3.2),(0,1.2,.06),1.7)
wc.hide_render=False;pc.hide_render=True
for k,o in worn.items():
    if k=='Jacket':o.location.z=.23
    elif k=='Trousers':o.location.z=-.03
    elif k.startswith('Glove'):o.location.x=.21 if k.endswith('L') else -.21
    else:o.location.x=.33 if k.endswith('L') else -.33
render('Components_Separated',(3,-5,2.9),(0,0,1.02),2.54)
for o in worn.values():o.location=(0,0,0)
refcol.hide_render=False
cam.location=(3,-4,2.3);cam.rotation_euler=(Vector((0,0,.89))-cam.location).to_track_quat('-Z','Y').to_euler();camdata.ortho_scale=2.13
# Pickups remain authored at local origin in saved file: staged layout is a separate review scene.
for o in pickup.values():o.location=(0,0,0)
bpy.context.view_layer.update()
pc.hide_render=True;pc.hide_viewport=True
for img in bpy.data.images:
    if img.source=='FILE' and img.name.startswith('PC_'):img.pack();img.filepath='//../Textures/'+Path(img.filepath).name
for ms in body.material_slots:ms.link='DATA'
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_distance=2.8;area.spaces.active.region_3d.view_location=(0,0,.9);area.spaces.active.clip_end=100
bpy.ops.object.select_all(action='DESELECT');jacket.select_set(True);bpy.context.view_layer.objects.active=jacket
bpy.ops.wm.save_as_mainfile(filepath=str(S/'ProtectiveClothing.blend'))
print('AUTHORING_COMPLETE',json.dumps({n:s['triangles'] for n,s in stats.items()}))
