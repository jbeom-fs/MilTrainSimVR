# 원본 FBX 를 그대로 읽어 메시 배열(Geometry)만 바꿔 다시 쓴다 — 노드 · 재질 이름, ID, 연결, 축 · 단위 설정은 원본과 같다.
# Blender 에 딸린 io_scene_fbx 의 parse_fbx(읽기) · encode_bin(쓰기)을 쓴다.
import array
import addon_utils

try:
    from io_scene_fbx import parse_fbx, encode_bin, data_types
except ImportError:
    addon_utils.enable("io_scene_fbx")
    from io_scene_fbx import parse_fbx, encode_bin, data_types

T = data_types


def load(path):
    root, version = parse_fbx.parse(path)
    return root, version


def _add(dst, value, t):
    if t == T.BOOL:
        dst.add_bool(bool(value))
    elif t == T.CHAR:
        dst.add_char(value)
    elif t == T.INT8:
        dst.add_int8(int(value))
    elif t == T.INT16:
        dst.add_int16(int(value))
    elif t == T.INT32:
        dst.add_int32(int(value))
    elif t == T.INT64:
        dst.add_int64(int(value))
    elif t == T.FLOAT32:
        dst.add_float32(float(value))
    elif t == T.FLOAT64:
        dst.add_float64(float(value))
    elif t == T.BYTES:
        dst.add_bytes(bytes(value))
    elif t == T.STRING:
        dst.add_string(bytes(value))
    elif t == T.INT32_ARRAY:
        dst.add_int32_array(array.array(T.ARRAY_INT32, value))
    elif t == T.INT64_ARRAY:
        dst.add_int64_array(array.array(T.ARRAY_INT64, value))
    elif t == T.FLOAT32_ARRAY:
        dst.add_float32_array(array.array(T.ARRAY_FLOAT32, value))
    elif t == T.FLOAT64_ARRAY:
        dst.add_float64_array(array.array(T.ARRAY_FLOAT64, value))
    elif t == T.BOOL_ARRAY:
        dst.add_bool_array(array.array(T.ARRAY_BOOL, value))
    elif t == T.BYTE_ARRAY:
        dst.add_byte_array(array.array(T.ARRAY_BYTE, value))
    else:
        raise ValueError(f"unknown FBX prop type {chr(t)}")


def to_enc(e, override=None):
    """parse_fbx 원소 → encode_bin 원소. override(e) 가 (props, types) 를 돌려주면 그 값으로 바꿔 쓴다."""
    dst = encode_bin.FBXElem(e.id)
    props, types = e.props, e.props_type
    if override is not None:
        r = override(e)
        if r is not None:
            props, types = r
    for v, t in zip(props, types):
        _add(dst, v, t)
    for c in e.elems:
        if override is not None and getattr(override, "drop", None) and override.drop(c):
            continue
        dst.elems.append(to_enc(c, override))
    return dst


def save(root, path, version, override=None):
    enc_root = encode_bin.FBXElem(b"")
    for e in root.elems:
        enc_root.elems.append(to_enc(e, override))
    encode_bin.write(path, enc_root, version)


def find(e, *ids):
    """경로를 따라 첫 원소를 찾는다: find(root, b'Objects', b'Geometry')"""
    cur = e
    for i in ids:
        nxt = None
        for c in cur.elems:
            if c.id == i:
                nxt = c
                break
        if nxt is None:
            return None
        cur = nxt
    return cur


def fbx_edges(poly):
    """FBX Edges: 변마다 그 변이 시작하는 모서리(PolygonVertexIndex 번호) 하나 — Blender 내보내기와 같은 방식"""
    seen = set()
    out = []
    start = 0
    for i, raw in enumerate(poly):
        if raw < 0:
            face = list(range(start, i + 1))
            vs = [poly[k] if poly[k] >= 0 else -poly[k] - 1 for k in face]
            for j, k in enumerate(face):
                a, b = vs[j], vs[(j + 1) % len(vs)]
                key = (a, b) if a < b else (b, a)
                if key not in seen:
                    seen.add(key)
                    out.append(k)
            start = i + 1
    return out


class GeometryPatch:
    """Objects/Geometry 의 배열을 새 메시로 바꾼다 — 원본이 쓰던 형식(법선 Direct/IndexToDirect, Edges, Smoothing)을 그대로 따른다.
    verts: [x,y,z,...] (파일 좌표) · poly: PolygonVertexIndex (끝 꼭짓점은 ~i) · normals: 모서리마다 [x,y,z,...]
    uv: [u,v,...] · uv_index: 모서리마다 uv 번호. 나머지(이름 · Layer · Material 배치)는 원본 그대로."""

    def __init__(self, verts, poly, normals, uv, uv_index, root=None):
        self.verts, self.poly, self.normals, self.uv, self.uv_index = verts, poly, normals, uv, uv_index
        self.n_poly = sum(1 for p in poly if p < 0)
        self.edges = fbx_edges(poly)
        # 법선을 원본처럼 IndexToDirect 로 쓸지
        self.normals_indexed = False
        if root is not None:
            g = find(root, b"Objects", b"Geometry")
            ln = find(g, b"LayerElementNormal") if g is not None else None
            self.normals_indexed = ln is not None and find(ln, b"NormalsIndex") is not None
        if self.normals_indexed:
            uniq = {}
            self.n_index = []
            self.n_unique = []
            for i in range(0, len(normals), 3):
                key = (round(normals[i], 7), round(normals[i + 1], 7), round(normals[i + 2], 7))
                if key not in uniq:
                    uniq[key] = len(uniq)
                    self.n_unique.extend(normals[i:i + 3])
                self.n_index.append(uniq[key])

    def __call__(self, e):
        D, I = T.FLOAT64_ARRAY, T.INT32_ARRAY
        if e.id == b"Vertices":
            return [self.verts], [D]
        if e.id == b"PolygonVertexIndex":
            return [self.poly], [I]
        if e.id == b"Normals":
            return [self.n_unique if self.normals_indexed else self.normals], [D]
        if e.id == b"NormalsIndex":
            return [self.n_index], [I]
        if e.id == b"NormalsW":
            n = len(self.n_unique if self.normals_indexed else self.normals) // 3
            return [[1.0] * n], [D]
        if e.id == b"UV":
            return [self.uv], [D]
        if e.id == b"UVIndex":
            return [self.uv_index], [I]
        if e.id == b"Edges":
            return [self.edges], [I]
        if e.id == b"Smoothing":
            # 면마다 부드러움 1 — 법선은 파일에 따로 다 적혀 있고 Unity 는 그 법선을 쓴다(normalImportMode Import)
            return [[1] * self.n_poly], [I]
        return None

    @staticmethod
    def drop(e):
        # 새 메시와 길이를 맞출 수 없는 원소 — 이 Tripo 파일들엔 없다 (있으면 빼 버린다)
        return e.id in (b"LayerElementBinormal", b"LayerElementTangent")
