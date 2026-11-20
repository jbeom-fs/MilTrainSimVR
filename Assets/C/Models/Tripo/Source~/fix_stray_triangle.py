# 삼각형 하나가 꼭짓점을 잘못 잡아 생긴 구멍을 고친다 (11/18 콘크리트 블록 — 좌하단의 가는 틈으로 안이 보였다).
# 증상: 세 변짜리 구멍(삼각형 하나가 비었다) 옆에, 변 둘을 이웃과 겹쳐 쓰는(삼각형 셋이 한 변을 쓴다) 삼각형이 하나 있다.
#       그 삼각형이 구멍의 두 꼭짓점 대신 바로 옆 꼭짓점 둘을 잡은 것 — 그래서 이웃과 겹치고 그 옆이 비었다.
# 고침: 그 삼각형의 꼭짓점 둘을 구멍의 꼭짓점으로 바꾼다. UV · 법선은 구멍 변을 사이에 둔 이웃 삼각형의 같은 꼭짓점 것을 쓴다(같은 UV 섬).
#       메시 배열만 바꿔 쓰니 노드 · 재질 이름, ID 가 그대로라 씬에 놓인 것들이 그대로 따라온다 (fbxio).
# blender -b --factory-startup --python fix_stray_triangle.py -- <원본.fbx> <고친.fbx>
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbxio  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
T = fbxio.T

root, version = fbxio.load(src)
g = fbxio.find(root, b"Objects", b"Geometry")
verts = list(fbxio.find(g, b"Vertices").props[0])
poly = list(fbxio.find(g, b"PolygonVertexIndex").props[0])
ln = fbxio.find(g, b"LayerElementNormal")
n_map = fbxio.find(ln, b"MappingInformationType").props[0]
n_ref = fbxio.find(ln, b"ReferenceInformationType").props[0]
normals = list(fbxio.find(ln, b"Normals").props[0])
n_index_el = fbxio.find(ln, b"NormalsIndex")
n_index = list(n_index_el.props[0]) if n_index_el is not None else None
luv = fbxio.find(g, b"LayerElementUV")
uv_map = fbxio.find(luv, b"MappingInformationType").props[0]
uv_index = list(fbxio.find(luv, b"UVIndex").props[0])
print("@@FIX", os.path.basename(src), "verts", len(verts) // 3, "corners", len(poly), "normals", n_map, n_ref, "uv", uv_map)
if n_map != b"ByPolygonVertex" or uv_map != b"ByPolygonVertex":
    raise SystemExit("@@FIX unsupported layer mapping")

# 같은 자리 꼭짓점은 하나로 (0.01 mm)
pos = [tuple(verts[i:i + 3]) for i in range(0, len(verts), 3)]
weld = {}
wid = []
for p in pos:
    k = (round(p[0], 5), round(p[1], 5), round(p[2], 5))
    wid.append(weld.setdefault(k, len(weld)))

# 면 = 모서리(PolygonVertexIndex 번호) 목록
faces = []
start = 0
for i, raw in enumerate(poly):
    if raw < 0:
        faces.append(list(range(start, i + 1)))
        start = i + 1


def vert(k):
    return poly[k] if poly[k] >= 0 else -poly[k] - 1


def wv(k):
    return wid[vert(k)]


def edge_stats():
    count = {}
    for f in faces:
        for j, k in enumerate(f):
            a, b = wv(k), wv(f[(j + 1) % len(f)])
            key = (a, b) if a < b else (b, a)
            count[key] = count.get(key, 0) + 1
    return count


def ek(a, b):
    return (a, b) if a < b else (b, a)


count = edge_stats()
n_open = sum(1 for c in count.values() if c == 1)
n_multi = sum(1 for c in count.values() if c > 2)
print("@@FIX before: faces", len(faces), "open edges", n_open, "edges shared by 3+", n_multi)

# 잘못된 삼각형: 변 하나는 열려 있고(1) 나머지 둘은 셋이 쓴다(3)
bad = [f for f in faces if len(f) == 3 and sorted(count[ek(wv(f[j]), wv(f[(j + 1) % 3]))] for j in range(3)) == [1, 3, 3]]
if len(bad) != 1:
    raise SystemExit(f"@@FIX expected exactly one stray triangle, found {len(bad)}")
f = bad[0]
ws = [wv(k) for k in f]
# c = 겹쳐 쓰는 두 변이 만나는 꼭짓점 (그대로 둔다), a · b = 열린 변의 두 끝 (바꾼다)
j_open = [j for j in range(3) if count[ek(ws[j], ws[(j + 1) % 3])] == 1][0]
ja, jb, jc = j_open, (j_open + 1) % 3, (j_open + 2) % 3
a, b, c = ws[ja], ws[jb], ws[jc]

# 구멍: c 에서 나가는 열린 변 둘의 다른 끝 (잘못된 삼각형 자신의 열린 변 a–b 는 뺀다)
hole = [w for (p, q), n in count.items() if n == 1 and c in (p, q) for w in (p, q) if w != c]
if len(hole) != 2 or count.get(ek(hole[0], hole[1])) != 1:
    raise SystemExit(f"@@FIX the hole next to the stray triangle is not a single triangle: {hole}")
# a 자리에 올 것 = 구멍 꼭짓점 중 a 와 이미 변으로 이어진 것
a2 = [h for h in hole if ek(h, a) in count]
b2 = [h for h in hole if ek(h, b) in count]
if len(a2) != 1 or len(b2) != 1 or a2[0] == b2[0]:
    raise SystemExit(f"@@FIX cannot tell which hole corner replaces which: {a2} {b2}")
a2, b2 = a2[0], b2[0]


# 구멍 변 (x–c) 을 쓰는 이웃 삼각형에서 x 꼭짓점 모서리 — 거기의 꼭짓점 번호 · UV · 법선을 가져온다
def donor(x):
    for face in faces:
        if face is f:
            continue
        w = [wv(k) for k in face]
        if x in w and c in w:
            kx, kc = face[w.index(x)], face[w.index(c)]
            return kx, kc
    raise SystemExit("@@FIX no neighbour across the hole edge")


for j, x in ((ja, a2), (jb, b2)):
    kx, kc = donor(x)
    if uv_index[kc] != uv_index[f[jc]]:
        print("@@FIX note: neighbour is on another UV island at the shared corner", uv_index[kc], uv_index[f[jc]])
    k = f[j]
    last = poly[k] < 0
    v = vert(kx)
    poly[k] = -(v + 1) if last else v
    uv_index[k] = uv_index[kx]
    if n_index is not None:
        n_index[k] = n_index[kx]
    else:
        normals[k * 3:k * 3 + 3] = normals[kx * 3:kx * 3 + 3]

count = edge_stats()
n_open2 = sum(1 for c_ in count.values() if c_ == 1)
n_multi2 = sum(1 for c_ in count.values() if c_ > 2)
print("@@FIX after: open edges", n_open2, "edges shared by 3+", n_multi2)
if n_open2 >= n_open or n_multi2 >= n_multi:
    raise SystemExit("@@FIX the patch did not improve the mesh — nothing written")


def override(e):
    if e.id == b"PolygonVertexIndex":
        return [poly], [T.INT32_ARRAY]
    if e.id == b"UVIndex":
        return [uv_index], [T.INT32_ARRAY]
    if e.id == b"NormalsIndex" and n_index is not None:
        return [n_index], [T.INT32_ARRAY]
    if e.id == b"Normals" and n_index is None:
        return [normals], [T.FLOAT64_ARRAY]
    if e.id == b"Edges":
        return [fbxio.fbx_edges(poly)], [T.INT32_ARRAY]
    return None


override.drop = lambda e: False
fbxio.save(root, dst, version, override)
print("@@FIX wrote", dst, os.path.getsize(dst), "bytes (source", os.path.getsize(src), ")")
