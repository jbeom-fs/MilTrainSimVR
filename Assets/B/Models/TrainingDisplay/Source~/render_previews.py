"""Render real Blender geometry; guide is temporary and never saved/exported."""
import bpy,pathlib,json
from mathutils import Vector
SRC=pathlib.Path(__file__).resolve().parent;PRE=SRC/'Previews';PRE.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(SRC/'TrainingDisplay.blend'))
report=json.loads((SRC/'validation.json').read_text(encoding='utf-8'))
def B(p):return Vector((p[0],-p[2],p[1]))
guide=bpy.data.materials.new('PREVIEW_ONLY_ExistingCanvasPosition');guide.use_nodes=True
n=next(n for n in guide.node_tree.nodes if n.type=='BSDF_PRINCIPLED');n.inputs['Base Color'].default_value=(.23,.37,.41,1);n.inputs['Roughness'].default_value=.95
for key,item in report['models'].items():
    w,h=item['screen_m'];tag=pathlib.Path(item['file']).stem
    sc=bpy.data.scenes.new('PREVIEW_ONLY_'+key);bpy.context.window.scene=sc
    for ob in bpy.data.collections[tag].objects:
        dup=ob.copy();dup.data=ob.data;sc.collection.objects.link(dup)
    mesh=bpy.data.meshes.new('PREVIEW_ONLY_UI_Guide')
    mesh.from_pydata([B((-w/2,-h/2,0)),B((w/2,-h/2,0)),B((w/2,h/2,0)),B((-w/2,h/2,0))],[],[(0,3,2,1)]);mesh.materials.append(guide)
    g=bpy.data.objects.new('PREVIEW_ONLY_ExistingCanvas_Guide_NOT_FBX',mesh);sc.collection.objects.link(g)
    world=bpy.data.worlds.new('PREVIEW_ONLY_World');world.use_nodes=True;sc.world=world
    bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.52,.56,.60,1);bg.inputs['Strength'].default_value=.7
    for name,loc,power,size in [('Key',(-1.2,2,1.8),190,2.5),('Fill',(2,1,.4),110,2),('Rear',(0,-1.8,1.5),220,2)]:
        data=bpy.data.lights.new('PREVIEW_ONLY_'+name,'AREA');data.energy=power;data.shape='DISK';data.size=size
        ob=bpy.data.objects.new('PREVIEW_ONLY_'+name,data);sc.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(-ob.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('PREVIEW_ONLY_Camera');cam=bpy.data.objects.new('PREVIEW_ONLY_Camera',data);sc.collection.objects.link(cam);sc.camera=cam;data.type='ORTHO';data.ortho_scale=.820
    sc.render.engine='BLENDER_EEVEE';sc.render.resolution_x=1000;sc.render.resolution_y=850;sc.render.resolution_percentage=100;sc.render.image_settings.file_format='PNG';sc.render.image_settings.color_mode='RGB';sc.view_settings.view_transform='AgX'
    target=B((0,0,.035))
    views=[('Front',(0,0,-1))]
    if key=='AED':views.extend([('Side',(1,0,0)),('Rear',(0,0,1)),('Oblique',(.9,.35,-1.8)),('RearOblique',(.9,.5,1.8))])
    for view,d in views:
        cam.location=target+B(d).normalized()*3;cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
        sc.render.filepath=str(PRE/f'{key}_{view}.png');bpy.ops.render.render(write_still=True)
        print('PREVIEW_SAVED',key,view,flush=True)
print('ALL_PREVIEWS_RENDERED; preview staging not saved into production blend',flush=True)
