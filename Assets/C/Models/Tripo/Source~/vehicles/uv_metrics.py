# UV 검사 — 겹친 텍셀 · 찌그러진 면 · 채움 비율 · Unity 꼭짓점 어림
import numpy as np


def _raster(me, R):
    n = len(me.polygons)
    uv = np.empty(len(me.loops) * 2, np.float32)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2).astype(np.float64)
    ls = np.empty(n, np.int32)
    me.polygons.foreach_get("loop_start", ls)
    t = uv[ls[:, None] + np.arange(3)[None, :]] * R
    cnt = np.zeros((R, R), np.uint8)
    EPS = 0.04
    masks = [None] * n
    for i in range(n):
        p, q, r = t[i]
        x0 = max(int(np.floor(min(p[0], q[0], r[0]))), 0); x1 = min(int(np.ceil(max(p[0], q[0], r[0]))), R - 1)
        y0 = max(int(np.floor(min(p[1], q[1], r[1]))), 0); y1 = min(int(np.ceil(max(p[1], q[1], r[1]))), R - 1)
        if x1 < x0 or y1 < y0:
            continue
        d = (q[1] - r[1]) * (p[0] - r[0]) + (r[0] - q[0]) * (p[1] - r[1])
        if abs(d) < 1e-9:
            continue
        X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        w0 = ((q[1] - r[1]) * (X - r[0]) + (r[0] - q[0]) * (Y - r[1])) / d
        w1 = ((r[1] - p[1]) * (X - r[0]) + (p[0] - r[0]) * (Y - r[1])) / d
        m = (w0 > EPS) & (w1 > EPS) & (1 - w0 - w1 > EPS)
        if m.any():
            yy, xx = np.nonzero(m)
            yy += y0; xx += x0
            cnt[yy, xx] += 1
            masks[i] = (yy, xx)
    return uv, ls, t, cnt, masks


def overlapped_faces(me, R=4096, min_texels=1):
    """다른 면과 같은 텍셀을 나눠 쓰는 면의 번호."""
    uv, ls, t, cnt, masks = _raster(me, R)
    over = cnt >= 2
    return [i for i in range(len(masks)) if masks[i] is not None and int(over[masks[i]].sum()) >= min_texels]


def measure(me, R=4096, isl=None):
    n = len(me.polygons)
    uv, ls, t, cnt, masks = _raster(me, R)
    area3 = np.empty(n, np.float32)
    me.polygons.foreach_get("area", area3)
    a, b, c = t[:, 0], t[:, 1], t[:, 2]
    area_uv = 0.5 * np.abs((b[:, 0] - a[:, 0]) * (c[:, 1] - a[:, 1]) - (c[:, 0] - a[:, 0]) * (b[:, 1] - a[:, 1]))
    ok = area3 > 1e-10
    dens = np.zeros(n)
    dens[ok] = area_uv[ok] / area3[ok]
    med = float(np.median(dens[ok]))
    over = cnt >= 2
    ov = [i for i in range(n) if masks[i] is not None and int(over[masks[i]].sum()) >= 1]
    out = {"median_px_per_m_real": round(float(np.sqrt(med)) / 4.2, 1),
           "fill_pct": round(100.0 * float((cnt >= 1).sum()) / (R * R), 1),
           "overlapped_texels": int(over.sum()), "faces_with_overlap": len(ov),
           "collapsed_faces_lt2pct": int((ok & (dens < med * 0.02)).sum()),
           "low_density_lt50pct_area_pct": round(100.0 * float(area3[ok & (dens < med * 0.5)].sum() / area3.sum()), 3)}
    if isl is not None and ov:
        # 겹침이 한 섬 안에서 생기는지, 섬과 섬 사이에서 생기는지
        owner = np.full((R, R), -1, np.int32)
        same = diff = 0
        for i in ov:
            yy, xx = masks[i]
            sel = over[yy, xx]
            o = owner[yy[sel], xx[sel]]
            same += int((o == isl[i]).sum())
            diff += int(((o >= 0) & (o != isl[i])).sum())
            owner[yy[sel], xx[sel]] = isl[i]
        out["overlap_same_island_vs_between"] = [same, diff]
    vi = np.empty(len(me.loops), np.int32)
    me.loops.foreach_get("vertex_index", vi)
    cn = np.empty(len(me.loops) * 3, np.float32)
    me.corner_normals.foreach_get("vector", cn)
    key = np.concatenate([vi[:, None].astype(np.float64), np.round(uv, 5), np.round(cn.reshape(-1, 3).astype(np.float64), 3)], axis=1)
    out["unity_verts_estimate"] = int(len(np.unique(key, axis=0)))
    return out
