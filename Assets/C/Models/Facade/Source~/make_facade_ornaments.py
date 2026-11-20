# Usage: blender -b --factory-startup --python make_facade_ornaments.py -- <facade_dir> <font_ttf>
#   <facade_dir> = Assets/C/Models/Facade — 그 아래 Sunburst/C_Sunburst.fbx · StoreName/C_StoreName.fbx 를 쓴다
# 청운백화점(N05) 4층 장식판의 금속 장식 (사용자 11/06 — 목업 C 안)
#   Sunburst: 아르데코 해돋이 — 지평선 막대 + 반쯤 떠오른 해(반원) + 부채꼴 빛살 11줄(길고 짧게 번갈아). 너비 약 4.3 m · 높이 2.3 m
#   StoreName: '청운백화점' 도드라진 금속 글자 (고운바탕 Bold), 글자 높이 약 0.9 m
#   둘 다 앞면이 Blender -Y 쪽, 뒷면이 y = 0 (판에 붙는 면), 두께 5 cm. 재질 이름 FacadeGold — Unity 에서 GB_Facade_Gold 로 바꿔 끼운다
#   빌더(UrbanGreyboxBuilder.Facade.cs)가 장식판 자리에 비율을 지켜 키워 놓는다
import bpy, bmesh, sys, os, math
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
OUT = argv[0]
FONT = argv[1]
DEPTH = 0.05

for ob in list(bpy.data.objects):
    bpy.data.objects.remove(ob, do_unlink=True)
gold = bpy.data.materials.new("FacadeGold")
gold.use_nodes = True
bsdf = gold.node_tree.nodes["Principled BSDF"]
bsdf.inputs["Base Color"].default_value = (0.56, 0.34, 0.10, 1.0)
bsdf.inputs["Metallic"].default_value = 1.0
bsdf.inputs["Roughness"].default_value = 0.3


def add_box(bm, x0, x1, z0, z1, rot_deg=0.0, center=None):
    c = Vector(((x0 + x1) / 2, -DEPTH / 2, (z0 + z1) / 2))
    m = Matrix.Translation(c) @ Matrix.Diagonal((abs(x1 - x0), DEPTH, abs(z1 - z0), 1.0))
    verts = bmesh.ops.create_cube(bm, size=1.0, matrix=m)["verts"]
    if rot_deg:
        bmesh.ops.rotate(bm, verts=verts, cent=c, matrix=Matrix.Rotation(math.radians(-rot_deg), 3, 'Y'))
    return verts


def half_disc(bm, cx, cz, r, segments=24):
    # 지평선(cz) 위 반원판 — 앞(y = -DEPTH) · 뒤(y = 0) 면과 둘레
    pts = [(cx + r * math.cos(math.pi * i / segments), cz + r * math.sin(math.pi * i / segments)) for i in range(segments + 1)]
    front = [bm.verts.new((x, -DEPTH, z)) for x, z in pts]
    back = [bm.verts.new((x, 0.0, z)) for x, z in pts]
    bm.faces.new(front)
    bm.faces.new(list(reversed(back)))
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], back[i], back[j], front[j]))


def mesh_object(name, bm):
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    me.materials.append(gold)
    return ob


def export(ob, folder, filename):
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    path = os.path.join(folder, filename)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, mesh_smooth_type="FACE",
                             add_leaf_bones=False, bake_anim=False, path_mode="STRIP", apply_scale_options="FBX_SCALE_ALL")
    dims = ob.dimensions
    print("WROTE", path, "크기 %.2f x %.2f x %.2f m" % (dims.x, dims.z, dims.y), flush=True)


# ---------------------------------------------------------------- 해돋이
bm = bmesh.new()
BAR_TOP = 0.08
add_box(bm, -1.85, 1.85, 0.0, BAR_TOP)
half_disc(bm, 0.0, BAR_TOP, 0.5)
for k in range(11):
    theta = math.radians(15 + k * 15)
    length = 1.6 if k % 2 == 0 else 1.1
    r_mid = 0.62 + length / 2
    cx, cz = r_mid * math.cos(theta), BAR_TOP + r_mid * math.sin(theta)
    # 가로 막대를 빛살 방향으로 기울인다 (Blender Y 축 회전: 양의 각이 x → -z 쪽이라 부호를 뒤집어 넘긴다)
    add_box(bm, cx - length / 2, cx + length / 2, cz - 0.035, cz + 0.035, rot_deg=math.degrees(theta), center=(cx, cz))
sun = mesh_object("C_Sunburst", bm)
export(sun, os.path.join(OUT, "Sunburst"), "C_Sunburst.fbx")

# ---------------------------------------------------------------- 백화점 이름
cu = bpy.data.curves.new("C_StoreName", 'FONT')
cu.body = "청운백화점"
cu.font = bpy.data.fonts.load(FONT)
cu.size = 1.0
cu.extrude = DEPTH / 2
cu.bevel_depth = 0.004
cu.resolution_u = 3          # 글자 곡선 나누기 — 기본 12 면 FBX 가 3.5 MB 라 VR 에 무겁다
cu.bevel_resolution = 1
cu.align_x = 'CENTER'
cu.align_y = 'CENTER'
txt = bpy.data.objects.new("C_StoreName", cu)
bpy.context.collection.objects.link(txt)
txt.rotation_euler = (math.radians(90), 0.0, 0.0)     # 글자 면이 -Y 를 본다
txt.location = (0.0, -DEPTH / 2, 0.0)                 # 뒷면이 y = 0
bpy.ops.object.select_all(action='DESELECT')
txt.select_set(True)
bpy.context.view_layer.objects.active = txt
bpy.ops.object.convert(target='MESH')
txt = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
txt.data.materials.clear()
txt.data.materials.append(gold)
export(txt, os.path.join(OUT, "StoreName"), "C_StoreName.fbx")
