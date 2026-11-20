# Blender headless: 받은 FBX 의 구조 · 크기 · 재질을 찍고, 안에 든 텍스처를 파일로 꺼낸다
# blender -b --factory-startup --python inspect_extract.py -- <fbx> <out_dir>
import bpy, sys, os, json
import numpy as np
from mathutils import Vector

fbx, out_dir = sys.argv[sys.argv.index("--") + 1:][:2]
os.makedirs(out_dir, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
rep = {"file": fbx, "objects": [], "images": [], "materials": []}
for ob in bpy.context.scene.objects:
    o = {"name": ob.name, "type": ob.type, "parent": ob.parent.name if ob.parent else None,
         "loc": [round(v, 4) for v in ob.location], "rot": [round(v, 4) for v in ob.rotation_euler],
         "scale": [round(v, 4) for v in ob.scale]}
    if ob.type == "MESH":
        me = ob.data
        me.calc_loop_triangles()
        co = np.empty(len(me.vertices) * 3, np.float64)
        me.vertices.foreach_get("co", co)
        co = co.reshape(-1, 3)
        mw = np.array(ob.matrix_world)
        ws = co @ mw[:3, :3].T + mw[:3, 3]
        o.update({"verts": len(me.vertices), "tris": len(me.loop_triangles), "uv_layers": [u.name for u in me.uv_layers],
                  "materials": [m.name if m else None for m in me.materials],
                  "world_min": ws.min(0).round(4).tolist(), "world_max": ws.max(0).round(4).tolist()})
    rep["objects"].append(o)
for m in bpy.data.materials:
    links = []
    if m.use_nodes:
        for l in m.node_tree.links:
            if l.from_node.type == "TEX_IMAGE" and l.from_node.image:
                links.append({"image": l.from_node.image.name, "to": l.to_node.type + "." + l.to_socket.name})
    rep["materials"].append({"name": m.name, "links": links})
for img in bpy.data.images:
    if img.type != "IMAGE" or img.size[0] == 0:
        continue
    ext = {"JPEG": ".jpg", "PNG": ".png"}.get(img.file_format, ".png")
    name = os.path.splitext(os.path.basename(img.name))[0] + ext
    path = os.path.join(out_dir, name)
    info = {"name": img.name, "size": list(img.size), "format": img.file_format, "packed": img.packed_file is not None,
            "filepath": img.filepath, "colorspace": img.colorspace_settings.name}
    try:
        img.filepath_raw = path
        img.save()
        info["saved"] = path
        info["bytes"] = os.path.getsize(path)
    except Exception as ex:
        info["error"] = str(ex)
    rep["images"].append(info)
print("@@JSON@@" + json.dumps(rep, ensure_ascii=False))
json.dump(rep, open(os.path.join(out_dir, "inspect.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
