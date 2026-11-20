# make_lowpoly 로그 요약: python summ.py log...
import json, sys

for path in sys.argv[1:]:
    line = [l for l in open(path, encoding="utf-8", errors="replace") if l.startswith("@@LOG")]
    errs = [l.strip() for l in open(path, encoding="utf-8", errors="replace") if "Traceback" in l or "Error" in l and "HIPEW" not in l]
    name = path.replace("\\", "/").split("/")[-1]
    if not line:
        print(f"{name}: 실패", errs[:6])
        continue
    d = json.loads(line[0][5:])
    o, l, e, r = d["orig"], d["low"], d["error"], d["reimport"]
    k = e.get("mm_per_unit") or 1000.0
    unit = "mm" if e.get("mm_per_unit") else "mu"
    se = d.get("shape_error_before_hidden_removal") or {"orig_to_low_mean": e["orig_to_low_surface_mean"], "orig_to_low_p99": e["orig_to_low_surface_p99"], "orig_to_low_max": e["orig_to_low_surface_max"]}
    bake = d.get("bake") or {}
    print(f"{name}: verts {o['unity_verts']:,} -> {r['unity_verts']:,} ({(r['unity_verts']/o['unity_verts']-1)*100:+.0f}%) | "
          f"tris {o['tris']:,} -> {r['tris']:,} | pos {l['positions']} | uv_islands {d.get('uv_islands')} | "
          f"shape err mean {se['orig_to_low_mean']*k:.1f}{unit} p99 {se['orig_to_low_p99']*k:.1f}{unit} max {se['orig_to_low_max']*k:.1f}{unit} | "
          f"low->orig p99 {e['low_to_orig_surface_p99']*k:.1f}{unit} | bounds diff {r['bounds_maxdiff']:.1e} | name same {r['name_same']} | mat {r['materials']} | "
          f"hidden {d.get('hidden_removed')} | unfold {d.get('unfold')} | dark repaired {bake.get('dark_faces_repaired', bake.get('dark_faces'))} | "
          f"color diff mean {bake.get('color_diff_mean', 0):.3f} p95 {bake.get('color_diff_p95', 0):.3f}")
