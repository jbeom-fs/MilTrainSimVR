# '창고 →' 안내판 그림 (사용자 11/17 — 백화점 1층 남문 오른쪽 벽 기둥, 창고(N06) 보급 쪽을 가리킴)
#   남색 판 · 황동 테두리(백화점 난간 · 손잡이 황동에 맞춤)에 흰 '창고'와 흰 화살표. 판 0.70 × 0.26 m (가로 : 세로 = 2.7)
#   맑은 고딕 굵게로 그려 그림으로 굽는다 — 글꼴 파일은 프로젝트에 넣지 않는다 (make_cold_storage_sign.py 와 같은 방식)
# 실행: blender -b --factory-startup --python make_warehouse_arrow_sign.py -- <출력.png>
import bpy, bmesh, sys

out = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "FLAT"
sc.display.shading.color_type = "MATERIAL"
sc.display.render_aa = "16"
sc.view_settings.view_transform = "Standard"
sc.render.resolution_x, sc.render.resolution_y = 1350, 500
sc.render.image_settings.file_format = "PNG"
sc.render.image_settings.color_mode = "RGB"
sc.world = bpy.data.worlds.new("W")
sc.world.color = (1, 1, 1)

def lin(h):   # sRGB 16진 → 선형
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0]

def mat(name, hexcol):
    m = bpy.data.materials.new(name)
    m.diffuse_color = lin(hexcol)
    return m

def poly(name, pts, z, m):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, z)) for x, y in pts]
    bm.faces.new(vs)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    o.data.materials.append(m)
    sc.collection.objects.link(o)

def rect(name, x0, y0, x1, y1, z, m):
    poly(name, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)], z, m)

W, H = 2.7, 1.0
font = bpy.data.fonts.load("C:/Windows/Fonts/malgunbd.ttf")
white = mat("White", "FFFFFF")

rect("Border", -W / 2, -H / 2, W / 2, H / 2, 0.0, mat("Brass", "B08D57"))
rect("Plate", -W / 2 + 0.045, -H / 2 + 0.045, W / 2 - 0.045, H / 2 - 0.045, 0.005, mat("Navy", "1F3A5F"))

cu = bpy.data.curves.new("T", type="FONT")
cu.body = "창고"
cu.font = font
cu.size = 0.6
cu.align_x = "CENTER"
cu.align_y = "CENTER"
t = bpy.data.objects.new("T_창고", cu)
t.location = (-0.61, -0.015, 0.02)
t.data.materials.append(white)
sc.collection.objects.link(t)

# 화살표 (→): 몸통 + 머리 — 글꼴의 '→' 는 가늘어 멀리서 안 보여 도형으로 그린다
shaft_y = 0.085
ax = -0.05   # 글자 · 화살표 묶음을 판 가운데로
poly("Arrow", [(ax + 0.14, -shaft_y), (ax + 0.74, -shaft_y), (ax + 0.74, -0.27), (ax + 1.13, 0.0), (ax + 0.74, 0.27), (ax + 0.74, shaft_y), (ax + 0.14, shaft_y)], 0.02, white)

cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = W
cam.location = (0, 0, 5)
sc.collection.objects.link(cam)
sc.camera = cam
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
print("saved", out)
