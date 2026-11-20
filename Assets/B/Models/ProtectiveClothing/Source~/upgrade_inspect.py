import bpy,bmesh,json,hashlib
from pathlib import Path
from mathutils import Vector
S=Path(__file__).resolve().parent;B=Path((S/'UpgradeBackupPath.txt').read_text())
bpy.ops.wm.open_mainfile(filepath=str(B/'Source~'/'ProtectiveClothing.blend'))
bpy.data.collections['Pickup'].hide_viewport=False;bpy.context.view_layer.update()
def digest(o):
    if o.type=='MESH':return hashlib.sha256(b''.join(__import__('struct').pack('<3f',*v.co) for v in o.data.vertices)).hexdigest()
    if o.type=='ARMATURE':return hashlib.sha256(str([(b.name,b.parent.name if b.parent else None,list(b.head_local),list(b.tail_local)) for b in o.data.bones]).encode()).hexdigest()
    return None
d={'objects':{},'scenes':[s.name for s in bpy.data.scenes],'units':{'system':bpy.context.scene.unit_settings.system,'scale_length':bpy.context.scene.unit_settings.scale_length},'textures':{i.name:{'filepath':i.filepath,'size':list(i.size)} for i in bpy.data.images if i.name.startswith('PC_')}}
for o in bpy.data.objects:
    item={'type':o.type,'parent':o.parent.name if o.parent else None,'matrix_local':[list(r) for r in o.matrix_local],'matrix_world':[list(r) for r in o.matrix_world],'geometry_digest':digest(o),'collections':[c.name for c in o.users_collection]}
    if o.type=='MESH':
        item['mesh_name']=o.data.name
        item['bounds_local']=[[min(v.co[k] for v in o.data.vertices),max(v.co[k] for v in o.data.vertices)] for k in range(3)];item['triangles']=sum(len(p.vertices)-2 for p in o.data.polygons);item['materials']=[m.name for m in o.data.materials]
        if o.name in ['Jacket_Worn','Trousers_Worn']:
            bm=bmesh.new();bm.from_mesh(o.data);remaining=set(bm.verts);parts=[]
            while remaining:
                v=remaining.pop();part={v};q=[v]
                while q:
                    for e in q.pop().link_edges:
                        t=e.other_vert(v) if False else None
                        for t in e.verts:
                            if t in remaining:remaining.remove(t);part.add(t);q.append(t)
                parts.append({'verts':len(part),'index_range':[min(v.index for v in part),max(v.index for v in part)],'bounds':[[min(v.co[k] for v in part),max(v.co[k] for v in part)] for k in range(3)]})
            item['components']=parts;bm.free()
    d['objects'][o.name]=item
body=bpy.data.objects['Ch36'];dg=bpy.context.evaluated_depsgraph_get();ev=body.evaluated_get(dg);me=ev.to_mesh()
d['hand_bones']={b.name:{'head':list(bpy.data.objects['Armature'].matrix_world@b.head_local),'tail':list(bpy.data.objects['Armature'].matrix_world@b.tail_local)} for b in bpy.data.objects['Armature'].data.bones if 'Hand' in b.name}
d['hand_vertices']={side:sum(1 for v in me.vertices if sign*(ev.matrix_world@v.co).x>.642) for sign,side in [(1,'L'),(-1,'R')]}
ev.to_mesh_clear();(S/'UpgradeBaseline.json').write_text(json.dumps(d,indent=2))
print(json.dumps({'object_count':len(d['objects']),'hands':d['hand_vertices'],'main_mesh_components':{n:d['objects'][n]['components'] for n in ['Jacket_Worn','Trousers_Worn']}}))
