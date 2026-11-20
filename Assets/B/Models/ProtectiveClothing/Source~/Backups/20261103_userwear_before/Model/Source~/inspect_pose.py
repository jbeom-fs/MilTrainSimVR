import bpy,json
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath='D:/TeamProject/MilTrainSimVR/Assets/B/Models/ProtectiveClothing/Source~/ReferenceInspection.blend')
a=bpy.data.objects['Armature']; m=bpy.data.objects['Ch36']
d={b.name:{'head':list(a.matrix_world@b.head),'tail':list(a.matrix_world@b.tail),'matrix':[list(r) for r in a.matrix_world@b.matrix]} for b in a.pose.bones}
dg=bpy.context.evaluated_depsgraph_get(); ev=m.evaluated_get(dg); me=ev.to_mesh()
vs=[ev.matrix_world@v.co for v in me.vertices]
out={'pose':d,'modifiers':[(x.type,x.name) for x in m.modifiers], 'action':str(a.animation_data.action if a.animation_data else None),'frame':bpy.context.scene.frame_current}
out['group_bounds']={g.name:[[min(vs[v.index][k] for v in me.vertices if any(t.group==g.index and t.weight>.4 for t in v.groups)),max(vs[v.index][k] for v in me.vertices if any(t.group==g.index and t.weight>.4 for t in v.groups))] for k in range(3)] for g in m.vertex_groups if any(any(t.group==g.index and t.weight>.4 for t in v.groups) for v in me.vertices)}
open('D:/TeamProject/MilTrainSimVR/Assets/B/Models/ProtectiveClothing/Source~/PoseInspection.json','w').write(json.dumps(out,indent=2))
print(json.dumps({n:v for n,v in d.items() if any(s in n for s in ['Arm','ForeArm','Hand','Neck','Head','Foot']) and not any(s in n for s in ['Thumb','Index','Middle','Ring','Pinky'])}))
