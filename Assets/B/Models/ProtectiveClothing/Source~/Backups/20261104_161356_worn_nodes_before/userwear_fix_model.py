"""Jacket deformation fix after the user-wear change (2026-11-03 review): less slack under the upper sleeve near the armpit (the
cause of the sleeve entering the torso with the arms down) and a wider weight blend at the elbow. Only Jacket_Worn's vertex
positions under the upper sleeves and its vertex weights change; object names, pivots, Grip / Attach / root transforms, the
skeleton and every other mesh stay as they are. Tried and rejected (worse numbers, see UserWearFixExperiments.json): wider
shoulder / armpit blends, attached islands skinned from the nearest shell vertex, no or more global smoothing.

Input: Backups/20261103_userwear_fix_before (the user-wear result before this fix), so the script can be re-run.
Run: blender -b --factory-startup --python userwear_fix_model.py -- [metrics|final]
  metrics: before/after numbers only (nothing saved); final: also renders, saves the .blend, exports the worn FBX, re-imports.
"""
import bpy, bmesh, math, json, sys, time
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

T0 = time.time()
MODE = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else 'metrics'
S = Path(__file__).resolve().parent
P = S.parent
BACKUP = S / 'Backups' / '20261103_userwear_fix_before' / 'Model' / 'Source~' / 'ProtectiveClothing.blend'
PREV = S / 'Previews' / 'UserWearFix'
PFX = 'mixamorig1:'
PARTS = ['Jacket', 'Trousers', 'Glove_L', 'Glove_R', 'Overboot_L', 'Overboot_R']

# Tuning (metres): armpit slack reduction and blend widths.
UNDERARM_TARGET = 0.007      # clearance kept between the sleeve underside and the arm skin
UNDERARM_SHARE = 0.8         # share of the slack above the target that is removed at the armpit
UNDERARM_X = (0.22, 0.40)    # |x| range of the upper sleeve treated (full effect up to 0.28, fading to 0 at 0.40)
UNDERARM_FULL_X = 0.28
UNDERARM_MAX_CLEARANCE = 0.045  # only cloth within this distance of the arm skin is the sleeve around that arm
UNDERARM_MAX_STEP = 0.025
GLOBAL_PASSES = 2            # weight smoothing passes on the whole shell (as in userwear_model.py)
ELBOW_PASSES = 10

bpy.ops.wm.open_mainfile(filepath=str(BACKUP))
sc = bpy.context.scene
vl = bpy.context.view_layer
for img in bpy.data.images:
    if img.name.startswith('PC_') and img.packed_file:
        img.filepath = '//../Textures/' + Path(img.filepath).name
wc = bpy.data.collections['Worn']; pc = bpy.data.collections['Pickup']
pa = bpy.data.objects['PC_Armature']; arm = bpy.data.objects['Armature']; body = bpy.data.objects['Ch36']
jo = bpy.data.objects['Jacket_Worn']
src_basis = {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones}
contract = {o.name: ([list(r) for r in o.matrix_local], o.parent.name if o.parent else None)
            for o in bpy.data.objects if o.type in {'EMPTY', 'MESH', 'ARMATURE'} and (o.name.startswith(('Attach_', 'Grip_', 'ProtectiveClothing_', 'PC_Armature')) or o.name.endswith(('_Worn', '_Pickup')))}
mesh_names = {o.name: o.data.name for o in bpy.data.objects if o.type == 'MESH'}
other_vertices = {p: [v.co.copy() for v in bpy.data.objects[p + '_Worn'].data.vertices] for p in PARTS if p != 'Jacket'}
other_weights = {p: [sorted((g.group, round(g.weight, 6)) for g in v.groups) for v in bpy.data.objects[p + '_Worn'].data.vertices] for p in PARTS if p != 'Jacket'}
report = {'input': str(BACKUP.relative_to(S)), 'mode': MODE,
          'tuning': {'underarm_target_m': UNDERARM_TARGET, 'underarm_share': UNDERARM_SHARE, 'underarm_abs_x_m': UNDERARM_X,
                     'underarm_full_effect_to_abs_x_m': UNDERARM_FULL_X, 'underarm_max_clearance_m': UNDERARM_MAX_CLEARANCE,
                     'underarm_max_step_m': UNDERARM_MAX_STEP, 'global_passes': GLOBAL_PASSES, 'elbow_blend_passes': ELBOW_PASSES}}

# ---------- poses (same construction as userwear_model.py, plus the two hand poses) ----------


def rot_world(a, bone, axis, deg):
    pb = a.pose.bones[PFX + bone]; vl.update()
    M = a.matrix_world @ pb.matrix; h = M.translation.copy()
    pb.matrix = a.matrix_world.inverted() @ Matrix.Translation(h) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-h) @ M
    vl.update()


def aim(a, bone, d):
    pb = a.pose.bones[PFX + bone]; vl.update()
    M = a.matrix_world @ pb.matrix; h = M.translation.copy()
    q = ((a.matrix_world @ pb.tail) - h).normalized().rotation_difference(Vector(d).normalized())
    pb.matrix = a.matrix_world.inverted() @ Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ M
    vl.update()


def arms_down(a, out=.25, fwd=.05):
    aim(a, 'LeftArm', (out, -fwd, -1)); aim(a, 'RightArm', (-out, -fwd, -1))


POSES = {
    'ArmsDown': lambda a: arms_down(a),
    'ElbowBend': lambda a: (arms_down(a, .35, .2), aim(a, 'LeftForeArm', (.15, -1, .05)), aim(a, 'RightForeArm', (-.15, -1, .05))),
    'HandToChest': lambda a: (arms_down(a, .25, .55), aim(a, 'LeftForeArm', (-.75, -.55, .35)), aim(a, 'RightForeArm', (.75, -.55, .35))),
    'HandToWaist': lambda a: (arms_down(a, .3, .35), aim(a, 'LeftForeArm', (-.55, -.85, -.05)), aim(a, 'RightForeArm', (.55, -.85, -.05))),
    'TorsoTwist': lambda a: (arms_down(a), [rot_world(a, b, 'Z', 10) for b in ('Spine', 'Spine1', 'Spine2')]),
    'LightBend': lambda a: (arms_down(a, .2, .3), rot_world(a, 'Spine', 'X', 10), rot_world(a, 'Spine1', 'X', 10),
                            rot_world(a, 'LeftUpLeg', 'X', -20), rot_world(a, 'RightUpLeg', 'X', -20),
                            rot_world(a, 'LeftLeg', 'X', 35), rot_world(a, 'RightLeg', 'X', 35)),
}
POSE_TEXT = {
    'ArmsDown': 'both upper arms aimed down and slightly out from the T pose',
    'ElbowBend': 'arms down and out, forearms forward (controller holding)',
    'HandToChest': 'arms down and forward, forearms folded in so the hands are in front of the chest',
    'HandToWaist': 'arms down, forearms forward and in so the hands are in front of the waist',
    'TorsoTwist': 'arms down, Spine/Spine1/Spine2 10 deg each about the vertical (30 deg)',
    'LightBend': 'arms down and forward, spine 20 deg forward, thighs 20 deg forward, knees 35 deg'}


def clear_pose():
    for pb in pa.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    for pb in arm.pose.bones:
        pb.matrix_basis = src_basis[pb.name]
    vl.update()


def evaluated(o):
    dg = bpy.context.evaluated_depsgraph_get(); ev = o.evaluated_get(dg); me = ev.to_mesh()
    pts = [ev.matrix_world @ v.co for v in me.vertices]; faces = [tuple(f.vertices) for f in me.polygons]
    ev.to_mesh_clear()
    return pts, faces


def body_tree():
    p, f = evaluated(body)
    return BVHTree.FromPolygons(p, f)


def region(v):
    x, y, z = v
    s = 'L' if x > 0 else 'R'
    if abs(x) > .2:
        return 'sleeve' + s + ('_upper' if abs(x) < .44 else '_fore')
    if z < 1.02:
        return 'hem'
    if abs(x) > .12 and z > 1.3:
        return 'armpit_shoulder' + s
    if abs(x) > .12:
        return 'side' + s
    return 'front' if y < 0 else 'back'


BODY_PART = None


def body_part_of(face_index, faces):
    """Dominant bone group of the reference body around a face: arm / torso / leg / other."""
    global BODY_PART
    if BODY_PART is None:
        gidx = {g.index: g.name for g in body.vertex_groups}
        BODY_PART = []
        for v in body.data.vertices:
            g = max(v.groups, key=lambda x: x.weight, default=None)
            n = gidx[g.group] if g else ''
            BODY_PART.append('arm' if ('Arm' in n or 'Hand' in n) else 'leg' if ('Leg' in n or 'Foot' in n) else 'torso')
    return BODY_PART[faces[face_index][0]]


def metrics(tag):
    """Same measures as userwear_model.py (every 3rd vertex against the posed body; edge length ratio to the rest pose), plus
    per-region counts and the stretch of edges of at least 5 mm (the 1.5-2.5 mm cloth thickness edges exaggerate ratios)."""
    clear_pose()
    rest = {p: evaluated(bpy.data.objects[p + '_Worn'])[0] for p in PARTS}
    edges = {p: [tuple(e.vertices) for e in bpy.data.objects[p + '_Worn'].data.edges] for p in PARTS}
    out = {}
    for pose, apply in POSES.items():
        clear_pose(); apply(pa); apply(arm)
        bpts, bfaces = evaluated(body)
        tree = BVHTree.FromPolygons(bpts, bfaces)
        out[pose] = {}
        for p in PARTS:
            pts = evaluated(bpy.data.objects[p + '_Worn'])[0]
            sd = []
            where = {}; against = {}
            for i in range(0, len(pts), 3):
                loc, no, idx, d = tree.find_nearest(pts[i])
                s = (pts[i] - loc).dot(no); sd.append(s)
                if s < -.005 and p == 'Jacket':
                    r = region(rest[p][i]); where[r] = min(where.get(r, 0.0), s)
                    k = body_part_of(idx, bfaces); against[k] = against.get(k, 0) + 1
            ratios = []; long_ratios = []; stretch_where = {}
            for a, b in edges[p]:
                r0 = (rest[p][a] - rest[p][b]).length
                if r0 > 1e-5:
                    k = (pts[a] - pts[b]).length / r0
                    ratios.append(k)
                    if r0 >= .005:
                        long_ratios.append(k)
                    if k > 1.5 and p == 'Jacket':
                        r = region(rest[p][a]); stretch_where[r] = stretch_where.get(r, 0) + 1
            ratios.sort(); long_ratios.sort()
            st = {'sampled_vertices': len(sd), 'inside_body_over_5mm': sum(1 for x in sd if x < -.005),
                  'inside_body_over_5mm_ratio': round(sum(1 for x in sd if x < -.005) / len(sd), 4),
                  'min_signed_distance_mm': round(min(sd) * 1000, 2),
                  'edge_stretch_p99': round(ratios[int(len(ratios) * .99)], 3), 'edge_stretch_max': round(ratios[-1], 3),
                  'edges_over_1_5x': sum(1 for r in ratios if r > 1.5),
                  'edges_5mm_plus_max': round(long_ratios[-1], 3) if long_ratios else None,
                  'edges_5mm_plus_over_1_5x': sum(1 for r in long_ratios if r > 1.5)}
            if p == 'Jacket':
                st['inside_by_region_min_mm'] = {k: round(v * 1000, 1) for k, v in sorted(where.items())}
                st['inside_against_body_part'] = dict(sorted(against.items()))
                st['stretch_by_region'] = dict(sorted(stretch_where.items()))
            out[pose][p] = st
    clear_pose()
    report['metrics_' + tag] = out


# ---------- renders ----------

def setup_render():
    review = bpy.data.materials.get('Reference_ReviewGrey')
    if review:
        for slot in body.material_slots:
            slot.link = 'OBJECT'; slot.material = review
    pc.hide_render = True
    cam = sc.camera or bpy.data.objects.get('ReviewCamera'); sc.camera = cam
    cam.data.type = 'ORTHO'
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 12; sc.cycles.use_denoising = True
    sc.render.resolution_x = 760; sc.render.resolution_y = 900
    PREV.mkdir(parents=True, exist_ok=True)
    return cam


def shot(cam, name, eye, target, scale):
    cam.location = Vector(eye)
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = scale
    sc.render.filepath = str(PREV / (name + '.png'))
    bpy.ops.render.render(write_still=True)


def renders(tag):
    cam = setup_render()
    for pose, apply in POSES.items():
        clear_pose(); apply(pa); apply(arm)
        shot(cam, f'{tag}_{pose}_Front', (0, -6, 1.0), (0, 0, .95), 2.1)
        shot(cam, f'{tag}_{pose}_ThreeQuarter', (4, -4.6, 1.6), (0, 0, .95), 2.1)
        if pose in ('ArmsDown', 'HandToChest', 'LightBend'):
            shot(cam, f'{tag}_{pose}_ArmpitL', (3.2, -1.6, 1.35), (.2, 0, 1.22), .75)
    clear_pose()
    shot(cam, f'{tag}_Rest_Front', (0, -6, 1.0), (0, 0, .95), 2.1)


# ---------- fix ----------

def islands_by_vertex(me):
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    island = [-1] * len(bm.verts); count = []
    for v in bm.verts:
        if island[v.index] >= 0:
            continue
        k = len(count); stack = [v]; island[v.index] = k; n = 0
        while stack:
            w = stack.pop(); n += 1
            for e in w.link_edges:
                u = e.other_vert(w)
                if island[u.index] < 0:
                    island[u.index] = k; stack.append(u)
        count.append(n)
    bm.free()
    return island, count


def short(n):
    return n[len(PFX):] if n.startswith(PFX) else n


def remap_jacket(b):
    s = 'Left' if b.startswith('Left') else ('Right' if b.startswith('Right') else '')
    if b == 'Head':
        return 'Neck'
    if 'Hand' in b and b not in ('LeftHand', 'RightHand'):
        return s + 'Hand'
    if any(k in b for k in ('UpLeg', 'Leg', 'Foot', 'Toe')):
        return 'Hips'
    return b


def fix_jacket():
    me = jo.data
    rest_tree = body_tree()  # rest pose
    island, sizes = islands_by_vertex(me)
    main = max(range(len(sizes)), key=lambda k: sizes[k])
    co = [v.co.copy() for v in me.vertices]

    # 1) Less slack under the upper sleeve near the armpit. The slack is measured from the arm skin on the outer shell and the
    #    same displacement is applied to every vertex within 3 mm (inner shell, rims), so the 1.8-2.5 mm cloth keeps its thickness.
    kd = KDTree(len(co))
    for i, c in enumerate(co):
        kd.insert(c, i)
    kd.balance()
    # Arm skin only (reference body faces weighted mostly to the upper arm / forearm), so the sleeve is taken in toward its own
    # arm and never toward the torso.
    bp, bf = evaluated(body)
    gidx = {g.index: g.name for g in body.vertex_groups}
    armw = [sum(g.weight for g in v.groups if gidx[g.group].endswith(('Arm', 'ForeArm')) and 'Shoulder' not in gidx[g.group]) for v in body.data.vertices]
    arm_faces = [f for f in bf if all(armw[i] > .6 for i in f)]
    arm_tree = BVHTree.FromPolygons(bp, arm_faces)
    clearance = []
    toward = []
    for c in co:
        loc, no, idx, d = arm_tree.find_nearest(c)
        clearance.append(d if loc is not None else 0.0); toward.append((loc - c).normalized() if loc is not None else Vector())
    moved = 0; largest = 0.0
    new = list(co)
    for i, c in enumerate(co):
        ax = abs(c.x)
        if island[i] != main or not (UNDERARM_X[0] <= ax <= UNDERARM_X[1]):
            continue
        axis_z = 1.432 - (ax - .187) * .075  # upper arm bone line in the rest pose (head 1.432 at 0.187, tail 1.413 at 0.441)
        if c.z > axis_z - .025:
            continue  # only the underside of the sleeve
        fade = 1.0 if ax <= UNDERARM_FULL_X else max(0.0, 1 - (ax - UNDERARM_FULL_X) / (UNDERARM_X[1] - UNDERARM_FULL_X))
        outer = max(clearance[j] for _, j, _ in kd.find_range(c, .003))
        slack = outer - UNDERARM_TARGET
        if slack <= 0 or outer > UNDERARM_MAX_CLEARANCE:
            continue  # not sleeve cloth around this arm (or already close to it)
        step = min(slack * UNDERARM_SHARE * fade, UNDERARM_MAX_STEP)
        new[i] = c + toward[i] * step
        moved += 1; largest = max(largest, step)
    for v, c in zip(me.vertices, new):
        v.co = c
    me.update()

    # 2) Weights again from the body (nearest face), remapped as before.
    for g in list(jo.vertex_groups):
        jo.vertex_groups.remove(g)
    body_groups = [g.name for g in body.vertex_groups]
    for n in body_groups:
        jo.vertex_groups.new(name=n)
    mods = [m for m in jo.modifiers if m.type == 'ARMATURE']
    for m in mods:
        m.show_viewport = False
    bpy.ops.object.select_all(action='DESELECT'); jo.select_set(True); vl.objects.active = jo
    m = jo.modifiers.new('WeightTransfer', 'DATA_TRANSFER')
    m.object = body; m.use_object_transform = True; m.use_vert_data = True
    m.data_types_verts = {'VGROUP_WEIGHTS'}; m.vert_mapping = 'POLYINTERP_NEAREST'
    m.layers_vgroup_select_src = 'ALL'; m.layers_vgroup_select_dst = 'NAME'
    bpy.ops.object.modifier_move_to_index(modifier=m.name, index=0)
    bpy.ops.object.modifier_apply(modifier=m.name)
    names = [short(g.name) for g in jo.vertex_groups]
    weights = []
    for v in me.vertices:
        w = {}
        for ge in v.groups:
            if ge.weight > 0:
                b = remap_jacket(names[ge.group]); w[b] = w.get(b, 0) + ge.weight
        weights.append(w)

    # 3) Smoothing on the main shell: two passes everywhere (as before), then wider blends in the shoulder / armpit and elbow zones.
    adj = [[] for _ in me.vertices]
    for e in me.edges:
        a, b = e.vertices; adj[a].append(b); adj[b].append(a)

    def smooth(passes, select):
        nonlocal weights
        for _ in range(passes):
            nxt = list(weights)
            for i in select:
                if not adj[i]:
                    continue
                avg = {}
                for j in adj[i]:
                    for k, x in weights[j].items():
                        avg[k] = avg.get(k, 0) + x / len(adj[i])
                nxt[i] = {k: .5 * weights[i].get(k, 0) + .5 * avg.get(k, 0) for k in set(weights[i]) | set(avg)}
            weights = nxt

    shell = [i for i in range(len(co)) if island[i] == main]
    smooth(GLOBAL_PASSES, shell)
    elbow = [i for i in shell if .37 < abs(new[i].x) < .53]
    smooth(ELBOW_PASSES, elbow)

    # 4) At most 4 influences, normalized; write back.
    final = []
    for w in weights:
        top4 = sorted(((k, x) for k, x in w.items() if x > 1e-4), key=lambda t: -t[1])[:4]
        total = sum(x for _, x in top4) or 1.0
        final.append([(k, x / total) for k, x in top4] if top4 else [('Hips', 1.0)])
    for g in list(jo.vertex_groups):
        jo.vertex_groups.remove(g)
    used = sorted({k for f in final for k, _ in f})
    groups = {k: jo.vertex_groups.new(name=PFX + k) for k in used}
    for i, f in enumerate(final):
        for k, x in f:
            groups[k].add([i], x, 'REPLACE')
    for m in mods:
        m.show_viewport = True
    vl.update()
    report['fix'] = {'underarm_vertices_moved': moved, 'underarm_largest_move_mm': round(largest * 1000, 1),
                     'elbow_zone_vertices': len(elbow), 'islands': len(sizes), 'jacket_bones': used,
                     'vertices': len(co), 'triangles': len(me.polygons)}


# ---------- run ----------

metrics('before')
if MODE == 'final':
    renders('Before')
fix_jacket()
metrics('after')
if MODE == 'final':
    renders('After')

problems = []
for n, (m, parent) in contract.items():
    o = bpy.data.objects.get(n)
    if o is None:
        problems.append(n + ' missing'); continue
    if (o.parent.name if o.parent else None) != parent:
        problems.append(n + ' parent')
    if max(abs(o.matrix_local[i][j] - m[i][j]) for i in range(4) for j in range(4)) > 0:
        problems.append(n + ' matrix')
problems += [n + ' mesh renamed' for n, mn in mesh_names.items() if bpy.data.objects[n].data.name != mn]
for p in other_vertices:
    o = bpy.data.objects[p + '_Worn']
    if any((v.co - c).length > 0 for v, c in zip(o.data.vertices, other_vertices[p])) or \
            [sorted((g.group, round(g.weight, 6)) for g in v.groups) for v in o.data.vertices] != other_weights[p]:
        problems.append(p + '_Worn changed')
clear_pose()
rest_err = max((a - b.co).length for a, b in zip(evaluated(jo)[0], jo.data.vertices))
report['contract'] = {'objects_checked': len(contract), 'problems': problems, 'jacket_rest_skin_error_m': rest_err,
                      'bones': len(pa.data.bones), 'other_worn_meshes_unchanged': not any('changed' in x for x in problems)}
report['poses'] = POSE_TEXT
assert not problems, problems

if MODE == 'final':
    pc.hide_viewport = True; pc.hide_render = True
    bpy.context.preferences.filepaths.save_version = 0
    # The renders changed the reference body's material slots: save from a clean state instead (reopen the input, re-apply only
    # the jacket fix, save and export).
    bpy.ops.wm.open_mainfile(filepath=str(BACKUP))
    sc = bpy.context.scene; vl = bpy.context.view_layer
    for img in bpy.data.images:
        if img.name.startswith('PC_') and img.packed_file:
            img.filepath = '//../Textures/' + Path(img.filepath).name
    wc = bpy.data.collections['Worn']; pc = bpy.data.collections['Pickup']
    pa = bpy.data.objects['PC_Armature']; arm = bpy.data.objects['Armature']; body = bpy.data.objects['Ch36']
    jo = bpy.data.objects['Jacket_Worn']
    fix_jacket()
    pc.hide_viewport = True; pc.hide_render = True
    bpy.ops.wm.save_as_mainfile(filepath=str(S / 'ProtectiveClothing.blend'), relative_remap=False)
    pc.hide_viewport = False
    bpy.ops.object.select_all(action='DESELECT')
    for o in wc.objects:
        if o.type in {'MESH', 'EMPTY', 'ARMATURE'}:
            o.hide_set(False); o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(P / 'ProtectiveClothing_Worn.fbx'), use_selection=True, object_types={'MESH', 'EMPTY', 'ARMATURE'},
                             global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             use_space_transform=True, bake_space_transform=False, bake_anim=False, add_leaf_bones=False, use_custom_props=True,
                             path_mode='RELATIVE', embed_textures=False, mesh_smooth_type='FACE', use_armature_deform_only=True,
                             primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL')
    # Re-import and compare with the saved source.
    expected = {o.name: (o.parent.name if o.parent else None, o.matrix_world.copy()) for o in wc.objects}
    clear_pose()
    rest_src = {p: evaluated(bpy.data.objects[p + '_Worn'])[0] for p in PARTS}
    POSES['HandToChest'](pa)
    posed_src = {p: evaluated(bpy.data.objects[p + '_Worn'])[0] for p in PARTS}
    for pb in pa.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bone_heads = {b.name: pa.matrix_world @ b.head_local for b in pa.data.bones}
    bpy.ops.wm.read_homefile(use_empty=True)
    sc = bpy.context.scene; vl = bpy.context.view_layer
    coll = bpy.data.collections.new('Reimport_Worn'); sc.collection.children.link(coll)
    vl.active_layer_collection = vl.layer_collection.children['Reimport_Worn']
    bpy.ops.import_scene.fbx(filepath=str(P / 'ProtectiveClothing_Worn.fbx'), use_custom_props=True, ignore_leaf_bones=True, automatic_bone_orientation=False)
    imp = {o.name: o for o in coll.objects}
    ia = imp['PC_Armature']
    mat = max(max(abs(imp[n].matrix_world[i][j] - m[i][j]) for i in range(4) for j in range(4)) for n, (_, m) in expected.items() if n in imp and imp[n].type != 'ARMATURE')
    bone = max((ia.matrix_world @ ia.data.bones[n].head_local - h).length for n, h in bone_heads.items() if n in ia.data.bones)
    worst_rest = 0.0; worst_pose = 0.0; mod_ok = True
    pa = ia
    for p in PARTS:
        o = imp[p + '_Worn']
        mod_ok &= any(m.type == 'ARMATURE' and m.object == ia for m in o.modifiers)
        kd = KDTree(len(rest_src[p]))
        for i, v in enumerate(rest_src[p]):
            kd.insert(v, i)
        kd.balance()
        pair = [kd.find(o.matrix_world @ v.co) for v in o.data.vertices]
        worst_rest = max(worst_rest, max(d for _, _, d in pair))
    POSES['HandToChest'](ia)
    for p in PARTS:
        o = imp[p + '_Worn']
        pts = evaluated(o)[0]
        kd = KDTree(len(rest_src[p]))
        for i, v in enumerate(rest_src[p]):
            kd.insert(v, i)
        kd.balance()
        for v, q in zip(o.data.vertices, pts):
            _, j, _ = kd.find(o.matrix_world @ v.co)
            worst_pose = max(worst_pose, (q - posed_src[p][j]).length)
    report['roundtrip'] = {'objects': len(imp), 'missing': [n for n in expected if n not in imp], 'max_matrix_difference': mat,
                           'max_bone_head_difference_m': bone, 'max_rest_vertex_difference_m': worst_rest,
                           'max_posed_vertex_difference_HandToChest_m': worst_pose, 'armature_modifiers_to_PC_Armature': mod_ok,
                           'pickup_fbx': 'not re-exported (unchanged)'}
    bpy.ops.wm.save_as_mainfile(filepath=str(S / 'FBX_Roundtrip_Review.blend'))

report['seconds'] = round(time.time() - T0, 1)
out = S / ('UserWearFixReport.json' if MODE == 'final' else '_userwear_fix_metrics.json')
out.write_text(json.dumps(report, indent=1, default=str), encoding='utf8')
print('FIX_DONE', MODE, report.get('fix'))
