# 게임 속 거리 비교: 실제 크기로 세운 가구 몇 개를 눈높이에서 VR 시야로 — 행마다 (라벨, fbx, tex)
# blender -b --factory-startup --python render_ingame.py -- spec.json
# spec: {"out", "height_m", "count", "eye": [x,y,z], "look": [x,y,z], "fov_deg", "res": [w,h], "rows": [{"label","fbx","tex"}]}
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
spec = json.load(open(argv[0], encoding="utf-8"))
sys.path.insert(0, os.path.dirname(__file__))
W, H = spec.get("res", [1400, 800])
OUT = spec["out"]
TMP = os.path.splitext(OUT)[0] + "_cells"
os.makedirs(TMP, exist_ok=True)
FONT = "C:/Windows/Fonts/malgunbd.ttf"


def gpu(sc, samples):
    sc.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for d in prefs.devices:
        d.use = d.type == "OPTIX"
    sc.cycles.device = "GPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.view_settings.view_transform = "Standard"
    sc.render.image_settings.file_format = "PNG"


def flat_mat(name, rgb, rough=0.8):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    return m


def tex_mat(tex):
    m = bpy.data.materials.new("T")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.7
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(tex)
    if max(t.image.size) > 2048:
        t.image.scale(2048, 2048)
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    # 뒷면은 그리지 않는다 (Unity URP Lit)
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mx = nt.nodes.new("ShaderNodeMixShader")
    out = nt.nodes["Material Output"]
    nt.links.new(geo.outputs["Backfacing"], mx.inputs[0])
    nt.links.new(b.outputs[0], mx.inputs[1])
    nt.links.new(tr.outputs[0], mx.inputs[2])
    nt.links.new(mx.outputs[0], out.inputs["Surface"])
    return m


def label_png(text, width, height, path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    gpu(sc, 4)
    sc.cycles.use_denoising = False
    sc.render.resolution_x, sc.render.resolution_y = width, height
    w = bpy.data.worlds.new("W")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.06, 0.065, 0.075, 1)
    cd = bpy.data.cameras.new("C")
    cd.type = "ORTHO"
    cd.ortho_scale = width / 100.0
    cam = bpy.data.objects.new("C", cd)
    sc.collection.objects.link(cam)
    cam.location = (0, 0, 10)
    sc.camera = cam
    td = bpy.data.curves.new("T", "FONT")
    td.body = text
    td.font = bpy.data.fonts.load(FONT)
    td.align_y = "CENTER"
    td.size = height / 100.0 * 0.5
    to = bpy.data.objects.new("T", td)
    sc.collection.objects.link(to)
    to.location = (-width / 200.0 + 0.18, 0, 0)
    m = bpy.data.materials.new("E")
    m.use_nodes = True
    m.node_tree.nodes.clear()
    o = m.node_tree.nodes.new("ShaderNodeOutputMaterial")
    e = m.node_tree.nodes.new("ShaderNodeEmission")
    nt = m.node_tree
    nt.links.new(e.outputs[0], o.inputs["Surface"])
    td.materials.append(m)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def px(path):
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.empty(w * h * 4, np.float32)
    im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    return a.reshape(h, w, 4)


cells = []
for ri, row in enumerate(spec["rows"]):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    gpu(sc, spec.get("samples", 48))
    sc.render.resolution_x, sc.render.resolution_y = W, H
    w = bpy.data.worlds.new("W")
    sc.world = w
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.52, 0.55, 1)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.2
    bpy.ops.import_scene.fbx(filepath=row["fbx"])
    src = [o for o in sc.objects if o.type == "MESH"][0]
    src.data.materials.clear()
    src.data.materials.append(tex_mat(row["tex"]))
    # 원본 경계로 크기를 맞춘다 (빌더와 같게: 높이를 height_m 로)
    bb = [src.matrix_world @ Vector(c) for c in src.bound_box]
    mn = Vector((min(p.x for p in bb), min(p.y for p in bb), min(p.z for p in bb)))
    mx = Vector((max(p.x for p in bb), max(p.y for p in bb), max(p.z for p in bb)))
    s = spec["height_m"] / (mx.z - mn.z)
    width = (mx.x - mn.x) * s
    n = spec.get("count", 3)
    objs = [src] + [src.copy() for _ in range(n - 1)]
    for o in objs[1:]:
        sc.collection.objects.link(o)
    base_m = src.matrix_world.copy()
    for i, o in enumerate(objs):
        x = (i - (n - 1) / 2) * width
        flip = Matrix.Rotation(math.pi, 4, "Z") if i % 2 else Matrix.Identity(4)
        c = (mn + mx) / 2
        o.matrix_world = Matrix.Translation((x, 0, 0)) @ Matrix.Scale(s, 4) @ flip @ Matrix.Translation((-c.x, -c.y, -mn.z)) @ base_m
    # 바닥 · 뒷벽
    depth = (mx.y - mn.y) * s
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
    bpy.context.object.data.materials.append(flat_mat("F", (0.32, 0.31, 0.3)))
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, depth / 2 + 0.02, 15), rotation=(math.pi / 2, 0, 0))
    bpy.context.object.data.materials.append(flat_mat("Wl", (0.62, 0.6, 0.57)))
    # 천장 등 · 창 빛
    for k, (lx, ly, lz, e) in enumerate([(0, -1.5, 3.0, 160), (-2.5, -2.5, 2.4, 45), (2.5, -2.5, 2.4, 45)]):
        ld = bpy.data.lights.new(f"L{k}", "AREA")
        ld.energy = e
        ld.size = 1.5
        lo = bpy.data.objects.new(f"L{k}", ld)
        sc.collection.objects.link(lo)
        lo.location = (lx, ly, lz)
        lo.rotation_euler = (Vector((0, 0, 1.0)) - Vector((lx, ly, lz))).to_track_quat("-Z", "Y").to_euler()
    cd = bpy.data.cameras.new("Cam")
    cd.sensor_fit = "HORIZONTAL"
    cd.angle = math.radians(spec.get("fov_deg", 90))
    cam = bpy.data.objects.new("Cam", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    eye, look = Vector(spec["eye"]), Vector(spec["look"])
    cam.location = eye
    cam.rotation_euler = (look - eye).to_track_quat("-Z", "Y").to_euler()
    p = os.path.join(TMP, f"row{ri}.png")
    sc.render.filepath = p
    bpy.ops.render.render(write_still=True)
    cells.append(p)

blocks = []
for ri, row in enumerate(spec["rows"]):
    lp = os.path.join(TMP, f"label{ri}.png")
    label_png(row["label"], W, 60, lp)
    gap = np.full((6, W, 4), 0.02, np.float32)
    gap[..., 3] = 1
    blocks.append(np.concatenate([px(cells[ri]), px(lp), gap], 0))
sheet = np.concatenate(blocks[::-1], 0)
img = bpy.data.images.new("S", sheet.shape[1], sheet.shape[0])
img.pixels.foreach_set(sheet.ravel())
img.filepath_raw = OUT
img.file_format = "PNG"
img.save()
print("@@OUT", OUT)
