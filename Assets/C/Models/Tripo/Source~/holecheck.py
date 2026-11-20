# 구멍 · 뒤집힌 면 검사: 앞면은 흰색, 뒷면은 자홍색(빛 없이 그대로)으로 여러 방향에서 찍어 자홍 픽셀을 센다.
# Unity 는 뒷면을 안 그리니 보이는 뒷면 = 구멍. 원본과 비교한다.
# blender -b --factory-startup --python holecheck.py -- out.json label=fbx [label=fbx ...]
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
out_json = argv[0]
items = [a.split("=", 1) for a in argv[1:]]
W, H = 220, 280
AZ = list(range(0, 360, 15))
EL = [-12, 12, 35]
TMP = os.path.splitext(out_json)[0] + "_px"
os.makedirs(TMP, exist_ok=True)

res = {}
for label, fbx in items:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for d in prefs.devices:
        d.use = d.type == "OPTIX"
    sc.cycles.device = "GPU"
    sc.cycles.samples = 1
    sc.cycles.use_denoising = False
    sc.render.filter_size = 0.01
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x, sc.render.resolution_y = W, H
    sc.render.image_settings.file_format = "PNG"
    world = bpy.data.worlds.new("W")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0, 0, 0, 1)
    bpy.ops.import_scene.fbx(filepath=fbx)
    obs = [o for o in sc.objects if o.type == "MESH"]
    m = bpy.data.materials.new("HC")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    o_ = nt.nodes.new("ShaderNodeOutputMaterial")
    ef = nt.nodes.new("ShaderNodeEmission")
    ef.inputs["Color"].default_value = (1, 1, 1, 1)
    eb = nt.nodes.new("ShaderNodeEmission")
    eb.inputs["Color"].default_value = (1, 0, 1, 1)
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(geo.outputs["Backfacing"], mix.inputs[0])
    nt.links.new(ef.outputs[0], mix.inputs[1])
    nt.links.new(eb.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], o_.inputs["Surface"])
    for ob in obs:
        ob.data.materials.clear()
        ob.data.materials.append(m)
    pts = [ob.matrix_world @ Vector(c) for ob in obs for c in ob.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    # 바닥: 모델 밑면 높이의 넓은 검은 판 (양면 검정) — 게임에서 바닥이 가리는 곳(밑면 · 바닥 아래 시점)은 세지 않는다
    fl = bpy.data.meshes.new("Floor")
    R_ = (mx - mn).length * 20
    z_ = mn.z - 1e-4
    fl.from_pydata([(-R_, -R_, z_), (R_, -R_, z_), (R_, R_, z_), (-R_, R_, z_)], [], [(0, 1, 2, 3)])
    flo = bpy.data.objects.new("Floor", fl)
    sc.collection.objects.link(flo)
    fm = bpy.data.materials.new("FloorM")
    fm.use_nodes = True
    fm.node_tree.nodes.clear()
    fo = fm.node_tree.nodes.new("ShaderNodeOutputMaterial")
    fe = fm.node_tree.nodes.new("ShaderNodeEmission")
    fe.inputs["Color"].default_value = (0, 0, 0, 1)
    fm.node_tree.links.new(fe.outputs[0], fo.inputs["Surface"])
    fl.materials.append(fm)
    c = (mn + mx) * 0.5
    r = (mx - mn).length * 0.5
    cd = bpy.data.cameras.new("C")
    cd.lens = 50
    cam = bpy.data.objects.new("C", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    dist = r / math.sin(cd.angle * 0.5) * 1.02
    counts = []
    for el in EL:
        for az in AZ:
            a, e = math.radians(az), math.radians(el)
            d = Vector((math.sin(a) * math.cos(e), math.cos(a) * math.cos(e), math.sin(e)))
            cam.location = c + d * dist
            cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
            p = os.path.join(TMP, f"{label}_{el}_{az}.png")
            sc.render.filepath = p
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(p)
            px = np.empty(W * H * 4, np.float32)
            img.pixels.foreach_get(px)
            bpy.data.images.remove(img)
            px = px.reshape(-1, 4)
            mag = int(((px[:, 0] > 0.7) & (px[:, 1] < 0.3) & (px[:, 2] > 0.7)).sum())
            counts.append({"az": az, "el": el, "magenta": mag})
    res[label] = {"total": sum(x["magenta"] for x in counts), "views": counts}
    print("@@HC", label, res[label]["total"])
json.dump(res, open(out_json, "w"), indent=1)
