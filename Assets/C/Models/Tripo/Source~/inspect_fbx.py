# Blender headless: FBX 구조 · 꼭짓점 수 · 경계 · 재질 · UV 를 찍는다
# blender -b --factory-startup --python inspect_fbx.py -- <fbx> [<fbx> ...]
import bpy, sys, json, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def unity_vertex_count(me, normal_round=4, uv_round=5):
    """Unity 가 가져올 때처럼 (자리, 법선, UV) 가 같은 모서리만 한 꼭짓점으로 친다."""
    uv = me.uv_layers.active.data if me.uv_layers.active else None
    try:
        cn = me.corner_normals
        normals = [tuple(round(c, normal_round) for c in cn[i].vector) for i in range(len(me.loops))]
    except AttributeError:
        me.calc_normals_split()
        normals = [tuple(round(c, normal_round) for c in l.normal) for l in me.loops]
    keys = set()
    for li, loop in enumerate(me.loops):
        u = tuple(round(c, uv_round) for c in uv[li].uv) if uv else ()
        keys.add((loop.vertex_index, normals[li], u))
    return len(keys)


def report(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    out = {"file": path, "objects": []}
    for ob in bpy.context.scene.objects:
        o = {
            "name": ob.name, "type": ob.type,
            "parent": ob.parent.name if ob.parent else None,
            "loc": [round(v, 5) for v in ob.location],
            "rot_deg": [round(math.degrees(v), 3) for v in ob.rotation_euler],
            "scale": [round(v, 5) for v in ob.scale],
        }
        if ob.type == "MESH":
            me = ob.data
            me.calc_loop_triangles()
            ws = [ob.matrix_world @ v.co for v in me.vertices]
            mn = Vector((min(p.x for p in ws), min(p.y for p in ws), min(p.z for p in ws)))
            mx = Vector((max(p.x for p in ws), max(p.y for p in ws), max(p.z for p in ws)))
            o.update({
                "mesh": me.name,
                "verts": len(me.vertices), "faces": len(me.polygons), "tris": len(me.loop_triangles),
                "loops": len(me.loops),
                "unity_verts": unity_vertex_count(me),
                "uv_layers": [u.name for u in me.uv_layers],
                "color_attrs": [c.name for c in me.color_attributes],
                "attrs": [a.name for a in me.attributes],
                "has_custom_normals": me.has_custom_normals,
                "materials": [m.name if m else None for m in me.materials],
                "world_min": [round(v, 5) for v in mn], "world_max": [round(v, 5) for v in mx],
                "local_dims": [round(v, 5) for v in ob.dimensions],
            })
            for m in me.materials:
                if m and m.use_nodes:
                    o.setdefault("textures", []).extend(
                        n.image.filepath for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image)
        out["objects"].append(o)
    print("@@JSON@@" + json.dumps(out, ensure_ascii=False))


for p in argv:
    report(p)
