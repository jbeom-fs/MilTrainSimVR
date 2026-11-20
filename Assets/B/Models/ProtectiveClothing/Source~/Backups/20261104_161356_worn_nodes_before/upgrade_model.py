"""Improve appearance by replacing mesh DATA only. Object/anchor contracts stay fixed."""
import bpy,bmesh,math,json,hashlib,struct
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
S=Path(__file__).resolve().parent;P=S.parent;B=Path((S/'UpgradeBackupPath.txt').read_text())
BASE=json.loads((S/'UpgradeBaseline.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(B/'Source~'/'ProtectiveClothing.blend'))
sc=bpy.context.scene;wc=bpy.data.collections['Worn'];pc=bpy.data.collections['Pickup'];pc.hide_viewport=False
bpy.context.view_layer.update()
original_names=set(bpy.data.objects); originals={n:o for n,o in bpy.data.objects.items()}
original_mesh_names={n:o.data.name for n,o in originals.items() if o.type=='MESH'}
contract={n:(o.matrix_local.copy(),o.parent.name if o.parent else None) for n,o in originals.items()}
body=bpy.data.objects['Ch36'];dg=bpy.context.evaluated_depsgraph_get();ev=body.evaluated_get(dg);me=ev.to_mesh()
RV=[ev.matrix_world@v.co for v in me.vertices];RF=[tuple(f.vertices) for f in me.polygons];ev.to_mesh_clear();REF=BVHTree.FromPolygons(RV,RF)
temp=bpy.data.collections.new('_Authoring_Temporary');sc.collection.children.link(temp)
mat=bpy.data.materials['PC_Shared']
for n in mat.node_tree.nodes:
    if n.type=='TEX_IMAGE' and n.image and n.image.name.startswith('PC_'):
        path=P/'Textures'/Path(n.image.filepath).name
        if n.image.packed_file:n.image.unpack(method='REMOVE')
        n.image.filepath=str(path);n.image.reload()
bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Metallic'].default_value=0
bs.inputs['IOR'].default_value=1.46
if 'Specular IOR Level' in bs.inputs:bs.inputs['Specular IOR Level'].default_value=.42
for n in mat.node_tree.nodes:
    if n.type=='NORMAL_MAP':n.inputs['Strength'].default_value=.45
def activate(o):
    bpy.ops.object.select_all(action='DESELECT');o.hide_set(False);o.select_set(True);bpy.context.view_layer.objects.active=o
def make(name,verts,faces):
    m=bpy.data.meshes.new(name+'_newMesh');m.from_pydata(verts,[],faces);m.update();o=bpy.data.objects.new('_tmp_'+name,m);temp.objects.link(o);return o
def apply(o,m):activate(o);bpy.ops.object.modifier_apply(modifier=m.name)
def normals(o):
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
def solidify(o,t=.002):
    normals(o);m=o.modifiers.new('ThinRims','SOLIDIFY');m.thickness=t;m.offset=-1;apply(o,m)
def finish_uv(o,style='camo',kind='torso'):
    if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
    uv=o.data.uv_layers.active.data
    for p in o.data.polygons:
        coords=[o.data.vertices[i].co for i in p.vertices];center=sum(coords,Vector())/len(coords)
        vals=[]
        for co in coords:
            x,y,z=co
            if style=='rubber':
                vals.append((.79+((x*1.17+y*.38)%1)*.17,.56+((z*1.8+y*.65)%1)*.39));continue
            if style=='olive':
                vals.append((.79+((x*2.0+y*.8)%1)*.17,.28+((z*2.2+y*.4)%1)*.19));continue
            if style=='thread':
                vals.append((.80+((x*3.7+y*2.1)%1)*.15,.035+((z*4.3+y*.7)%1)*.17));continue
            if kind=='torso' and abs(center.x)>.205:
                cy=.031+.012*math.exp(-((abs(x)-.42)/.12)**2);cz=1.427
                ang=math.atan2(z-cz,y-cy)+math.pi;circ=.44
                vals.append((.04+ang/(2*math.pi)*circ/1.5*.72,.035+(abs(x)-.13)/1.5*.72))
            elif kind=='legs' and center.z<.90:
                cx=(1 if x>0 else -1)*(.151-(z-.1)*.083)
                ang=math.atan2(y-.005,x-cx)+math.pi
                vals.append((.13+ang/(2*math.pi)*.57/1.5*.72,.05+(z-.18)/1.5*.72))
            else:
                ang=math.atan2(y+.018,x)+math.pi
                vals.append((.018+ang/(2*math.pi)*1.18/1.5*.72,.025+(z-.90)/1.5*.72))
        # Repair azimuth seam per polygon, while leaving a gutter before the swatches.
        if style=='camo' and max(v[0] for v in vals)-min(v[0] for v in vals)>.20:
            period=(.44 if kind=='torso' and abs(center.x)>.205 else (.57 if kind=='legs' and center.z<.90 else 1.18))/1.5*.72
            if max(v[0] for v in vals)-min(v[0] for v in vals)>period*.65:vals=[(u+period if u<min(t[0] for t in vals)+period*.35 else u,v) for u,v in vals]
        for li,co in zip(p.loop_indices,vals):uv[li].uv=co
    o.data.materials.clear();o.data.materials.append(mat)
    for p in o.data.polygons:p.use_smooth=True
def planar_uv(o,style='camo',plane='xz',offset=(.11,.09)):
    if style!='camo':finish_uv(o,style);return
    if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
    axes={'xz':(0,2),'xy':(0,1),'yz':(1,2)}[plane]
    for p in o.data.polygons:
        for li in p.loop_indices:
            co=o.data.vertices[o.data.loops[li].vertex_index].co
            along=co[axes[1]]
            if axes[1]==2 and along>.90:along-=.90
            o.data.uv_layers.active.data[li].uv=(offset[0]+co[axes[0]]/1.5*.72,offset[1]+along/1.5*.72)
    o.data.materials.clear();o.data.materials.append(mat)
    for p in o.data.polygons:p.use_smooth=True
def tube(name,rings,axis='z',n=40,style='camo',cap=False,wrinkle=0):
    # ring: longitudinal, centreA, centreB, radiusA, radiusB
    v=[];f=[]
    for j,(t,a,b,ra,rb) in enumerate(rings):
        for i in range(n):
            th=2*math.pi*i/n;w=1+wrinkle*math.sin(th*7+j*.83)+wrinkle*.4*math.cos(th*3-j*1.4)
            u=a+ra*math.cos(th)*w;vv=b+rb*math.sin(th)*w
            v.append((u,vv,t) if axis=='z' else ((t,u,vv) if axis=='x' else (u,t,vv)))
    for j in range(len(rings)-1):
        for i in range(n):a=j*n+i;b=j*n+(i+1)%n;f.append((a,b,b+n,a+n))
    if cap:f.extend([tuple(reversed(range(n))),tuple((len(rings)-1)*n+i for i in range(n))])
    o=make(name,v,f);normals(o);finish_uv(o,style);return o
def merge(parts,target):
    # Keep the original object identity, parent, matrix, custom props and anchor children.
    vs=[];fs=[];uvs=[]
    for o in parts:
        start=len(vs);vs.extend(tuple(o.matrix_world@v.co) for v in o.data.vertices)
        for p in o.data.polygons:
            fs.append(tuple(start+i for i in p.vertices));uvs.append([tuple(o.data.uv_layers.active.data[li].uv) for li in p.loop_indices])
    m=bpy.data.meshes.new(target.name+'_Improved');m.from_pydata(vs,[],fs);m.update();m.materials.append(mat);uv=m.uv_layers.new(name='UVMap')
    for p,coords in zip(m.polygons,uvs):
        for li,co in zip(p.loop_indices,coords):uv.data[li].uv=co
        p.use_smooth=True
    target.data=m
    for o in parts:bpy.data.objects.remove(o,do_unlink=True)
def extract_outer(name,count):
    src=bpy.data.objects[name];cutoff=count//2
    o=make(name+'_Shell',[tuple(v.co) for v in src.data.vertices[:cutoff]],[tuple(p.vertices) for p in src.data.polygons if all(i<cutoff for i in p.vertices)])
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000002)
    bmesh.ops.subdivide_edges(bm,edges=list(bm.edges),cuts=1,use_grid_fill=True)
    movable=[v for v in bm.verts if not v.is_boundary]
    for _ in range(4):bmesh.ops.smooth_vert(bm,verts=movable,factor=.30,use_axis_x=True,use_axis_y=True,use_axis_z=True)
    bm.to_mesh(o.data);bm.free();normals(o);return o
def clamp_to_body(o,margin=.004):
    for v in o.data.vertices:
        loc,no,idx,dist=REF.find_nearest(v.co)
        if (v.co-loc).dot(no)<margin:v.co=loc+no*margin
def reduce(o,ratio):
    m=o.modifiers.new('SilhouetteBudget','DECIMATE');m.ratio=ratio;apply(o,m)
def seam(name,points,width=.0015,style='thread',direction=Vector((0,-1,0))):
    vs=[];fs=[]
    for i,p in enumerate(points):
        p=Vector(p);t=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        side=t.cross(direction).normalized()*width/2
        vs.extend([p-side,p+side])
        if i:fs.append((2*i-2,2*i-1,2*i+1,2*i))
    o=make(name,vs,fs);normals(o);finish_uv(o,style);return o
def surface_y(o,x,z):
    tree=BVHTree.FromPolygons([v.co.copy() for v in o.data.vertices],[list(f.vertices) for f in o.data.polygons])
    loc,no,index,dist=tree.ray_cast(Vector((x,-.55,z)),Vector((0,1,0)),1.1)
    return loc.y if loc else -.16
def pocket(name,cx,cz,w,h,base,depth=.009):
    # A curved textile envelope, a softly peaked flap, and sewn perimeter. No boxes.
    nx,nz=8,8;v=[];f=[]
    for j in range(nz+1):
        vv=j/nz;z=cz+(vv-.5)*h
        for i in range(nx+1):
            u=i/nx;x=cx+(u-.5)*w
            y=surface_y(base,x,z)-.003-depth*(math.sin(math.pi*u)**.6)*(math.sin(math.pi*vv)**.45)
            y-=.0025*math.sin(4*math.pi*u+.3)*math.sin(math.pi*vv)
            v.append((x,y,z))
    for j in range(nz):
        for i in range(nx):a=j*(nx+1)+i;f.append((a,a+1,a+nx+2,a+nx+1))
    po=make(name,v,f);normals(po);solidify(po,.0017);planar_uv(po,plane='xz',offset=(.30,.03))
    fv=[];ff=[]
    for j in range(4):
        for i in range(9):
            u=i/8;x=cx+(u-.5)*(w+.009);z=cz+h*.5+.007-j/3*.031-.006*(1-abs(2*u-1))*(j/3)
            y=surface_y(base,x,z)-.007-depth*.6*math.sin(math.pi*u)-.002*math.sin(j*math.pi/3)
            fv.append((x,y,z))
    for j in range(3):
        for i in range(8):a=j*9+i;ff.append((a,a+1,a+10,a+9))
    flap=make(name+'Flap',fv,ff);solidify(flap,.0017);planar_uv(flap,plane='xz',offset=(.30,.03))
    edge=[Vector(v[i]) for i in range(0,nx+1)]+[Vector(v[j*(nx+1)+nx]) for j in range(1,nz+1)]
    for p in edge:p.y-=.001
    return [po,flap,seam(name+'Seam',edge)]
# Jacket: refine the existing fitted outer silhouette rather than moving its object.
j=extract_outer('Jacket_Worn',3082)
for v in j.data.vertices:
    x,y,z=v.co;a=abs(x)
    if a>.19:
        cy=.031;cz=1.427;rad=Vector((0,y-cy,z-cz));ang=math.atan2(z-cz,y-cy)
        env=.0075*math.exp(-((a-.435)/.105)**2)+.005*math.exp(-((a-.62)/.04)**2)+.004*math.exp(-((a-.22)/.055)**2)
        fold=env*(.65*math.sin(a*105+ang*2.7)+.35*math.sin(a*65-ang*4.0))
        if rad.length:v.co+=rad.normalized()*fold
    else:
        rad=Vector((x,y+.02,0));ang=math.atan2(y+.02,x)
        fold=.006*math.exp(-((z-1.055)/.10)**2)*math.sin(z*101+ang*2)+.0025*math.sin(ang*7+z*14)*math.sin((z-.94)*math.pi/.62)
        if rad.length:v.co+=rad.normalized()*fold
clamp_to_body(j,.006);reduce(j,.43)
jacket_outer=BVHTree.FromPolygons([v.co.copy() for v in j.data.vertices],[tuple(f.vertices) for f in j.data.polygons])
finish_uv(j,'camo','torso');solidify(j)
parts=[j]
# Thin curved storm flap follows the cloth, replacing the old protruding bar.
vs=[];fs=[]
for k in range(28):
    z=.956+k/27*.555
    for x in [-.016,.016]:vs.append((x,surface_y(j,x,z)-.0033-.001*math.sin(k*.8),z))
    if k:fs.append((2*k-2,2*k-1,2*k+1,2*k))
o=make('StormFlap',vs,fs);solidify(o,.0015);planar_uv(o,plane='xz',offset=(.29,.05));parts.append(o)
parts.append(seam('FlapStitch',[(x-.011,y-.001,z) for x,y,z in vs[::2]],.001))
for s in [-1,1]:
    parts+=pocket('ChestPocket',s*.108,1.277,.108,.117,j,.008)
    parts+=pocket('LowerPocket',s*.112,1.035,.103,.095,j,.010)
    rings=[]
    for i in range(7):
        t=.626+i/6*.051;r=.048+.002*math.sin(i*2.4);rings.append((s*t,.036,1.422,r,r*1.02))
    cu=tube('FabricWristGather',rings,'x',36,'camo',wrinkle=.018);solidify(cu);finish_uv(cu,'camo','torso');parts.append(cu)
# Olive shoulder cape, with side height following raised T-pose shoulders.
cv=[];cf=[];N=64
for jn in range(9):
    t=jn/8;rx=.085+(.296-.085)*t;ry=.078+(.181-.078)*t
    for i in range(N):
        a=2*math.pi*i/N
        hem=1.397+.125*math.cos(a)**2+.012*math.sin(a)
        z=1.535*(1-t)+hem*t+.006*math.sin(a*7+t*6)*t+.003*math.sin(a*11-t*5)*t*t
        cv.append((rx*math.cos(a),.023+ry*math.sin(a),z))
        if jn:cf.append(((jn-1)*N+i,(jn-1)*N+(i+1)%N,jn*N+(i+1)%N,jn*N+i))
cape=make('IntegratedOliveCape',cv,cf);normals(cape)
for vert in cape.data.vertices:
    loc,no,idx,dist=jacket_outer.find_nearest(vert.co)
    if (vert.co-loc).dot(no)<.006:vert.co=loc+no*.006
clamp_to_body(cape,.005);solidify(cape,.0022);finish_uv(cape,'olive');parts.append(cape)
hempts=[cape.data.vertices[i].co.copy() for i in range(8*N,9*N)];hempts.append(hempts[0])
parts.append(seam('CapeHem',hempts,.002,style='thread',direction=Vector((0,0,1))))
nv=[];nf=[]
for row in range(4):
    t=row/3
    for i in range(N):
        a=2*math.pi*i/N
        base=cape.data.vertices[i].co.copy()+Vector((.001*math.cos(a),.001*math.sin(a),.001))
        top=Vector((.098*math.cos(a),.020+.105*math.sin(a),1.551+.011*math.sin(a)))
        nv.append(base.lerp(top,t))
        if row:nf.append(((row-1)*N+i,(row-1)*N+(i+1)%N,row*N+(i+1)%N,row*N+i))
neck=make('IntegratedOliveNeckJoin',nv,nf);normals(neck)
clamp_to_body(neck,.005);solidify(neck,.0022);finish_uv(neck,'olive');parts.append(neck)
# Hood is an open face ring carried over the crown/back, not an extra wearable.
hood=tube('IntegratedOliveHood',[(-.143,0,1.650,.087,.103),(-.113,0,1.652,.105,.139),(-.062,0,1.650,.115,.150),(.002,0,1.650,.116,.151),(.060,0,1.650,.093,.132),(.094,0,1.650,.050,.084),(.107,0,1.650,.009,.025)],'y',56,'olive',cap=False,wrinkle=.012)
# Close only the small rear tip; face opening stays open with a thick cloth rim.
bm=bmesh.new();bm.from_mesh(hood.data);bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary and all(v.co.y>.10 for v in e.verts)],sides=0)
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=(0,0,1.540),plane_no=(0,0,-1),clear_outer=True,clear_inner=False)
bm.to_mesh(hood.data);bm.free();clamp_to_body(hood,.005)
solidify(hood,.0023);finish_uv(hood,'olive');parts.append(hood)
facepoints=[(-.087*math.cos(i*2*math.pi/72),-.145,1.65+.103*math.sin(i*2*math.pi/72)) for i in range(73)]
parts.append(seam('HoodFaceBinding',facepoints,.003,'thread',Vector((0,-1,0))))
merge(parts,bpy.data.objects['Jacket_Worn'])
# Trousers: knee/crotch/ankle folds and generous thigh volume.
tr=extract_outer('Trousers_Worn',2788)
for v in tr.data.vertices:
    x,y,z=v.co;s=1 if x>0 else -1
    if z<.90:
        cx=s*(.151-(z-.1)*.083);rad=Vector((x-cx,y-.006,0));a=math.atan2(y-.006,x-cx)
        env=.009*math.exp(-((z-.53)/.10)**2)+.008*math.exp(-((z-.29)/.065)**2)+.003*math.exp(-((z-.78)/.10)**2)
        d=env*(.68*math.sin(z*105+a*2.1)+.32*math.sin(z*63-a*3.6))
        if rad.length:v.co+=rad.normalized()*d
        if z<.33:
            fac=1-.12*math.exp(-((z-.247)/.04)**2);v.co.x=cx+(v.co.x-cx)*fac;v.co.y=.006+(v.co.y-.006)*fac
    else:
        rad=Vector((x,y+.02,0));a=math.atan2(y+.02,x)
        if rad.length:v.co+=rad.normalized()*(.004*math.sin(z*130+a*4))
clamp_to_body(tr,.007);reduce(tr,.43);finish_uv(tr,'camo','legs');solidify(tr)
parts=[tr]
for s in [-1,1]:
    parts+=pocket('ThighPocket',s*.136,.748,.100,.137,tr,.009)
    ri=[]
    for i in range(6):
        z=.235+i*.007;ri.append((z,s*.141,.035,.064+.002*math.sin(i*2),.074+.003*math.sin(i*2)))
    cuff=tube('AnkleGather',ri,'z',32,'camo',wrinkle=.03);solidify(cuff);finish_uv(cuff,'camo','legs');parts.append(cuff)
waist=tube('WaistGather',[(.970,0,-.024,.179,.152),(.980,0,-.024,.182,.155),(.991,0,-.024,.178,.150),(1.001,0,-.024,.180,.151)],'z',48,'camo',wrinkle=.016);solidify(waist);finish_uv(waist,'camo','legs');parts.append(waist)
merge(parts,bpy.data.objects['Trousers_Worn'])
# Hands use the actual reference finger topology, keeping all five inter-finger webs.
for s,side in [(1,'L'),(-1,'R')]:
    g=make('AnatomicalRubberHand',RV,[f for f in RF if all(s*RV[i].x>.640 for i in f)])
    bm=bmesh.new();bm.from_mesh(g.data);bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00005)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update()
    for v in bm.verts:
        x,y,z=v.co
        # Thin rubber preserves narrow finger separation; soft material fullness on hand back.
        puff=.0020+.0015*math.exp(-((abs(x)-.755)/.045)**2)*max(0,v.normal.z)
        v.co+=v.normal*puff
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=(s*.660,0,0),plane_no=(-s,0,0),clear_outer=True,clear_inner=False)
    bm.to_mesh(g.data);bm.free();normals(g);reduce(g,.70);finish_uv(g,'rubber');solidify(g,.0012)
    rings=[]
    for i in range(13):
        t=.601+i/12*.104;r=.066-(t-.601)/.104*.010+.0014*math.sin(i*1.9)
        rings.append((s*t,.037,1.423,r,r*1.03))
    cuff=tube('LongRubberGauntlet',rings,'x',40,'rubber',wrinkle=.018);clamp_to_body(cuff,.0045);solidify(cuff,.0022);parts=[g,cuff]
    rim=tube('RubberRolledCuff',[(s*.601,.037,1.423,.066,.068),(s*.607,.037,1.423,.067,.069),(s*.613,.037,1.423,.064,.066)],'x',40,'rubber');solidify(rim,.002);parts.append(rim)
    merge(parts,bpy.data.objects['Glove_'+side+'_Worn'])
# Overboots: continuous rubber instep and broad toe, folds plus side closure straps.
for s,side in [(1,'L'),(-1,'R')]:
    rings=[]
    profile=[(.006,.169,-.043,.075,.153),(.015,.169,-.043,.078,.155),(.032,.169,-.040,.079,.151),(.052,.166,-.030,.076,.144),(.073,.163,-.019,.072,.132),(.092,.158,.002,.069,.107),(.115,.152,.023,.067,.087),(.142,.149,.035,.069,.078),(.170,.147,.039,.074,.081),(.195,.145,.041,.078,.086),(.220,.143,.043,.081,.091),(.245,.141,.045,.085,.098),(.269,.139,.045,.088,.104),(.289,.138,.045,.089,.107)]
    for z,cx,cy,rx,ry in profile:rings.append((z,s*cx,cy,rx,ry))
    boot=tube('RubberOverboot',rings,'z',48,'rubber',cap=True,wrinkle=.0)
    # Remove top cap, retain the rubber sole. Gently asymmetrical left/right toe outline.
    bm=bmesh.new();bm.from_mesh(boot.data);bmesh.ops.delete(bm,geom=[f for f in bm.faces if len(f.verts)>4 and f.calc_center_median().z>.27],context='FACES');bm.to_mesh(boot.data);bm.free()
    for v in boot.data.vertices:
        x,y,z=v.co;env=.0055*math.exp(-((z-.147)/.055)**2)
        v.co.y+=env*math.sin(z*137+x*31)
        v.co.x+=s*.002*math.sin(y*35)*math.exp(-((z-.065)/.05)**2)
    solidify(boot,.0023);finish_uv(boot,'rubber');parts=[boot]
    sole=tube('OverbootSoleLip',[(.007,s*.169,-.043,.080,.158),(.015,s*.169,-.043,.082,.159),(.025,s*.169,-.040,.080,.155)],'z',48,'rubber');solidify(sole,.0025);parts.append(sole)
    collar=tube('OverbootRolledMouth',[(.277,s*.138,.045,.093,.112),(.287,s*.138,.045,.094,.113),(.293,s*.138,.045,.092,.111)],'z',48,'rubber');solidify(collar,.002);parts.append(collar)
    # Reference 03/04 shows a side overlap and short fastenings; no invented boot laces.
    vs=[];fs=[]
    for jn in range(12):
        z=.06+jn/11*.218
        for i in range(4):
            a=.32+i/3*.38;rx=.074+(z/.29)*.015;ry=.09+(z/.29)*.02
            vs.append((s*(.146+rx*math.cos(a)),.037+ry*math.sin(a),z))
            if jn and i:idx=jn*4+i;fs.append((idx-5,idx-4,idx,idx-1))
    panel=make('SideOverlap',vs,fs);solidify(panel,.002);finish_uv(panel,'rubber');parts.append(panel)
    for z in [.115,.185,.25]:
        vs=[];fs=[]
        for jn in range(3):
            zz=z+(jn-1)*.008
            for i in range(7):
                a=-.06+i/6*.69;rx=.079+(zz/.29)*.015;ry=.092+(zz/.29)*.02
                vs.append((s*(.146+rx*math.cos(a)),.038+ry*math.sin(a),zz))
                if jn and i:idx=jn*7+i;fs.append((idx-8,idx-7,idx,idx-1))
        st=make('ShortClosureStrap',vs,fs);solidify(st,.0025);finish_uv(st,'rubber');parts.append(st)
    merge(parts,bpy.data.objects['Overboot_'+side+'_Worn'])
# Folded pickups: rounded layered fabric, identifiable sleeves/pockets and waistband.
def folded_layer(name,w,d,z,h,phase=0):
    # A rounded superellipse footprint, with controlled soft wrinkles on its top/bottom.
    n=64;v=[];f=[]
    for j,(rr,zz) in enumerate([(.88,z-h/2),(.98,z-h*.35),(1,z),( .98,z+h*.35),(.87,z+h/2)]):
        for i in range(n):
            a=2*math.pi*i/n;c=math.cos(a);s=math.sin(a)
            x=w/2*math.copysign(abs(c)**.32,c)*rr;y=d/2*math.copysign(abs(s)**.32,s)*rr
            v.append((x,y,zz+.0017*math.sin(a*5+phase)*math.sin(j*math.pi/4)))
            if j:f.append(((j-1)*n+i,(j-1)*n+(i+1)%n,j*n+(i+1)%n,j*n+i))
    # Two inner top rings carry slight real cloth undulation; the underside remains closed.
    for j,rr in [(5,.52),(6,.10)]:
        for i in range(n):
            a=2*math.pi*i/n;c=math.cos(a);sn=math.sin(a)
            x=w/2*math.copysign(abs(c)**.32,c)*rr;y=d/2*math.copysign(abs(sn)**.32,sn)*rr
            zz=z+h/2+.0017*math.sin(x*65+y*24+phase)*math.sin(math.pi*rr)
            v.append((x,y,zz));f.append(((j-1)*n+i,(j-1)*n+(i+1)%n,j*n+(i+1)%n,j*n+i))
    f.extend([tuple(reversed(range(n))),tuple(6*n+i for i in range(n))])
    o=make(name,v,f);normals(o);planar_uv(o,plane='xy',offset=(.27,.27))
    # Physical perimeter-length UVs on the sides avoid collapsing cloth thickness.
    perimeter=[0.0]
    for i in range(1,n+1):perimeter.append(perimeter[-1]+(Vector(v[2*n+i%n])-Vector(v[2*n+(i-1)%n])).length)
    uv=o.data.uv_layers.active.data
    for poly in o.data.polygons:
        if abs(poly.normal.z)<.72:
            for li in poly.loop_indices:
                vi=o.data.loops[li].vertex_index;i=vi%n
                arc=perimeter[i]
                if i==0 and any(t%n==n-1 for t in poly.vertices):arc=perimeter[n]
                uv[li].uv=(.025+arc/1.5*.72,.28+v[vi][2]/1.5*.72)
    return o
for kind in ['Jacket','Trousers']:
    w,d=(.34,.28) if kind=='Jacket' else (.29,.32);parts=[]
    for i in range(3):parts.append(folded_layer('FoldedClothLayer',w-(i%2)*.012,d,.015+i*.021,.027,i*.9))
    if kind=='Jacket':
        for s in [-1,1]:
            sl=folded_layer('FoldedSleeve',.108,.232,.081,.026,s)
            for v in sl.data.vertices:v.co.x+=s*.103;v.co.y+=.005
            parts.append(sl)
            fl=folded_layer('FoldedPocketFlap',.082,.041,.097,.006,s)
            for v in fl.data.vertices:v.co.x+=s*.103;v.co.y-=.032
            parts.append(fl)
        band=folded_layer('FoldedThinStormFlap',.027,.238,.076,.004);parts.append(band)
        # Folded olive hood/cape remains within the original table footprint.
        hoodfold=folded_layer('FoldedOliveHood',.273,.068,.082,.022,1.6)
        for v in hoodfold.data.vertices:v.co.y+=.101
        finish_uv(hoodfold,'olive');parts.append(hoodfold)
    else:
        band=folded_layer('FoldedWaistband',.281,.049,.078,.021,.8)
        for v in band.data.vertices:v.co.y+=.116
        parts.append(band)
        parts.append(seam('FoldedLegCentre',[(0,-.142,.071),(0,-.07,.073),(0,.02,.074),(0,.11,.073)],.0016, direction=Vector((0,0,1))))
    merge(parts,bpy.data.objects[kind+'_Pickup'])
    folded=bpy.data.objects[kind+'_Pickup'];bottom=min(v.co.z for v in folded.data.vertices)
    for v in folded.data.vertices:v.co.z-=bottom
for kind in ['Glove_L','Glove_R','Overboot_L','Overboot_R']:
    target=bpy.data.objects[kind+'_Pickup'];src=bpy.data.objects[kind+'_Worn'];data=src.data.copy();target.data=data
    s=1 if kind.endswith('L') else -1;rot=Matrix.Rotation(s*math.pi/2,4,'Z') if kind.startswith('Glove') else Matrix.Identity(4)
    points=[rot@v.co for v in data.vertices];lo=Vector([min(v[a] for v in points) for a in range(3)]);hi=Vector([max(v[a] for v in points) for a in range(3)])
    c=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    for v,co in zip(data.vertices,points):v.co=co-c
# Repair topology, triangulate and record exact old/new bounds; anchors stay untouched.
stats={};health={}
for name in [k+'_'+state for state in ['Worn','Pickup'] for k in ['Jacket','Trousers','Glove_L','Glove_R','Overboot_L','Overboot_R']]:
    o=bpy.data.objects[name];normals(o)
    wanted=original_mesh_names[name]
    occupied=bpy.data.meshes.get(wanted)
    if occupied and occupied!=o.data:occupied.name='_PreservedUnused_'+wanted
    o.data.name=wanted
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001);bmesh.ops.dissolve_degenerate(bm,dist=.000001,edges=list(bm.edges));bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
    for poly in o.data.polygons:poly.use_smooth=True
    bounds=[[min(v.co[a] for v in o.data.vertices),max(v.co[a] for v in o.data.vertices)] for a in range(3)]
    stats[name]={'before_triangles':BASE['objects'][name]['triangles'],'triangles':len(o.data.polygons),'vertices':len(o.data.vertices),'before_material_slots':len(BASE['objects'][name]['materials']),'material_slots':len(o.data.materials),'bounds_before_m':BASE['objects'][name]['bounds_local'],'bounds_after_m':bounds,'size_before_m':[b-a for a,b in BASE['objects'][name]['bounds_local']],'size_after_m':[b-a for a,b in bounds]}
(S/'UpgradeStatistics.json').write_text(json.dumps(stats,indent=2))
(S/'MeshNamePreservation.json').write_text(json.dumps({n:{'before':original_mesh_names[n],'after':bpy.data.objects[n].data.name} for n in stats},indent=2))
# User additions and every original object are preserved. Only the 12 equipment DATA blocks changed.
assert set(bpy.data.objects)==original_names, (set(bpy.data.objects)-original_names,original_names-set(bpy.data.objects))
for n,(m,parent) in contract.items():
    o=bpy.data.objects[n];assert (o.parent.name if o.parent else None)==parent,n
    assert max(abs(o.matrix_local[i][j]-m[i][j]) for i in range(4) for j in range(4))<1e-8,n
bpy.data.collections.remove(temp)
for c,filename in [(wc,'ProtectiveClothing_Worn.fbx'),(pc,'ProtectiveClothing_Pickup.fbx')]:
    bpy.ops.object.select_all(action='DESELECT')
    expected={o.name for o in c.objects if o.type in {'MESH','EMPTY'}}
    for o in c.objects:o.select_set(o.name in expected)
    bpy.ops.export_scene.fbx(filepath=str(P/filename),use_selection=True,object_types={'MESH','EMPTY'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=False,bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='RELATIVE',embed_textures=False,mesh_smooth_type='FACE')
for img in bpy.data.images:
    if img.name.startswith('PC_'):
        img.pack();img.filepath='//../Textures/'+Path(img.filepath).name
bpy.context.view_layer.update();pc.hide_viewport=True;pc.hide_render=True
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(S/'ProtectiveClothing.blend'))
print('UPGRADE_SAVED',json.dumps({n:s['triangles'] for n,s in stats.items()}))
