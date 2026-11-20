# 냉동창고 안내판 그림 (사용자 11/09 — 도야지천국 N23 앞 오른쪽 방을 냉동창고로 막고 철문에 붙임)
#   파란 머리띠에 흰 '냉동창고', 아래 흰 바탕에 빨간 '관계자 외 출입금지'. 판 0.42 × 0.30 m (가로 : 세로 = 1.4)
#   맑은 고딕 굵게로 그려 그림으로 굽는다 — 글꼴 파일은 프로젝트에 넣지 않는다
# 실행: blender -b --factory-startup --python make_cold_storage_sign.py -- <출력.png>
import bpy, sys

out = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "FLAT"
sc.display.shading.color_type = "MATERIAL"
sc.display.render_aa = "16"
sc.view_settings.view_transform = "Standard"
sc.render.resolution_x, sc.render.resolution_y = 1120, 800
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

def rect(name, x0, y0, x1, y1, z, m):
    bpy.ops.mesh.primitive_plane_add(size=1, location=((x0 + x1) / 2, (y0 + y1) / 2, z))
    o = bpy.context.object
    o.name = name
    o.scale = (x1 - x0, y1 - y0, 1)
    o.data.materials.append(m)

W, H = 1.4, 1.0
font = bpy.data.fonts.load("C:/Windows/Fonts/malgunbd.ttf")

def text(body, size, y, m, z=0.02):
    cu = bpy.data.curves.new("T", type="FONT")
    cu.body = body
    cu.font = font
    cu.size = size
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    o = bpy.data.objects.new("T_" + body, cu)
    o.location = (0, y, z)
    o.data.materials.append(m)
    sc.collection.objects.link(o)

rect("Border", -W / 2, -H / 2, W / 2, H / 2, 0.0, mat("Border", "3A3D42"))
rect("Paper", -W / 2 + 0.025, -H / 2 + 0.025, W / 2 - 0.025, H / 2 - 0.025, 0.005, mat("Paper", "F5F5F2"))
rect("Band", -W / 2 + 0.025, 0.02, W / 2 - 0.025, H / 2 - 0.025, 0.01, mat("Band", "1E4FA0"))
text("냉동창고", 0.34, 0.245, mat("White", "FFFFFF"))
text("관계자 외 출입금지", 0.165, -0.2, mat("Red", "C8102E"))

cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = W
cam.location = (0, 0, 5)
sc.collection.objects.link(cam)
sc.camera = cam
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
print("saved", out)
