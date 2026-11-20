from pathlib import Path
import bpy,sys
S=Path(__file__).resolve().parent
sys.argv=[sys.argv[0]]
exec(compile((S/'jacket_final_review.py').read_text(encoding='utf8').split('# ---------- review entry point ----------')[0],str(S/'jacket_final_review.py'),'exec'))
PREV=WORK
for label,path in [('Baseline',None),('H4',S/'Backups/20261104_145838_h4_review_before/ProtectiveClothing.blend'),('Final',S/'ProtectiveClothing.blend')]:
    if path is None:load();previous_fix()
    else:
        bpy.ops.wm.open_mainfile(filepath=str(path))
        sc=bpy.context.scene;vl=bpy.context.view_layer
        wc=bpy.data.collections['Worn'];pc=bpy.data.collections['Pickup'];pa=bpy.data.objects['PC_Armature']
        arm=bpy.data.objects['Armature'];body=bpy.data.objects['Ch36'];jo=bpy.data.objects['Jacket_Worn']
        src_basis={pb.name:pb.matrix_basis.copy() for pb in arm.pose.bones}
    cam=setup_render();pose('ElbowBend')
    for side,sign in [('Left',1),('Right',-1)]:
        target=pa.matrix_world@pa.pose.bones[PFX+side+'ForeArm'].head
        eye=target+Vector((sign*1.6,1.6,.2))
        shot(cam,label+'_ElbowBend_BackElbow'+side,eye,target,.48)
print('ELBOW_DETAIL_DONE',flush=True)
