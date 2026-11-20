# inspect_extract.py 가 남긴 inspect.json 을 읽어 run_vehicle_lowpoly.py 용 spec 을 만든다 (10만 삼각형 · 4K 굽기)
# python make_vehicle_spec.py <차량 폴더(Source~/vehicles/van)> <이름(Van100k)> <결과 폴더> <spec 파일>
import sys, os, json

folder, name, out_dir, spec_path = sys.argv[1:5]
rep = json.load(open(os.path.join(folder, "inspect.json"), encoding="utf-8"))
saved = {i["name"]: i.get("saved") for i in rep["images"] if i.get("saved")}
maps = {}
want = {"basecolor": ("base color", "basecolor", "albedo", "diffuse"), "normal": ("normal",),
        "metallic": ("metallic", "metal"), "roughness": ("roughness", "rough")}
for m in rep["materials"]:
    for l in m["links"]:
        to = l["to"].lower()
        for kind, keys in want.items():
            if kind not in maps and any(k in to for k in keys) and saved.get(l["image"]):
                maps[kind] = saved[l["image"]]
for kind, keys in want.items():          # 재질 연결로 못 찾으면 파일 이름으로
    if kind not in maps:
        for nm, path in saved.items():
            if any(k.replace(" ", "") in nm.lower().replace("_", "") for k in keys):
                maps[kind] = path
                break
meshes = [o for o in rep["objects"] if o["type"] == "MESH"]
orig = [f for f in os.listdir(folder) if f.endswith("_orig.fbx")]
spec = {"src_fbx": os.path.join(folder, orig[0]).replace("\\", "/"),
        "maps": {k: v.replace("\\", "/") for k, v in maps.items()},
        "out_dir": out_dir, "name": name,
        "target_tris": 100000, "planar_deg": 1.0, "hide_remove": False, "tex_res": 4096,
        "cage": 0.002, "ray": 0.012, "bake_margin": 48, "bake_samples": 16}
json.dump(spec, open(spec_path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print("SPEC", json.dumps({"maps": sorted(maps), "meshes": [[o["name"], o["tris"], o["world_min"], o["world_max"]] for o in meshes],
                          "images": [[i["name"], i["size"]] for i in rep["images"]]}))
if len(maps) < 4 or len(meshes) != 1:
    print("SPEC_WARNING: expected 4 maps and 1 mesh")
    sys.exit(3)
