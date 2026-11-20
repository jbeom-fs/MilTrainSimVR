# 전/후 비교 렌더 — 같은 각도로 행(모델)마다 렌더해 한 장으로 붙인다. 행 위에는 꼭짓점 수 띠.
# blender -b --factory-startup --python render_compare.py -- spec.json
# spec = {"out": png, "res": [w,h], "samples": 48,
#         "views": [{"az":30,"el":15,"zoom":1.0,"target":[0,0,0],"wire":false}],
#         "rows": [{"label": "...", "fbx": "...", "tex": "..."}]}
import bpy, sys, os, json, math, struct
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
spec = json.load(open(argv[0], encoding="utf-8"))
OUT = spec["out"]
TMP = os.path.splitext(OUT)[0] + "_cells"
os.makedirs(TMP, exist_ok=True)
W, H = spec.get("res", [560, 640])
SAMPLES = spec.get("samples", 48)
FONT = "C:/Windows/Fonts/malgunbd.ttf" if os.path.exists("C:/Windows/Fonts/malgunbd.ttf") else "C:/Windows/Fonts/malgun.ttf"


def unity_vertex_count(me):
    """(자리, 법선, UV) 가 모두 같은 모서리를 한 꼭짓점으로 — Unity 가져오기(weld)와 같은 셈."""
    n = len(me.loops)
    pos = np.empty(len(me.vertices) * 3, np.float32)
    me.vertices.foreach_get("co", pos)
    pos = pos.reshape(-1, 3)
    vi = np.empty(n, np.int32)
    me.loops.foreach_get("vertex_index", vi)
    nor = np.empty(n * 3, np.float32)
    me.corner_normals.foreach_get("vector", nor)
    nor = nor.reshape(-1, 3)
    if me.uv_layers.active:
        uv = np.empty(n * 2, np.float32)
        me.uv_layers.active.data.foreach_get("uv", uv)
        uv = uv.reshape(-1, 2)
    else:
        uv = np.zeros((n, 2), np.float32)
    # 법선은 Unity 도 float 로 들고 있어 아주 작은 차이는 다른 꼭짓점 — 소수 5자리로 맞춘다
    key = np.concatenate([pos[vi], np.round(nor, 5), uv], axis=1)
    return len(np.unique(key, axis=0))


def gpu_setup(scene):
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    for dev_type in ("OPTIX", "CUDA"):
        try:
            prefs.compute_device_type = dev_type
            prefs.get_devices()
            ok = False
            for d in prefs.devices:
                d.use = d.type == dev_type
                ok |= d.use
            if ok:
                scene.cycles.device = "GPU"
                break
        except Exception:
            pass
    scene.cycles.samples = SAMPLES
    scene.cycles.use_denoising = True
    try:
        scene.cycles.denoiser = "OPTIX"
    except Exception:
        pass
    scene.cycles.max_bounces = 4
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "Standard"
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"


def make_world(scene):
    world = bpy.data.worlds.new("W")
    scene.world = world
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld")
    bg_light = nt.nodes.new("ShaderNodeBackground")
    bg_light.inputs["Color"].default_value = (0.55, 0.58, 0.62, 1)
    bg_light.inputs["Strength"].default_value = 0.6
    bg_cam = nt.nodes.new("ShaderNodeBackground")
    bg_cam.inputs["Color"].default_value = (0.16, 0.17, 0.19, 1)
    lp = nt.nodes.new("ShaderNodeLightPath")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(lp.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(bg_light.outputs[0], mix.inputs[1])
    nt.links.new(bg_cam.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])


def add_light(name, loc, energy, size, target):
    ld = bpy.data.lights.new(name, "AREA")
    ld.energy = energy
    ld.size = size
    lo = bpy.data.objects.new(name, ld)
    bpy.context.scene.collection.objects.link(lo)
    lo.location = loc
    d = Vector(target) - Vector(loc)
    lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    return lo


def model_material(tex_path, wire):
    m = bpy.data.materials.new("M")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.7     # Unity 재질 Smoothness 0.3
    bsdf.inputs["Metallic"].default_value = 0.0
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(tex_path, check_existing=True)
    # Unity 가져오기 한도(maxTextureSize 2048)와 같게 줄여서 본다
    tmax = spec.get("tex_max", 2048)
    if tmax and max(tex.image.size) > tmax:
        w, h = tex.image.size
        tex.image.scale(int(w * tmax / max(w, h)), int(h * tmax / max(w, h)))
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    shader = bsdf.outputs[0]
    if wire:
        wf = nt.nodes.new("ShaderNodeWireframe")
        wf.use_pixel_size = True
        wf.inputs["Size"].default_value = 1.1
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Color"].default_value = (1.0, 0.45, 0.05, 1)
        em.inputs["Strength"].default_value = 1.0
        mw = nt.nodes.new("ShaderNodeMixShader")
        nt.links.new(wf.outputs[0], mw.inputs[0])
        nt.links.new(shader, mw.inputs[1])
        nt.links.new(em.outputs[0], mw.inputs[2])
        shader = mw.outputs[0]
    # Unity URP Lit 은 뒷면을 그리지 않는다 — 뒷면은 투명하게 (구멍 · 뒤집힌 면이 그대로 보이게)
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mb = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(geo.outputs["Backfacing"], mb.inputs[0])
    nt.links.new(shader, mb.inputs[1])
    nt.links.new(tr.outputs[0], mb.inputs[2])
    nt.links.new(mb.outputs[0], out.inputs["Surface"])
    return m


def import_model(fbx, tex, wire):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=fbx)
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    mat = model_material(tex, wire)
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(mat)
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    count = sum(unity_vertex_count(o.data) for o in objs)
    tris = 0
    for o in objs:
        o.data.calc_loop_triangles()
        tris += len(o.data.loop_triangles)
    return objs, mn, mx, count, tris


def place_camera(cam, mn, mx, view):
    c = (mn + mx) * 0.5
    size = mx - mn
    t = view.get("target", [0, 0, 0])
    target = c + Vector((t[0] * size.x, t[1] * size.y, t[2] * size.z))
    radius = size.length * 0.5
    az, el = math.radians(view.get("az", 30)), math.radians(view.get("el", 15))
    fov = cam.data.angle
    dist = radius / math.sin(fov * 0.5) / view.get("zoom", 1.0) * 1.02
    d = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
    cam.location = target + d * dist
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam.data.clip_start = dist * 0.01
    cam.data.clip_end = dist * 10
    return target, dist, d


def render_to(path):
    sc = bpy.context.scene
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def label_strip(text, width, height, path, color=(1, 1, 1)):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    gpu_setup(sc)
    sc.cycles.samples = 4
    sc.cycles.use_denoising = False
    sc.render.resolution_x, sc.render.resolution_y = width, height
    world = bpy.data.worlds.new("W")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.06, 0.065, 0.075, 1)
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
    td.align_x = "LEFT"
    td.align_y = "CENTER"
    td.size = height / 100.0 * 0.5
    to = bpy.data.objects.new("T", td)
    sc.collection.objects.link(to)
    to.location = (-width / 200.0 + 0.18, 0, 0)
    m = bpy.data.materials.new("E")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = (*color, 1)
    nt.links.new(e.outputs[0], o.inputs["Surface"])
    td.materials.append(m)
    render_to(path)


def load_px(path):
    img = bpy.data.images.load(path)
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(h, w, 4)   # 아래 줄이 먼저 (Blender 순서)


def save_px(arr, path):
    h, w = arr.shape[:2]
    img = bpy.data.images.new("sheet", w, h, alpha=False)
    img.pixels.foreach_set(arr.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


cells, labels, info = [], [], []
for ri, row in enumerate(spec["rows"]):
    row_cells = []
    for vi, view in enumerate(spec["views"]):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        sc = bpy.context.scene
        gpu_setup(sc)
        sc.render.resolution_x, sc.render.resolution_y = W, H
        make_world(sc)
        objs, mn, mx, count, tris = import_model(row["fbx"], row["tex"], view.get("wire", False))
        cd = bpy.data.cameras.new("Cam")
        cd.lens = 50
        cd.sensor_fit = "AUTO"
        cam = bpy.data.objects.new("Cam", cd)
        sc.collection.objects.link(cam)
        sc.camera = cam
        target, dist, d = place_camera(cam, mn, mx, view)
        # 빛: 카메라 왼쪽 위 주광 + 오른쪽 보조광 — 모든 행 · 같은 각도에서 같다
        right = d.cross(Vector((0, 0, 1))).normalized()
        L = (mx - mn).length
        add_light("Key", target + (d * 1.2 - right * 0.9 + Vector((0, 0, 1.1))) * L * 1.6, 60 * L * L, L * 1.2, target)
        add_light("Fill", target + (d * 1.0 + right * 1.2 + Vector((0, 0, 0.3))) * L * 1.6, 22 * L * L, L * 1.5, target)
        add_light("Top", target + Vector((0, 0, 2.5)) * L, 18 * L * L, L * 2.0, target)
        p = os.path.join(TMP, f"r{ri}_v{vi}.png")
        render_to(p)
        row_cells.append(p)
        if vi == 0:
            info.append({"label": row["label"], "unity_verts": count, "tris": tris,
                         "min": list(mn), "max": list(mx)})
    cells.append(row_cells)

LABEL_H = 64
total_w = W * len(spec["views"])
for ri, row in enumerate(spec["rows"]):
    lab = row["label"]
    if "{verts}" in lab or "{tris}" in lab:
        lab = lab.format(verts=f"{info[ri]['unity_verts']:,}", tris=f"{info[ri]['tris']:,}")
    p = os.path.join(TMP, f"label_{ri}.png")
    label_strip(lab, total_w, LABEL_H, p, tuple(row.get("color", (1, 1, 1))))
    labels.append(p)

# 붙이기 (Blender 픽셀은 아래 줄부터라 위 행을 마지막에 쌓는다)
blocks = []
for ri in range(len(spec["rows"])):
    imgs = [load_px(p) for p in cells[ri]]
    row_img = np.concatenate(imgs, axis=1)
    lab = load_px(labels[ri])
    gap = np.full((6, total_w, 4), 0.02, np.float32)
    gap[..., 3] = 1
    blocks.append(np.concatenate([row_img, lab, gap], axis=0))
sheet = np.concatenate(blocks[::-1], axis=0)
save_px(sheet, OUT)
print("@@INFO" + json.dumps(info, ensure_ascii=False))
print("@@OUT", OUT)
