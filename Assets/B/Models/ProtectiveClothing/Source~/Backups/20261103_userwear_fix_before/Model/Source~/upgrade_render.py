import bpy,sys,math
from pathlib import Path
from mathutils import Vector
S=Path(__file__).resolve().parent;B=Path((S/'UpgradeBackupPath.txt').read_text())
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
before='before' in args;quick='quick' in args
source=B/'Source~'/'ProtectiveClothing.blend' if before else S/'ProtectiveClothing.blend'
bpy.ops.wm.open_mainfile(filepath=str(source));sc=bpy.context.scene
out=S/'Previews'/('Before' if before else ('Iteration01' if quick else ''))
out.mkdir(parents=True,exist_ok=True)
# Packed historical images are retained for the baseline comparison, even though
# the production paths now point to the revised files.
body=bpy.data.objects['Ch36'];review=bpy.data.materials['Reference_ReviewGrey']
for slot in body.material_slots:slot.link='OBJECT';slot.material=review
cam=sc.camera;sc.render.resolution_x=900 if quick else 1100;sc.render.resolution_y=sc.render.resolution_x
sc.cycles.samples=16 if quick else 24;sc.cycles.use_denoising=True
pc=bpy.data.collections['Pickup'];wc=bpy.data.collections['Worn'];ref=bpy.data.collections['Reference']
def shot(name,eye,target,scale):
    cam.location=eye;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
    sc.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
if quick:
    shot('Worn_ThreeQuarter',(3,-4,2.3),(0,0,.89),2.13)
    shot('Hood_Close',(.7,-1.8,1.94),(0,-.015,1.59),.66)
    shot('Wrist_L',(.76,-.08,2.5),(.755,.015,1.409),.44)
    shot('Ankle_L',(.8,-1.5,.8),(.15,-.015,.17),.58)
else:
    for name,eye in [('Worn_Front',(0,-4,.92)),('Worn_Back',(0,4,.92)),('Worn_Left',(4,0,.92)),('Worn_Right',(-4,0,.92)),('Worn_ThreeQuarter',(3,-4,2.3))]:shot(name,eye,(0,0,.89),2.13)
    shot('Hood_Close',(.7,-1.8,1.94),(0,-.015,1.59),.66)
    for s,side in [(1,'L'),(-1,'R')]:
        shot('Wrist_'+side,(s*.76,-.08,2.5),(s*.755,.015,1.409),.44)
        shot('Ankle_'+side,(s*.8,-1.5,.8),(s*.15,-.015,.17),.58)
wc.hide_render=True;ref.hide_render=True;pc.hide_render=False;pc.hide_viewport=False
pickups=[bpy.data.objects[n+'_Pickup'] for n in ['Jacket','Trousers','Glove_L','Overboot_L','Glove_R','Overboot_R']]
for i,o in enumerate(pickups):o.location=((i%3-1)*.46,1.00+(i//3)*.49,0)
bpy.context.view_layer.update();shot('Pickup_All',(1.7,-1.8,3.2),(0,1.2,.06),1.7)
if not quick:
    wc.hide_render=False;pc.hide_render=True
    for o in wc.objects:
        if o.type!='MESH':continue
        if o.name.startswith('Jacket'):o.location.z=.23
        elif o.name.startswith('Trousers'):o.location.z=-.03
        elif o.name.startswith('Glove'):o.location.x=.21 if '_L_' in o.name else -.21
        else:o.location.x=.33 if '_L_' in o.name else -.33
    bpy.context.view_layer.update();shot('Components_Separated',(3,-5,2.9),(0,0,1.02),2.54)
print('RENDER_COMPLETE',str(out))
