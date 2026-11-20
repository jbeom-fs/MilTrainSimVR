# 마무리한 차를 Unity 에 넣기 전에 Blender 에서 미리 본다 — 유리 자리 알파 · 투명 유리 · 실내까지 재질을 엮어 여러 각도로 렌더해 한 장으로
# blender -b --factory-startup --python preview_vehicle.py -- cfg.json
# cfg = {"dir": 마무리 결과 폴더, "name": "Sedan", "out": png, "scale": [SX, SY, SZ], "front": "+X", "cell": [w, h], "cols": 4,
#        "views": [{"az":..,"el":..,"zoom":..,"target":[..]}  또는  {"eye":[x,y,z m], "look":[x,y,z m], "lens": 20}]}
import bpy, sys, os, json, math
import numpy as np
from mathutils import Vector

cfg = json.load(open(sys.argv[sys.argv.index("--") + 1], encoding="utf-8"))
D, NAME = cfg["dir"], cfg["name"]
SX, SY, SZ = cfg["scale"]
FS = -1.0 if cfg.get("front", "+X") == "-X" else 1.0
W, H = cfg.get("cell", [640, 400])
COLS = cfg.get("cols", 4)
bpy.ops.wm.open_mainfile(filepath=os.path.join(D, NAME + "_finished.blend"))
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
sc.cycles.samples = cfg.get("samples", 48)
sc.cycles.use_denoising = True
sc.cycles.max_bounces = 8
sc.cycles.transparent_max_bounces = 16
sc.view_settings.view_transform = "Standard"
sc.render.resolution_x, sc.render.resolution_y = W, H
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGB"


def tex(nt, file, non_color=False, closest=False):
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(os.path.join(D, file), check_existing=True)
    if non_color:
        t.image.colorspace_settings.name = "Non-Color"
    if closest:
        t.interpolation = "Closest"
    return t


def principled(mat):
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(b.outputs[0], out.inputs["Surface"])
    return nt, b


body = bpy.data.materials.get(NAME + "_Body")
if body:
    nt, b = principled(body)
    c = tex(nt, NAME + "_basecolor.png")
    nt.links.new(c.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(c.outputs["Alpha"], b.inputs["Alpha"])
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tex(nt, NAME + "_normal.png", True).outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
    m = tex(nt, NAME + "_metallicSmoothness.png", True)
    nt.links.new(m.outputs["Color"], b.inputs["Metallic"])
    inv = nt.nodes.new("ShaderNodeInvert")
    nt.links.new(m.outputs["Alpha"], inv.inputs["Color"])
    nt.links.new(inv.outputs["Color"], b.inputs["Roughness"])
    cc = tex(nt, NAME + "_clearcoat.png", True)
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(cc.outputs["Color"], sep.inputs["Color"])
    nt.links.new(sep.outputs["Red"], b.inputs["Coat Weight"])
    b.inputs["Coat Roughness"].default_value = 0.08
glass = bpy.data.materials.get(NAME + "_Glass")
if glass:
    nt, b = principled(glass)
    g = tex(nt, NAME + "_glass.png")
    nt.links.new(g.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(g.outputs["Alpha"], b.inputs["Alpha"])
    b.inputs["Roughness"].default_value = 0.05
shell = bpy.data.materials.get(NAME + "_Interior")
if shell:
    nt, b = principled(shell)
    c = tex(nt, NAME + "_basecolor.png")
    mul = nt.nodes.new("ShaderNodeMixRGB")
    mul.blend_type = "MULTIPLY"
    mul.inputs["Fac"].default_value = 1.0
    mul.inputs["Color2"].default_value = (0.024, 0.024, 0.027, 1.0)
    nt.links.new(c.outputs["Color"], mul.inputs["Color1"])
    nt.links.new(mul.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(c.outputs["Alpha"], b.inputs["Alpha"])
    b.inputs["Roughness"].default_value = 0.75
cabin = bpy.data.materials.get("Vehicle_Cabin")
if cabin:
    nt, b = principled(cabin)
    nt.links.new(tex(nt, "Vehicle_CabinPalette.png", closest=True).outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.7

# Unity 는 뒷면을 그리지 않는다(몸통 · 유리 · 안쪽 껍데기는 한 면) — 여기서도 뒷면을 투명하게 해야 같은 그림이 나온다.
# 안쪽 껍데기는 몸통 면을 뒤집어 겹쳐 놓은 것이라, 뒷면을 그리면 몸통 위에 검은 얼룩으로 비친다
for mat_ in (body, glass, shell):
    if not mat_:
        continue
    nt = mat_.node_tree
    out_ = next(n_ for n_ in nt.nodes if n_.type == "OUTPUT_MATERIAL")
    src = out_.inputs["Surface"].links[0].from_socket
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(geo.outputs["Backfacing"], mix.inputs[0])
    nt.links.new(src, mix.inputs[1])
    nt.links.new(tr.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out_.inputs["Surface"])

# 안쪽 껍데기 · 실내는 카메라에만 보이게 — Unity 는 광선을 쏘지 않아서, 몸통 바로 안쪽에 겹친 껍데기가 몸통에 그늘을 지우지 않는다
for o in sc.objects:
    if o.type == "MESH" and (o.name.endswith("_Interior") or o.name.endswith("_Cabin")):
        o.visible_shadow = False
        o.visible_diffuse = False
        o.visible_glossy = False
        o.visible_transmission = False

objs = [o for o in sc.objects if o.type == "MESH"]
for o in objs:
    if cfg.get("hide") and any(h in o.name for h in cfg["hide"]):
        o.hide_render = True
pts = [o.matrix_world @ Vector(cn) for o in objs for cn in o.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
cen, size = (mn + mx) * 0.5, mx - mn
radius = size.length * 0.5
pl = bpy.data.meshes.new("Floor")
s = radius * 14
pl.from_pydata([(-s, -s, mn.z), (s, -s, mn.z), (s, s, mn.z), (-s, s, mn.z)], [], [(0, 1, 2, 3)])
fo = bpy.data.objects.new("Floor", pl)
sc.collection.objects.link(fo)
fm = bpy.data.materials.new("FloorMat")
fm.use_nodes = True
fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.22, 0.22, 0.23, 1)
fm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
pl.materials.append(fm)
world = bpy.data.worlds.new("W")
sc.world = world
world.use_nodes = True
wn = world.node_tree
bg = wn.nodes["Background"]
sky = wn.nodes.new("ShaderNodeTexSky")
try:
    sky.sky_type = "HOSEK_WILKIE"
except Exception:
    pass
wn.links.new(sky.outputs[0], bg.inputs["Color"])
bg.inputs["Strength"].default_value = cfg.get("sky", 1.0)
sun = bpy.data.lights.new("Sun", "SUN")
sun.energy = 3.0
sun.angle = math.radians(4)
so = bpy.data.objects.new("Sun", sun)
sc.collection.objects.link(so)
so.rotation_euler = (math.radians(52), 0, math.radians(40))
cd = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cd)
sc.collection.objects.link(cam)
sc.camera = cam


def model(p):
    return Vector((p[0] / SX * FS, p[1] / SY * FS, p[2] / SZ + mn.z))


tmp = os.path.splitext(cfg["out"])[0] + "_cells"
os.makedirs(tmp, exist_ok=True)
cells = []
for i, v in enumerate(cfg["views"]):
    if "eye" in v:
        eye, look = model(v["eye"]), model(v["look"])
        cd.lens = v.get("lens", 20)
        cam.location = eye
        cam.rotation_euler = (look - eye).to_track_quat("-Z", "Y").to_euler()
        cd.clip_start, cd.clip_end = 0.002, 100
    else:
        cd.lens = 50
        az, el = math.radians(v.get("az", 30)), math.radians(v.get("el", 15))
        d = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
        dist = radius / math.sin(cd.angle * 0.5) / v.get("zoom", 1.0) * 1.02
        tg = v.get("target", [0, 0, 0])
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
sheet = np.concatenate(rows[::-1], axis=0)
out = bpy.data.images.new("sheet", sheet.shape[1], sheet.shape[0], alpha=False)
out.pixels.foreach_set(sheet.ravel())
out.filepath_raw = cfg["out"]
out.file_format = "PNG"
out.save()
print("PREVIEW_DONE", cfg["out"])
