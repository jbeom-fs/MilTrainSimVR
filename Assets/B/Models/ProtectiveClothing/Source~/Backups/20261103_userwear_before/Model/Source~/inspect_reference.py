import bpy, json, os
from mathutils import Vector
BASE = 'D:/TeamProject/MilTrainSimVR'
OUT = BASE + '/Assets/B/Models/ProtectiveClothing/Source~'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=BASE+'/Assets/B/Models/Mannequin/Mannequin.fbx', use_anim=False)
info={}
for o in bpy.context.scene.objects:
    d={'type':o.type,'matrix':[list(r) for r in o.matrix_world], 'dimensions':list(o.dimensions)}
    if o.type=='ARMATURE':
        d['bones']={b.name:{'head':list(o.matrix_world@b.head_local),'tail':list(o.matrix_world@b.tail_local),'parent':b.parent.name if b.parent else None} for b in o.data.bones}
    if o.type=='MESH':
        dg=bpy.context.evaluated_depsgraph_get(); ev=o.evaluated_get(dg); me=ev.to_mesh()
        vs=[ev.matrix_world@v.co for v in me.vertices]
        d['bounds']=[[min(v[a] for v in vs),max(v[a] for v in vs)] for a in range(3)]
        d['vertices']=len(me.vertices); d['groups']=[g.name for g in o.vertex_groups]
        d['slices']={str(z):[[round(min(v[a] for v in vs if abs(v.z-z)<.012),5),round(max(v[a] for v in vs if abs(v.z-z)<.012),5)] for a in range(2)] for z in [.05,.1,.2,.3,.45,.6,.75,.85,.95,1.,1.1,1.2,1.3,1.4,1.5,1.6] if any(abs(v.z-z)<.012 for v in vs)}
        ev.to_mesh_clear()
    info[o.name]=d
with open(OUT+'/ReferenceInspection.json','w') as f:json.dump(info,f,indent=2)
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/ReferenceInspection.blend')
print('Reference inspection saved')
