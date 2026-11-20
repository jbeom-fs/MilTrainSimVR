# make_prop_lowpoly 를 차량용 UV(uv_charts.charts_only)로 바꿔 끼워 돌린다 — 저장소 스크립트는 그대로 둔다
# blender -b --factory-startup --python run_vehicle_lowpoly.py -- spec.json <Source~ 폴더> <이 폴더>
import sys, runpy
a = sys.argv[sys.argv.index("--") + 1:]
src, here = a[1], a[2]
sys.path.insert(0, src)
sys.path.insert(0, here)
import uvtools, uv_charts


def _unfold(ob, smart_angle_deg=66.0, margin=0.003, log=None):
    uv_charts.charts_only(ob, smart_angle_deg, margin, log)


uvtools.unfold_seams = _unfold
runpy.run_path(src + "/make_prop_lowpoly.py", run_name="__main__")
