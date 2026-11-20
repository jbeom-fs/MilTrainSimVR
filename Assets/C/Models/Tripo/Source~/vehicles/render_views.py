# Blender headless: 모델 하나를 여러 각도로 렌더해 한 장으로 붙인다 (한 번만 불러온다)
# blender -b --factory-startup --python render_views.py -- spec.json
# spec = {"fbx": ..., "out": png, "cell": [w, h], "cols": 4, "samples": 24,
#         "maps": {"basecolor":..., "normal":..., "metallic":..., "roughness":..., "metallicSmoothness":...}  (없으면 FBX 재질 그대로),
#         "views": [{"az": 30, "el": 18, "zoom": 1.0}], "backface_magenta": false}
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector

spec = json.load(open(sys.argv[sys.argv.index("--") + 1], encoding="utf-8"))
W, H = spec.get("cell", [640, 420])
COLS = spec.get("cols", 4)
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = "CYCLES"
try:
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for dv in prefs.devices:
        dv.use = dv.type == "OPTIX"
    sc.cycles.device = "GPU"
except Exception:
    sc.cycles.device = "CPU"
sc.cycles.samples = spec.get("samples", 24)
sc.cycles.use_denoising = True
sc.cycles.max_bounces = 4
sc.view_settings.view_transform = "Standard"
sc.render.resolution_x, sc.render.resolution_y = W, H
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGB"

if spec["fbx"].lower().endswith(".blend"):
    bpy.ops.wm.open_mainfile(filepath=spec["fbx"])
else:
    bpy.ops.import_scene.fbx(filepath=spec["fbx"])
sc = bpy.context.scene
objs = [o for o in sc.objects if o.type == "MESH"]
maps = spec.get("maps")
if maps:
    m = bpy.data.materials.new("PreviewMat")
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]

    def tex(path, non_color):
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = bpy.data.images.load(path)
        if non_color:
            t.image.colorspace_settings.name = "Non-Color"
        return t
    nt.links.new(tex(maps["basecolor"], False).outputs["Color"], bsdf.inputs["Base Color"])
    if maps.get("normal"):
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(tex(maps["normal"], True).outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    if maps.get("metallicSmoothness"):          # Unity 식: RGB = 금속, A = 매끈함
        t = tex(maps["metallicSmoothness"], True)
        nt.links.new(t.outputs["Color"], bsdf.inputs["Metallic"])
        inv = nt.nodes.new("ShaderNodeInvert")
        nt.links.new(t.outputs["Alpha"], inv.inputs["Color"])
        nt.links.new(inv.outputs["Color"], bsdf.inputs["Roughness"])
    else:
        if maps.get("metallic"):
            nt.links.new(tex(maps["metallic"], True).outputs["Color"], bsdf.inputs["Metallic"])
        if maps.get("roughness"):
            nt.links.new(tex(maps["roughness"], True).outputs["Color"], bsdf.inputs["Roughness"])
    for o in objs:
        slots = max(1, len(o.data.materials))
        o.data.materials.clear()
        for _ in range(slots):
            o.data.materials.append(m)
if spec.get("backface_magenta"):
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        nt = mat.node_tree
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        src = out.inputs["Surface"].links[0].from_socket
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Color"].default_value = (1, 0, 1, 1)
        mix = nt.nodes.new("ShaderNodeMixShader")
        nt.links.new(geo.outputs["Backfacing"], mix.inputs[0])
        nt.links.new(src, mix.inputs[1])
        nt.links.new(em.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs["Surface"])

pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
cen, size = (mn + mx) * 0.5, mx - mn
radius = size.length * 0.5

# 바닥 판 · 하늘빛 · 해
pl = bpy.data.meshes.new("Floor")
s = radius * 12
pl.from_pydata([(-s, -s, mn.z), (s, -s, mn.z), (s, s, mn.z), (-s, s, mn.z)], [], [(0, 1, 2, 3)])
fo = bpy.data.objects.new("Floor", pl)
sc.collection.objects.link(fo)
fm = bpy.data.materials.new("FloorMat")
fm.use_nodes = True
fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.28, 0.28, 0.29, 1)
fm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
pl.materials.append(fm)
world = bpy.data.worlds.new("W")
sc.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = (0.62, 0.68, 0.78, 1)
bg.inputs["Strength"].default_value = 0.9
sun = bpy.data.lights.new("Sun", "SUN")
sun.energy = 3.0
sun.angle = math.radians(6)
so = bpy.data.objects.new("Sun", sun)
sc.collection.objects.link(so)
so.rotation_euler = (math.radians(50), 0, math.radians(35))

cd = bpy.data.cameras.new("Cam")
cd.lens = 50
cam = bpy.data.objects.new("Cam", cd)
sc.collection.objects.link(cam)
sc.camera = cam
tmp = os.path.splitext(spec["out"])[0] + "_cells"
os.makedirs(tmp, exist_ok=True)
cells = []
for i, v in enumerate(spec["views"]):
    az, el = math.radians(v.get("az", 30)), math.radians(v.get("el", 15))
    d = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
    dist = radius / math.sin(cd.angle * 0.5) / v.get("zoom", 1.0) * 1.02
    tg = v.get("target", [0, 0, 0])              # 가운데에서 크기 비율만큼 옮긴 곳을 본다
    look = cen + Vector((tg[0] * size.x, tg[1] * size.y, tg[2] * size.z))
    cam.location = look + d * dist
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cd.clip_start, cd.clip_end = dist * 0.01, dist * 20
    path = os.path.join(tmp, f"v{i}.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path)
    a = np.empty(W * H * 4, np.float32)
    img.pixels.foreach_get(a)
    cells.append(a.reshape(H, W, 4))
    bpy.data.images.remove(img)
rows = []
for r in range(0, len(cells), COLS):
    row = cells[r:r + COLS]
    while len(row) < COLS:
        row.append(np.zeros((H, W, 4), np.float32))
    rows.append(np.concatenate(row, axis=1))
sheet = np.concatenate(rows[::-1], axis=0)      # Blender 그림은 아래 줄이 먼저
out = bpy.data.images.new("sheet", sheet.shape[1], sheet.shape[0], alpha=False)
out.pixels.foreach_set(sheet.ravel())
out.filepath_raw = spec["out"]
out.file_format = "PNG"
out.save()
print("VIEWS_DONE", json.dumps({"min": [round(v, 4) for v in mn], "max": [round(v, 4) for v in mx], "out": spec["out"]}))
