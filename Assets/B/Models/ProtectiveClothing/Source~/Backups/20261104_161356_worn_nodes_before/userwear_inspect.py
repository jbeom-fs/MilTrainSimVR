"""Read-only inspection before the user-wear change: objects, armature, Jacket islands classified by UV region."""
import bpy, bmesh, json, sys
from pathlib import Path
from mathutils import Vector

S = Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(S / 'ProtectiveClothing.blend'))
out = {'objects': {}, 'collections': {c.name: [o.name for o in c.objects] for c in bpy.data.collections}}
for o in bpy.data.objects:
    out['objects'][o.name] = {'type': o.type, 'parent': o.parent.name if o.parent else None,
                              'parent_type': o.parent_type, 'parent_bone': o.parent_bone,
                              'loc': list(o.matrix_world.translation), 'scale': list(o.matrix_world.to_scale()),
                              'modifiers': [(m.type, getattr(m, 'object', None) and m.object.name) for m in o.modifiers],
                              'vgroups': len(o.vertex_groups)}
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE']
out['armatures'] = {a.name: {'bones': len(a.data.bones), 'pose_position': a.data.pose_position,
                             'matrix_world': [list(r) for r in a.matrix_world],
                             'first': [b.name for b in a.data.bones][:6]} for a in arm}


def region(u, v):
    if u >= .785 and .275 <= v <= .475:
        return 'olive'
    if u >= .785 and .555 <= v:
        return 'rubber'
    if u >= .795 and v <= .21:
        return 'thread'
    return 'camo'


def islands(name):
    o = bpy.data.objects[name]
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    uv = bm.loops.layers.uv.active
    seen = set(); res = []
    for f in bm.faces:
        if f.index in seen:
            continue
        stack = [f]; seen.add(f.index); faces = []
        while stack:
            g = stack.pop(); faces.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in seen:
                        seen.add(h.index); stack.append(h)
        regs = {}
        for g in faces:
            c = sum((l[uv].uv for l in g.loops), Vector((0, 0))) / len(g.loops)
            r = region(c.x, c.y); regs[r] = regs.get(r, 0) + 1
        vs = {v for g in faces for v in g.verts}
        lo = [min(v.co[a] for v in vs) for a in range(3)]; hi = [max(v.co[a] for v in vs) for a in range(3)]
        res.append({'faces': len(faces), 'regions': regs, 'min': [round(x, 4) for x in lo], 'max': [round(x, 4) for x in hi]})
    bm.free()
    return sorted(res, key=lambda r: -r['faces'])


out['Jacket_Worn_islands'] = islands('Jacket_Worn')
out['Jacket_Pickup_islands'] = islands('Jacket_Pickup')
body = bpy.data.objects.get('Ch36')
if body:
    out['Ch36'] = {'vgroups': [g.name for g in body.vertex_groups][:80], 'n_vgroups': len(body.vertex_groups),
                   'modifiers': [(m.type, m.object.name if getattr(m, 'object', None) else None) for m in body.modifiers]}
(Path(sys.argv[-1])).write_text(json.dumps(out, indent=1))
print('INSPECT_DONE')
