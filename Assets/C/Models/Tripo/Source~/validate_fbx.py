# 패치한 FBX 의 Geometry 배열 정합성 검사 + 원본과 Geometry 밖 트리가 같은지
# blender -b --factory-startup --python validate_fbx.py -- <원본.fbx> <후보.fbx> [...]
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import fbxio

argv = sys.argv[sys.argv.index("--") + 1:]
orig_path, cands = argv[0], argv[1:]


def same_outside_geometry(a, b, path=""):
    """Geometry 아래(배열)만 빼고 원본과 같은지 — 이름 · ID · 연결 · 설정"""
    if a.id != b.id:
        return f"id {path}: {a.id} != {b.id}"
    if a.id == b"Geometry" and path.endswith("/Objects"):
        # Geometry 머리(ID · 이름 · 종류)만 같으면 된다
        return None if list(a.props) == list(b.props) else f"geometry header {path}"
    if a.id == b"CreationTime" or a.id == b"CreationTimeStamp":
        return None
    if list(a.props_type) != list(b.props_type):
        return f"types {path}/{a.id.decode()}"
    for p, q in zip(a.props, b.props):
        if hasattr(p, "__len__") and not isinstance(p, (bytes, str)):
            if list(p) != list(q):
                return f"array {path}/{a.id.decode()}"
        elif p != q:
            return f"value {path}/{a.id.decode()}: {p!r} != {q!r}"
    if len(a.elems) != len(b.elems):
        return f"children count {path}/{a.id.decode()}"
    for x, y in zip(a.elems, b.elems):
        r = same_outside_geometry(x, y, path + "/" + a.id.decode())
        if r:
            return r
    return None


ro, vo = fbxio.load(orig_path)
for cp in cands:
    rc, vc = fbxio.load(cp)
    errs = []
    if vo != vc:
        errs.append(f"version {vo} != {vc}")
    for x, y in zip(ro.elems, rc.elems):
        r = same_outside_geometry(x, y)
        if r:
            errs.append("tree: " + r)
    g = fbxio.find(rc, b"Objects", b"Geometry")
    V = list(fbxio.find(g, b"Vertices").props[0])
    P = list(fbxio.find(g, b"PolygonVertexIndex").props[0])
    nv = len(V) // 3
    if len(V) % 3:
        errs.append("Vertices not multiple of 3")
    if not P or P[-1] >= 0:
        errs.append("last polygon not closed")
    npoly = sum(1 for p in P if p < 0)
    idx = [p if p >= 0 else -p - 1 for p in P]
    if min(idx) < 0 or max(idx) >= nv:
        errs.append(f"vertex index out of range (max {max(idx)}, nv {nv})")
    if any(not math.isfinite(x) for x in V):
        errs.append("non-finite vertex")
    nl = len(P)
    # 다각형 크기
    sizes, s = [], 0
    for p in P:
        s += 1
        if p < 0:
            sizes.append(s)
            s = 0
    if min(sizes) < 3:
        errs.append("polygon with <3 corners")
    used = set(idx)
    if len(used) != nv:
        errs.append(f"unused vertices {nv - len(used)}")
    E = fbxio.find(g, b"Edges")
    if E is not None:
        Ev = list(E.props[0])
        if Ev and (min(Ev) < 0 or max(Ev) >= nl):
            errs.append("edge index out of range")
        # 변 수 = 서로 다른 (a,b) 수
        keys, st = set(), 0
        for i, p in enumerate(P):
            if p < 0:
                face = idx[st:i + 1]
                for j in range(len(face)):
                    a, b = face[j], face[(j + 1) % len(face)]
                    keys.add((min(a, b), max(a, b)))
                st = i + 1
        if len(Ev) != len(keys):
            errs.append(f"edges {len(Ev)} != unique {len(keys)}")
    ln = fbxio.find(g, b"LayerElementNormal")
    N = list(fbxio.find(ln, b"Normals").props[0])
    ni = fbxio.find(ln, b"NormalsIndex")
    ref = bytes(fbxio.find(ln, b"ReferenceInformationType").props[0])
    mp = bytes(fbxio.find(ln, b"MappingInformationType").props[0])
    if mp != b"ByPolygonVertex":
        errs.append(f"normal mapping {mp}")
    if ref == b"Direct":
        if len(N) != nl * 3:
            errs.append(f"normals direct {len(N) // 3} != loops {nl}")
        if ni is not None:
            errs.append("Direct but NormalsIndex present")
    else:
        NI = list(ni.props[0])
        if len(NI) != nl:
            errs.append("NormalsIndex length")
        if max(NI) >= len(N) // 3:
            errs.append("NormalsIndex out of range")
    for i in range(0, len(N), 3):
        l = math.sqrt(N[i] ** 2 + N[i + 1] ** 2 + N[i + 2] ** 2)
        if not 0.98 < l < 1.02:
            errs.append(f"normal not unit ({l:.3f})")
            break
    lu = fbxio.find(g, b"LayerElementUV")
    U = list(fbxio.find(lu, b"UV").props[0])
    UI = list(fbxio.find(lu, b"UVIndex").props[0])
    if len(UI) != nl or max(UI) >= len(U) // 2 or min(UI) < 0:
        errs.append("UVIndex bad")
    if min(U) < -1e-6 or max(U) > 1 + 1e-6:
        errs.append(f"UV outside 0..1 ({min(U):.4f}..{max(U):.4f})")
    sm = fbxio.find(g, b"LayerElementSmoothing")
    if sm is not None:
        Sm = list(fbxio.find(sm, b"Smoothing").props[0])
        smap = bytes(fbxio.find(sm, b"MappingInformationType").props[0])
        if smap == b"ByPolygon" and len(Sm) != npoly:
            errs.append("Smoothing length != polygons")
    lm = fbxio.find(g, b"LayerElementMaterial")
    if lm is not None and bytes(fbxio.find(lm, b"MappingInformationType").props[0]) != b"AllSame":
        errs.append("material mapping not AllSame")
    print("@@VALID", os.path.basename(os.path.dirname(cp)), os.path.basename(cp),
          "OK" if not errs else "FAIL", f"verts {nv} polys {npoly} loops {nl}", errs[:5])
