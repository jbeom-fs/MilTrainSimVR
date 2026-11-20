# 차처럼 굽은 면이 많은 모델용 UV — Smart UV 조각을 그대로 쓰고(이어 붙여 다시 펴지 않는다), 겹친 면은 따로 떼어 낸다.
# make_prop_lowpoly 의 uvtools.unfold_seams 는 상자꼴 가구용이라, 10만 면 차체에서는 이어 붙인 섬이 찌그러졌다(11/10).
import bpy, bmesh, math
from mathutils import Vector
import uv_metrics


def _pack(ob, margin):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=True, scale=True, margin_method="FRACTION", margin=margin, shape_method="CONCAVE")
    bpy.ops.object.mode_set(mode="OBJECT")


def charts_only(ob, smart_angle_deg=66.0, margin=0.003, log=None, res=4096):
    me = ob.data
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(smart_angle_deg), island_margin=margin,
                             area_weight=0.0, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    _pack(ob, margin)
    detached = []
    for _ in range(4):
        bad = uv_metrics.overlapped_faces(me, res)
        if not bad:
            break
        # 겹친 면은 하나씩 따로 섬으로 — 서로 다른 곳으로 옮겨 놓고 다시 담는다
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        uvl = bm.loops.layers.uv.active
        for k, fi in enumerate(bad):
            off = Vector((3.0 + 0.02 * (k % 400), 3.0 + 0.02 * (k // 400)))
            for l in bm.faces[fi].loops:
                l[uvl].uv = l[uvl].uv + off
        bm.to_mesh(me)
        bm.free()
        detached.append(len(bad))
        _pack(ob, margin)
    left = len(uv_metrics.overlapped_faces(me, res))
    if log is not None:
        log["unfold"] = {"mode": "charts_only", "smart_angle": smart_angle_deg, "detached_faces_per_round": detached, "overlap_faces_left": left}
    return detached, left
