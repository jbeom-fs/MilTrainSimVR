"""Jacket deformation fix 2 (2026-11-04): the remaining deep penetration under the upper sleeves and at the sides in the light
bend / hands-to-chest poses and the over-stretched armpit / shoulder edges with the arms down. Only Jacket_Worn's vertex
positions in those areas and its vertex weights change; names, pivots, Grip / Attach / root transforms, PC_Armature and every
other mesh stay as they are. No cloth simulation, no corrective shapes, nothing added for runtime.

Input. The live ProtectiveClothing.blend was found (2026-11-04) saved at 18:38 on 11/03 with the content of the backup made
BEFORE the user-wear change (hood and cape, no PC_Armature, no weights; identical to Backups/20261103_userwear_before in
objects, meshes, weights, UVs, modifiers, materials and collections), i.e. older than ProtectiveClothing_Worn.fbx (18:20, the
fixed jacket). That file is kept in Backups/20261104_jacket_deform_before. The source of the current Worn FBX is rebuilt here
instead: Backups/20261103_userwear_fix_before (the user-wear result) + the previous jacket fix (userwear_fix_model.py, same code
and tuning, reproduced below), checked against the Worn FBX of the 11/04 backup (rest vertices and weights).
Run: blender -b --factory-startup --python jacket_deform_fix.py -- [diag|metrics|final] [out.json] [tuning json]
  diag: details of the problem vertices / edges of the rebuilt previous result; metrics: previous vs new numbers (nothing
  saved); final: also renders, saves the .blend, exports the worn FBX and re-imports both FBX files.
"""
import bpy, bmesh, math, json, sys, time
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

T0 = time.time()
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
MODE = ARGS[0] if ARGS else 'metrics'
S = Path(__file__).resolve().parent
P = S.parent
INPUT = S / 'Backups' / '20261103_userwear_fix_before' / 'Model' / 'Source~' / 'ProtectiveClothing.blend'
OLD_FBX = S / 'Backups' / '20261104_jacket_deform_before' / 'Model' / 'ProtectiveClothing_Worn.fbx'
PREV = S / 'Previews' / 'JacketDeformFix'
OUT = Path(ARGS[1]) if len(ARGS) > 1 else S / ('JacketDeformFixReport.json' if MODE == 'final' else '_jacket_deform_metrics.json')
PFX = 'mixamorig1:'
PARTS = ['Jacket', 'Trousers', 'Glove_L', 'Glove_R', 'Overboot_L', 'Overboot_R']

# Previous fix (userwear_fix_model.py, unchanged values).
PREV_TUNING = dict(target=0.007, share=0.8, x=(0.22, 0.40), full_x=0.28, max_clearance=0.045, max_step=0.025, global_passes=2,
                   elbow_passes=10)
# This fix (see fix_jacket2); overridable for experiments with a JSON argument.
TUNING = dict()
if len(ARGS) > 2:
    TUNING.update(json.loads(ARGS[2]))

report = {'input': str(INPUT.relative_to(S)), 'mode': MODE, 'blender': bpy.app.version_string, 'tuning': TUNING}


def load():
    global sc, vl, wc, pc, pa, arm, body, jo, src_basis
    bpy.ops.wm.open_mainfile(filepath=str(INPUT))
    sc = bpy.context.scene; vl = bpy.context.view_layer
    for img in bpy.data.images:
        if img.name.startswith('PC_') and img.packed_file:
            img.filepath = '//../Textures/' + Path(img.filepath).name
    wc = bpy.data.collections['Worn']; pc = bpy.data.collections['Pickup']
    pa = bpy.data.objects['PC_Armature']; arm = bpy.data.objects['Armature']; body = bpy.data.objects['Ch36']
    jo = bpy.data.objects['Jacket_Worn']
    src_basis = {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones}


# ---------- poses (same construction as userwear_fix_model.py) ----------

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


def pose(name):
    clear_pose(); POSES[name](pa); POSES[name](arm)


def evaluated(o):
    dg = bpy.context.evaluated_depsgraph_get(); ev = o.evaluated_get(dg); me = ev.to_mesh()
    pts = [ev.matrix_world @ v.co for v in me.vertices]; faces = [tuple(f.vertices) for f in me.polygons]
    ev.to_mesh_clear()
    return pts, faces


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
    global BODY_PART
    if BODY_PART is None:
        gidx = {g.index: g.name for g in body.vertex_groups}
        BODY_PART = []
        for v in body.data.vertices:
            g = max(v.groups, key=lambda x: x.weight, default=None)
            n = gidx[g.group] if g else ''
            BODY_PART.append('arm' if ('Arm' in n or 'Hand' in n) else 'leg' if ('Leg' in n or 'Foot' in n) else 'torso')
    return BODY_PART[faces[face_index][0]]


def metrics(tag, every=3, detail=False):
    """Same measures as userwear_fix_model.py: every 3rd vertex against the posed reference body (signed distance along the
    nearest face normal), edge length ratio to the rest pose. detail: every jacket vertex below -5 mm and every edge over 1.5x."""
    global BODY_PART
    BODY_PART = None
    clear_pose()
    rest = {p: evaluated(bpy.data.objects[p + '_Worn'])[0] for p in PARTS}
    edges = {p: [tuple(e.vertices) for e in bpy.data.objects[p + '_Worn'].data.edges] for p in PARTS}
    out = {}; det = {}
    # The rest (bind) pose itself, every jacket vertex: a shape change must not put cloth into the body there.
    bpts, bfaces = evaluated(body)
    tree = BVHTree.FromPolygons(bpts, bfaces)
    sd = [(rest['Jacket'][i] - loc).dot(no) for i in range(len(rest['Jacket'])) for loc, no, _, _ in [tree.find_nearest(rest['Jacket'][i])]]
    report['rest_jacket_' + tag] = {'inside_body_over_2mm': sum(1 for x in sd if x < -.002), 'min_signed_distance_mm': round(min(sd) * 1000, 2)}
    for name in POSES:
        pose(name)
        bpts, bfaces = evaluated(body)
        tree = BVHTree.FromPolygons(bpts, bfaces)
        out[name] = {}
        for p in PARTS:
            pts = evaluated(bpy.data.objects[p + '_Worn'])[0]
            sd = []; where = {}; against = {}
            for i in range(0, len(pts), every):
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
                # Every vertex too (the sampled count above is the measure the earlier reports used).
                every_sd = [(pts[i] - loc).dot(no) for i in range(len(pts)) for loc, no, _, _ in [tree.find_nearest(pts[i])]]
                st['all_vertices_inside_over_5mm'] = sum(1 for x in every_sd if x < -.005)
                st['all_vertices_min_signed_distance_mm'] = round(min(every_sd) * 1000, 2)
                st['inside_by_region_min_mm'] = {k: round(v * 1000, 1) for k, v in sorted(where.items())}
                st['inside_against_body_part'] = dict(sorted(against.items()))
                st['stretch_by_region'] = dict(sorted(stretch_where.items()))
            out[name][p] = st
            if detail and p == 'Jacket':
                vin = []
                for i in range(len(pts)):
                    loc, no, idx, d = tree.find_nearest(pts[i])
                    s = (pts[i] - loc).dot(no)
                    if s < -.005:
                        vin.append([i, round(s * 1000, 1), [round(c, 3) for c in rest[p][i]], region(rest[p][i]), body_part_of(idx, bfaces)])
                ein = []
                for a, b in edges[p]:
                    r0 = (rest[p][a] - rest[p][b]).length
                    if r0 > 1e-5 and (pts[a] - pts[b]).length / r0 > 1.5:
                        ein.append([a, b, round((pts[a] - pts[b]).length / r0, 2), round(r0 * 1000, 1),
                                    [round(c, 3) for c in (rest[p][a] + rest[p][b]) / 2], region(rest[p][a])])
                det[name] = {'inside': vin, 'stretched': ein}
    clear_pose()
    report['metrics_' + tag] = out
    if detail:
        report['detail_' + tag] = det
    return out


# ---------- shared mesh helpers ----------

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


def transferred_weights():
    """Body weights on the jacket (nearest face interpolation), remapped to the jacket bones; the jacket's groups are replaced."""
    me = jo.data
    for g in list(jo.vertex_groups):
        jo.vertex_groups.remove(g)
    for g in body.vertex_groups:
        jo.vertex_groups.new(name=g.name)
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
    return weights, mods


def adjacency(me):
    adj = [[] for _ in me.vertices]
    for e in me.edges:
        a, b = e.vertices; adj[a].append(b); adj[b].append(a)
    return adj


def smooth(weights, adj, passes, select, share=.5):
    for _ in range(passes):
        nxt = list(weights)
        for i in select:
            if not adj[i]:
                continue
            avg = {}
            for j in adj[i]:
                for k, x in weights[j].items():
                    avg[k] = avg.get(k, 0) + x / len(adj[i])
            nxt[i] = {k: (1 - share) * weights[i].get(k, 0) + share * avg.get(k, 0) for k in set(weights[i]) | set(avg)}
        weights = nxt
    return weights


def write_weights(weights, mods):
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
    return used


def arm_skin_tree():
    bp, bf = evaluated(body)
    gidx = {g.index: g.name for g in body.vertex_groups}
    armw = [sum(g.weight for g in v.groups if gidx[g.group].endswith(('Arm', 'ForeArm')) and 'Shoulder' not in gidx[g.group]) for v in body.data.vertices]
    return BVHTree.FromPolygons(bp, [f for f in bf if all(armw[i] > .6 for i in f)])


# ---------- previous fix (userwear_fix_model.py fix_jacket, same code path and values) ----------

def previous_fix():
    t = PREV_TUNING
    me = jo.data
    island, sizes = islands_by_vertex(me)
    main = max(range(len(sizes)), key=lambda k: sizes[k])
    co = [v.co.copy() for v in me.vertices]
    kd = KDTree(len(co))
    for i, c in enumerate(co):
        kd.insert(c, i)
    kd.balance()
    tree = arm_skin_tree()
    clearance = []; toward = []
    for c in co:
        loc, no, idx, d = tree.find_nearest(c)
        clearance.append(d if loc is not None else 0.0); toward.append((loc - c).normalized() if loc is not None else Vector())
    new = list(co)
    for i, c in enumerate(co):
        ax = abs(c.x)
        if island[i] != main or not (t['x'][0] <= ax <= t['x'][1]):
            continue
        axis_z = 1.432 - (ax - .187) * .075
        if c.z > axis_z - .025:
            continue
        fade = 1.0 if ax <= t['full_x'] else max(0.0, 1 - (ax - t['full_x']) / (t['x'][1] - t['full_x']))
        outer = max(clearance[j] for _, j, _ in kd.find_range(c, .003))
        slack = outer - t['target']
        if slack <= 0 or outer > t['max_clearance']:
            continue
        new[i] = c + toward[i] * min(slack * t['share'] * fade, t['max_step'])
    for v, c in zip(me.vertices, new):
        v.co = c
    me.update()
    weights, mods = transferred_weights()
    adj = adjacency(me)
    shell = [i for i in range(len(co)) if island[i] == main]
    weights = smooth(weights, adj, t['global_passes'], shell)
    weights = smooth(weights, adj, t['elbow_passes'], [i for i in shell if .37 < abs(new[i].x) < .53])
    write_weights(weights, mods)


# ---------- this fix ----------

def fade(v, a, b):
    """0 at a, 1 at b (a > b allowed for a falling edge)."""
    if a == b:
        return 1.0 if v >= b else 0.0
    return min(1.0, max(0.0, (v - a) / (b - a)))


def torso_skin_tree():
    bp, bf = evaluated(body)
    gidx = {g.index: g.name for g in body.vertex_groups}
    torso = []
    for v in body.data.vertices:
        g = max(v.groups, key=lambda x: x.weight, default=None)
        n = gidx[g.group] if g else ''
        torso.append(not any(k in n for k in ('Arm', 'Hand', 'Leg', 'Foot', 'Toe')))
    return BVHTree.FromPolygons(bp, [f for f in bf if all(torso[i] for i in f)])


def side_take_in(t, me, island, main):
    """Less slack of the torso side under the armpit (the loose cloth an arm hanging down passes through). Main shell vertices
    in the zone move toward the torso skin; the move is measured on the outermost cloth within 3 mm, so the shell keeps its
    thickness, and attached islands in the zone move with their nearest shell vertex, so nothing floats or sinks."""
    clear_pose()
    co = [v.co.copy() for v in me.vertices]
    tree = torso_skin_tree()
    kd = KDTree(len(co))
    for i, c in enumerate(co):
        kd.insert(c, i)
    kd.balance()
    kd_main = KDTree(sum(1 for k in island if k == main))
    for i, c in enumerate(co):
        if island[i] == main:
            kd_main.insert(c, i)
    kd_main.balance()
    near = {}
    for i, c in enumerate(co):
        loc, no, idx, d = tree.find_nearest(c)
        near[i] = (d, (loc - c).normalized()) if loc is not None else (0.0, Vector())
    z0, z1, z2, z3 = t['side_z']; x0, x1, x2, x3 = t['side_x']
    far = t['side_max_clearance']
    step = {}
    for i, c in enumerate(co):
        if island[i] != main:
            continue
        ax = abs(c.x)
        # Every border fades (height, width, distance from the skin), so the taken-in area has no step at its edge.
        w = fade(ax, x0, x1) * fade(ax, x3, x2) * fade(c.z, z0, z1) * fade(c.z, z3, z2)
        if w <= 0:
            continue
        outer = max(near[j][0] for _, j, _ in kd.find_range(c, .003) if island[j] == main)
        slack = outer - t['side_target']
        w *= fade(outer, far, far - .015)
        if slack <= 0 or w <= 0:
            continue
        # Never closer to the skin than side_min_gap on its own (a fold whose outer layer is far from the skin).
        s = min(min(slack * t['side_share'], t['side_max_step']) * w, max(0.0, near[i][0] - t['side_min_gap']))
        if s > 0:
            step[i] = (s, near[i][1])
    follow = {}
    for i, c in enumerate(co):
        if island[i] != main:
            _, j, d = kd_main.find(c)
            if j in step and d < .006:
                follow[i] = j
    info = {}
    if t['side_choose']:
        share = choose_side_share(t, me, co, step, follow, kd, island, main)
        info['side_share_chosen'] = {k: sum(1 for x in share.values() if abs(x - k) < .125) for k in (0.0, .25, .5, .75, 1.0)}
    else:
        share = {i: 1.0 for i in step}
    moved = {}
    for i in step:
        if share[i] > 1e-3:
            moved[i] = (step[i][0] * share[i], step[i][1])
    for i, j in follow.items():
        if j in moved:
            moved[i] = moved[j]
    for i, (s, direction) in moved.items():
        me.vertices[i].co = co[i] + direction * s
    me.update()
    info.update({'side_vertices_moved': len(moved), 'side_attached_vertices_moved': sum(1 for i in moved if island[i] != main),
                 'side_largest_move_mm': round(max((s for s, _ in moved.values()), default=0) * 1000, 1)})
    return info


def choose_side_share(t, me, co, step, follow, kd, island, main):
    """How much of each vertex's take-in to use: the share (0, 1/4 ... 1) that gives the vertex (and the attached cloth that
    follows it) the largest smallest clearance to the posed body over the review poses, with the weights as they are. The
    choice is then smoothed over the cloth and made equal within 3 mm (both layers), so the taken-in surface stays smooth."""
    shares = (0.0, .25, .5, .75, 1.0)
    users = {}
    for i in step:
        users[i] = [i]
    for i, j in follow.items():
        users[j].append(i)
    score = {i: [] for i in step}
    for k in shares:
        for i, (s, direction) in step.items():
            for u in users[i]:
                me.vertices[u].co = co[u] + direction * s * k
        me.update()
        worst = {i: 1.0 for i in step}
        for name in POSES:
            pose(name)
            bp, bf = evaluated(body)
            tree = BVHTree.FromPolygons(bp, bf)
            pts = evaluated(jo)[0]
            for i in step:
                for u in users[i]:
                    loc, no, _, _ = tree.find_nearest(pts[u])
                    worst[i] = min(worst[i], (pts[u] - loc).dot(no))
        clear_pose()
        for i in step:
            score[i].append(worst[i])
    for i, u in users.items():
        for x in u:
            me.vertices[x].co = co[x]
    me.update()
    # Largest share whose worst clearance is within 1 mm of the best one (less move when it does not help).
    best = {}
    for i, sc_ in score.items():
        top = max(sc_)
        best[i] = max(k for k, x in zip(shares, sc_) if x >= top - .001)
    adj = adjacency(me)
    for _ in range(t['side_choose_passes']):
        nxt = dict(best)
        for i in best:
            nb = [best.get(j, 0.0) for j in adj[i]]
            if nb:
                nxt[i] = .5 * best[i] + .5 * sum(nb) / len(nb)
        best = nxt
    return {i: min(best.get(j, 0.0) for _, j, _ in kd.find_range(co[i], .003) if island[j] == main) for i in best}


def collar_arm_weights(t, weights, rest, island, main):
    """Arm / forearm / hand influence on the stand collar island goes to that side's shoulder: the collar must not swing with
    the upper arm (its 3 mm layers stretched up to 2.6x with the arms down). The collar is the attached island with the most
    vertices near the neck; the shell around it is not changed (a zone border there made new steps)."""
    count = {}
    for i, c in enumerate(rest):
        if island[i] != main and abs(c.x) < t['collar_x'] and c.z > t['collar_z']:
            count[island[i]] = count.get(island[i], 0) + 1
    collar = max(count, key=count.get) if count else None
    n = 0
    for i, w in enumerate(weights):
        if island[i] != collar:
            continue
        for side in ('Left', 'Right'):
            x = sum(w.pop(k) for k in [k for k in w if k.startswith(side) and k != side + 'Shoulder'])
            if x > 0:
                w[side + 'Shoulder'] = w.get(side + 'Shoulder', 0) + x; n += 1
    return n


def posed_jacket_points():
    out = []
    for name in POSES:
        pose(name)
        out.append(evaluated(jo)[0])
    clear_pose()
    return out


def stretch_relax(t, weights, adj, mods):
    """TRIED AND REJECTED (relax_rounds 0; see JacketDeformFixExperiments.json): smoothing driven by the stretch itself, the
    end vertices of edges stretched over the threshold in any review pose and their neighbours get one pass per round. It
    spread the arm weights into the torso cloth: more penetration and more stretched edges in every pose."""
    clear_pose()
    rest = evaluated(jo)[0]
    edges = [tuple(e.vertices) for e in jo.data.edges]
    rounds = []
    for _ in range(t['relax_rounds']):
        write_weights(weights, mods)
        posed = posed_jacket_points()
        sel = set()
        for a, b in edges:
            r0 = (rest[a] - rest[b]).length
            if r0 > 1e-5 and any((p[a] - p[b]).length / r0 > t['relax_threshold'] for p in posed):
                sel.add(a); sel.add(b)
        if not sel:
            break
        if t['relax_ring']:
            sel |= {j for i in list(sel) for j in adj[i]}
        rounds.append(len(sel))
        weights = smooth(weights, adj, 1, sorted(sel), t['relax_share'])
    return weights, rounds


def short_edge_equalize(t, weights, mods):
    """Both ends of a short jacket edge (under short_edge_m: cloth thickness, seams, fine folds) that is stretched over 1.5x in
    a review pose get the average of their weights: such an edge is a weight step between two layers or two very close
    vertices, not a part of the cloth that should stretch. Longer edges are left to the shell blend."""
    clear_pose()
    rest = evaluated(jo)[0]
    short_edges = [tuple(e.vertices) for e in jo.data.edges if 1e-5 < (rest[e.vertices[0]] - rest[e.vertices[1]]).length < t['short_edge_m']]
    rounds = []
    for _ in range(t['short_rounds']):
        write_weights(weights, mods)
        posed = posed_jacket_points()
        pairs = [(a, b) for a, b in short_edges
                 if any((p[a] - p[b]).length / (rest[a] - rest[b]).length > 1.5 for p in posed)]
        if not pairs:
            break
        rounds.append(len(pairs))
        weights = list(weights)
        for a, b in pairs:
            avg = {k: .5 * (weights[a].get(k, 0) + weights[b].get(k, 0)) for k in set(weights[a]) | set(weights[b])}
            weights[a] = dict(avg); weights[b] = dict(avg)
    return weights, rounds


TUNING_DEFAULT = dict(short_rounds=0, short_edge_m=0.004,side_target=0.015, side_share=0.7, side_max_step=0.025, side_max_clearance=0.07, side_min_gap=0.006,
                      side_z=(1.15, 1.22, 1.34, 1.40), side_x=(0.14, 0.17, 0.195, 0.225),
                      collar_x=0.17, collar_z=1.46, relax_rounds=0, relax_threshold=1.4, relax_share=0.5, relax_ring=True,
                      take_in=True, collar=True, retransfer=True, side_choose=True, side_choose_passes=4)
TUNING_DEFAULT.update(short_rounds=5, short_edge_m=0.0075)
TUNING = {**TUNING_DEFAULT, **TUNING}
report['tuning'] = TUNING


def fix_jacket2():
    t = TUNING
    me = jo.data
    island, sizes = islands_by_vertex(me)
    main = max(range(len(sizes)), key=lambda k: sizes[k])
    fix = {}
    if t['take_in']:
        fix.update(side_take_in(t, me, island, main))
    adj = adjacency(me)
    if t['retransfer']:
        # Weights again with the previous pipeline (transfer, 2 shell passes, 10 elbow passes) on the new shape.
        weights, mods = transferred_weights()
        shell = [i for i in range(len(me.vertices)) if island[i] == main]
        weights = smooth(weights, adj, PREV_TUNING['global_passes'], shell)
        weights = smooth(weights, adj, PREV_TUNING['elbow_passes'], [i for i in shell if .37 < abs(me.vertices[i].co.x) < .53])
    else:
        # The previous weights stay (a taken-in vertex keeps how it follows the bones).
        names = {g.index: short(g.name) for g in jo.vertex_groups}
        weights = [{names[g.group]: g.weight for g in v.groups if g.weight > 0} for v in me.vertices]
        mods = [m for m in jo.modifiers if m.type == 'ARMATURE']
    if t['collar']:
        fix['collar_vertices_reassigned'] = collar_arm_weights(t, weights, [v.co for v in me.vertices], island, main)
    if t['relax_rounds']:
        weights, rounds = stretch_relax(t, weights, adj, mods)
        fix['relax_selected_per_round'] = rounds
    if t['short_rounds']:
        weights, pairs = short_edge_equalize(t, weights, mods)
        fix['short_edge_pairs_per_round'] = pairs
    fix['jacket_bones'] = write_weights(weights, mods)
    fix.update(vertices=len(me.vertices), triangles=len(me.polygons), islands=len(sizes))
    report['fix'] = fix


def rest_shape_change(before):
    d = [(v.co - c).length for v, c in zip(jo.data.vertices, before)]
    return {'moved_over_0_1mm': sum(1 for x in d if x > 1e-4), 'largest_mm': round(max(d) * 1000, 1)}


# ---------- renders (same studio, camera type, samples and size as userwear_fix_model.py; Before / After in one run) ----------

SHOTS = {  # name: (eye, target, ortho scale, poses or None for all)
    'Front': ((0, -6, 1.0), (0, 0, .95), 2.1, None),
    'ThreeQuarter': ((4, -4.6, 1.6), (0, 0, .95), 2.1, None),
    'ArmpitL': ((3.2, -1.6, 1.35), (.2, 0, 1.22), .75, ('ArmsDown', 'HandToChest', 'LightBend')),
    # The taken-in torso side under each arm, from behind and outside (the arms hang forward in these poses).
    'SideL': ((1.6, 1.3, 1.1), (.19, 0, 1.27), .6, ('ArmsDown', 'HandToChest', 'TorsoTwist', 'LightBend')),
    'SideR': ((-1.6, 1.3, 1.1), (-.19, 0, 1.27), .6, ('ArmsDown', 'HandToChest', 'TorsoTwist', 'LightBend')),
}


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
    names = []
    for p in POSES:
        pose(p)
        for s, (eye, target, scale, only) in SHOTS.items():
            if only is None or p in only:
                shot(cam, f'{tag}_{p}_{s}', eye, target, scale); names.append(f'{p}_{s}')
    clear_pose()
    shot(cam, f'{tag}_Rest_Front', (0, -6, 1.0), (0, 0, .95), 2.1); names.append('Rest_Front')
    return names


def compare_images(names):
    """Compare_<pose>_<shot>.png: Before (left) and After (right) of the same pose, camera and light, with a 6 px divider."""
    import numpy as np
    for n in names:
        a = bpy.data.images.load(str(PREV / f'Before_{n}.png')); b = bpy.data.images.load(str(PREV / f'After_{n}.png'))
        w, h = a.size
        pa_ = np.array(a.pixels[:]).reshape(h, w, 4); pb_ = np.array(b.pixels[:]).reshape(h, w, 4)
        gap = np.ones((h, 6, 4)); gap[..., :3] = .1
        out = np.concatenate([pa_, gap, pb_], axis=1)
        img = bpy.data.images.new('cmp_' + n, out.shape[1], h, alpha=True)
        img.pixels = out.ravel().tolist()
        img.filepath_raw = str(PREV / f'Compare_{n}.png'); img.file_format = 'PNG'; img.save()
        for x in (a, b, img):
            bpy.data.images.remove(x)


# ---------- contract (what must not change) ----------

def snapshot():
    return {'objects': {o.name: ([list(r) for r in o.matrix_local], o.parent.name if o.parent else None)
                        for o in bpy.data.objects if o.type in {'EMPTY', 'MESH', 'ARMATURE'} and (o.name.startswith(('Attach_', 'Grip_', 'ProtectiveClothing_', 'PC_Armature')) or o.name.endswith(('_Worn', '_Pickup')))},
            'mesh_names': {o.name: o.data.name for o in bpy.data.objects if o.type == 'MESH'},
            'bones': [(b.name, b.parent.name if b.parent else None, [list(r) for r in b.matrix_local]) for b in pa.data.bones],
            'others': {o.name: ([v.co.copy() for v in o.data.vertices], [sorted((o.vertex_groups[g.group].name, round(g.weight, 6)) for g in v.groups) for v in o.data.vertices])
                       for o in bpy.data.objects if o.type == 'MESH' and o.name != 'Jacket_Worn'},
            'jacket_topology': (len(jo.data.vertices), [tuple(p.vertices) for p in jo.data.polygons],
                                [tuple(round(x, 6) for x in l.uv) for l in jo.data.uv_layers.active.data], [s.material.name for s in jo.material_slots]),
            'collections': {c.name: (c.hide_viewport, c.hide_render, len(c.objects)) for c in bpy.data.collections}}


def contract_problems(before):
    now = snapshot(); problems = []
    for n, (m, parent) in before['objects'].items():
        if n not in now['objects']:
            problems.append(n + ' missing'); continue
        m2, parent2 = now['objects'][n]
        if parent2 != parent:
            problems.append(n + ' parent')
        if max(abs(m2[i][j] - m[i][j]) for i in range(4) for j in range(4)) > 0:
            problems.append(n + ' matrix')
    problems += [n + ' added' for n in now['objects'] if n not in before['objects']]
    problems += [n + ' mesh renamed' for n, mn in before['mesh_names'].items() if now['mesh_names'].get(n) != mn]
    if now['bones'] != before['bones']:
        problems.append('PC_Armature bones (names, parents or rest) changed')
    for n, (co, w) in before['others'].items():
        co2, w2 = now['others'][n]
        if any((a - b).length > 0 for a, b in zip(co, co2)) or w != w2:
            problems.append(n + ' changed')
    if now['jacket_topology'] != before['jacket_topology']:
        problems.append('Jacket_Worn topology, UV or material changed')
    return problems


# ---------- run ----------

if MODE == 'final' and (S / 'JacketFinalDeliveryManifest.json').exists():
    raise RuntimeError('h5 delivery already exists. This historical h4 final command would overwrite it. Reproduce only in a separate preserved h4 workspace.')

load()
previous_fix()
if MODE == 'diag':
    metrics('previous', detail=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT.with_suffix('.blend')), copy=True)
elif MODE == 'metrics':
    metrics('previous')
    before_co = [v.co.copy() for v in jo.data.vertices]
    fix_jacket2()
    report['rest_shape_change'] = rest_shape_change(before_co)
    metrics('after', detail=len(ARGS) > 3)
    if len(ARGS) > 3:
        bpy.ops.wm.save_as_mainfile(filepath=str(OUT.with_suffix('.blend')), copy=True)
elif MODE == 'final':
    base = snapshot()
    metrics('previous', detail=True)
    shots = renders('Before')
    before_co = [v.co.copy() for v in jo.data.vertices]
    fix_jacket2()
    report['rest_shape_change'] = rest_shape_change(before_co)
    metrics('after', detail=True)
    renders('After')
    compare_images(shots)
    report['renders'] = {'folder': str(PREV.relative_to(S)), 'shots': shots}
    problems = contract_problems(base)
    clear_pose()
    report['contract'] = {'objects_checked': len(base['objects']), 'problems': problems,
                          'jacket_rest_skin_error_m': max((a - b.co).length for a, b in zip(evaluated(jo)[0], jo.data.vertices)),
                          'bones': len(pa.data.bones)}
    assert not problems, problems
    rendered = ([v.co.copy() for v in jo.data.vertices], [sorted((jo.vertex_groups[g.group].name, round(g.weight, 6)) for g in v.groups) for v in jo.data.vertices])

    # The renders changed the reference body's material slots and the render settings: save from a clean state instead
    # (reopen the input, the previous fix and this fix again; the result must be the rendered jacket).
    load()
    previous_fix()
    prev_jacket = ([jo.matrix_world @ v.co for v in jo.data.vertices],
                   [{short(jo.vertex_groups[g.group].name): g.weight for g in v.groups if g.weight > 1e-4} for v in jo.data.vertices])
    fix_jacket2()
    again = ([v.co.copy() for v in jo.data.vertices], [sorted((jo.vertex_groups[g.group].name, round(g.weight, 6)) for g in v.groups) for v in jo.data.vertices])
    assert all((a - b).length == 0 for a, b in zip(again[0], rendered[0])) and again[1] == rendered[1], 'rebuild differs from the rendered jacket'
    clear_pose()
    pc.hide_viewport = True; pc.hide_render = True
    bpy.context.preferences.filepaths.save_version = 0
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

    # Re-import the new worn FBX (and, for the record, the earlier one and the unchanged pickup FBX) and compare with the source.
    expected = {o.name: (o.parent.name if o.parent else None, o.matrix_world.copy()) for o in wc.objects}
    clear_pose()
    rest_src = {p: evaluated(bpy.data.objects[p + '_Worn'])[0] for p in PARTS}
    posed_src = {}
    for name in POSES:
        clear_pose(); POSES[name](pa)
        posed_src[name] = {p: evaluated(bpy.data.objects[p + '_Worn'])[0] for p in PARTS}
    for pb in pa.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bone_heads = {b.name: pa.matrix_world @ b.head_local for b in pa.data.bones}
    weights_src = {p: [{short(bpy.data.objects[p + '_Worn'].vertex_groups[g.group].name): g.weight for g in v.groups if g.weight > 1e-4}
                       for v in bpy.data.objects[p + '_Worn'].data.vertices] for p in PARTS}
    bpy.ops.wm.read_homefile(use_empty=True)
    sc = bpy.context.scene; vl = bpy.context.view_layer

    def import_into(name, path):
        coll = bpy.data.collections.new(name); sc.collection.children.link(coll)
        vl.active_layer_collection = vl.layer_collection.children[name]
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=str(path), use_custom_props=True, ignore_leaf_bones=True, automatic_bone_orientation=False)
        return {o.name.split('.')[0]: o for o in bpy.data.objects if o not in before}

    def kd_of(points):
        kd = KDTree(len(points))
        for i, v in enumerate(points):
            kd.insert(v, i)
        kd.balance()
        return kd

    def weight_difference(o, ws, pair):
        worst = 0.0
        for v, (_, j, _) in zip(o.data.vertices, pair):
            wi = {short(o.vertex_groups[g.group].name): g.weight for g in v.groups if g.weight > 1e-4}
            worst = max([worst] + [abs(wi.get(k, 0) - ws[j].get(k, 0)) for k in set(wi) | set(ws[j])])
        return worst

    old = import_into('Earlier_Worn_FBX', OLD_FBX)
    kd = kd_of(prev_jacket[0])
    pair = [kd.find(old['Jacket_Worn'].matrix_world @ v.co) for v in old['Jacket_Worn'].data.vertices]
    report['previous_rebuild_vs_earlier_fbx'] = {
        'jacket_max_rest_vertex_difference_m': max(d for _, _, d in pair),
        'jacket_max_weight_difference': round(weight_difference(old['Jacket_Worn'], prev_jacket[1], pair), 5),
        'other_parts_max_rest_vertex_difference_m': max(max(kd_of(rest_src[p]).find(old[p + '_Worn'].matrix_world @ v.co)[2] for v in old[p + '_Worn'].data.vertices) for p in PARTS if p != 'Jacket')}
    bpy.data.collections.remove(bpy.data.collections['Earlier_Worn_FBX'])
    for o in list(old.values()):
        bpy.data.objects.remove(o, do_unlink=True)

    imp = import_into('Reimport_Worn', P / 'ProtectiveClothing_Worn.fbx')
    ia = imp['PC_Armature']
    mat = max(max(abs(imp[n].matrix_world[i][j] - m[i][j]) for i in range(4) for j in range(4)) for n, (_, m) in expected.items() if n in imp and imp[n].type != 'ARMATURE')
    bone = max((ia.matrix_world @ ia.data.bones[n].head_local - h).length for n, h in bone_heads.items() if n in ia.data.bones)
    worst_rest = 0.0; worst_w = 0.0; mod_ok = True; pairs = {}
    for p in PARTS:
        o = imp[p + '_Worn']
        mod_ok &= any(m.type == 'ARMATURE' and m.object == ia for m in o.modifiers)
        pairs[p] = [kd_of(rest_src[p]).find(o.matrix_world @ v.co) for v in o.data.vertices]
        worst_rest = max(worst_rest, max(d for _, _, d in pairs[p]))
        worst_w = max(worst_w, weight_difference(o, weights_src[p], pairs[p]))
    pa = ia
    worst_pose = {}
    for name in POSES:
        for pb in ia.pose.bones:
            pb.matrix_basis = Matrix.Identity(4)
        vl.update(); POSES[name](ia)
        worst_pose[name] = max(max((q - posed_src[name][p][j]).length for q, (_, j, _) in zip(evaluated(imp[p + '_Worn'])[0], pairs[p])) for p in PARTS)
    for pb in ia.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    vl.update()
    report['roundtrip'] = {'objects': len(imp), 'missing': [n for n in expected if n not in imp], 'max_matrix_difference': mat,
                           'max_bone_head_difference_m': bone, 'max_rest_vertex_difference_m': worst_rest,
                           'max_weight_difference': round(worst_w, 5), 'max_posed_vertex_difference_m': worst_pose,
                           'armature_modifiers_to_PC_Armature': mod_ok, 'bones_imported': len(ia.data.bones)}
    pick = import_into('Reimport_Pickup', P / 'ProtectiveClothing_Pickup.fbx')
    report['roundtrip']['pickup_fbx'] = f'not re-exported (unchanged); re-imported {len(pick)} objects for review'
    bpy.ops.wm.save_as_mainfile(filepath=str(S / 'FBX_Roundtrip_Review.blend'))

report['poses'] = POSE_TEXT
report['seconds'] = round(time.time() - T0, 1)
OUT.write_text(json.dumps(report, indent=1, default=str), encoding='utf8')
print('JDF_DONE', MODE, OUT)
