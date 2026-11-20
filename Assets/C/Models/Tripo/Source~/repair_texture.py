# 이미 만든 후보 텍스처에서, 로그에 적힌 '빗나간 면'만 가장 가까운 원본 표면 색으로 다시 칠한다 (모양은 그대로)
# blender -b --factory-startup --python repair_texture.py -- <make_lowpoly 로그> ...
import bpy, bmesh, sys, os, json
import numpy as np
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(__file__))
import uvtools

argv = sys.argv[sys.argv.index("--") + 1:]


def load_obj(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before and o.type == "MESH"][0]


def load_px(path):
    im = bpy.data.images.load(path)
    w, h = im.size
    a = np.empty(w * h * 4, np.float32)
    im.pixels.foreach_get(a)
    return im, a.reshape(h, w, 4)


for log in argv:
    d = [json.loads(l[5:]) for l in open(log, encoding="utf-8", errors="replace") if l.startswith("@@LOG")][0]
    faces = (d.get("bake") or {}).get("dark_faces_repaired") or []
    sp = d["spec"]
    if not faces:
        print("@@REPAIR", os.path.basename(log), "nothing")
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    lo = load_obj(sp["out_fbx"])
    hi = load_obj(sp["src_fbx"])
    bl = bmesh.new()
    bl.from_mesh(lo.data)
    bl.faces.ensure_lookup_table()
    bh = bmesh.new()
    bh.from_mesh(hi.data)
    bh.faces.ensure_lookup_table()
    th = BVHTree.FromBMesh(bh)
    img, px = load_px(sp["out_tex"])
    _, opx = load_px(sp["tex"])
    # 로그의 면 번호는 파일 속 면 순서 — Blender 로 읽으면 겹친 면이 빠져 번호가 밀릴 수 있어, 파일에서 무게중심을 구해 짝을 찾는다
    import fbxio
    from mathutils import Vector
    from mathutils.kdtree import KDTree
    r_, _ = fbxio.load(sp["out_fbx"])
    g_ = fbxio.find(r_, b"Objects", b"Geometry")
    P_ = list(fbxio.find(g_, b"PolygonVertexIndex").props[0])
    V_ = list(fbxio.find(g_, b"Vertices").props[0])
    polys, cur = [], []
    for x in P_:
        cur.append(x if x >= 0 else -x - 1)
        if x < 0:
            polys.append(cur)
            cur = []
    kd = KDTree(len(bl.faces))
    for f in bl.faces:
        kd.insert(f.calc_center_median(), f.index)
    kd.balance()
    targets = []
    for fi in faces:
        if fi >= len(polys):
            continue
        c = sum((Vector(V_[3 * k:3 * k + 3]) for k in polys[fi]), Vector()) / len(polys[fi])
        co, idx, dist = kd.find(c)
        if dist < 1e-5:
            targets.append(bl.faces[idx])
    n = uvtools.repaint_faces_nearest(px, targets, bl.loops.layers.uv.active, th, bh, bh.loops.layers.uv.active, opx)
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = sp["out_tex"]
    img.file_format = "JPEG"
    img.save(filepath=sp["out_tex"], quality=sp.get("jpeg_quality", 95))
    print("@@REPAIR", os.path.basename(log), "faces", faces, "texels", n)
