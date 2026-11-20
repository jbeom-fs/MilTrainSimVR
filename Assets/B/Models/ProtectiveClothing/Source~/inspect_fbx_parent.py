import bpy,json
from pathlib import Path
from io_scene_fbx import parse_fbx
S=Path(__file__).resolve().parent
tree,ver=parse_fbx.parse(str(S.parent/'ProtectiveClothing_Worn.fbx'))
obj=next(x for x in tree.elems if x.id==b'Objects')
names={x.props[0]:x.props[1].decode(errors='replace').split('\x00')[0] for x in obj.elems if x.id==b'Model'}
connections=next(x for x in tree.elems if x.id==b'Connections')
out={names[x.props[1]]:names.get(x.props[2],str(x.props[2])) for x in connections.elems if x.props[0]==b'OO' and x.props[1] in names and x.props[2] in names}
print('RAW_FBX_PARENTS',json.dumps(out))
bpy.ops.wm.open_mainfile(filepath=str(S/'ProtectiveClothing.blend'))
print('SOURCE',[(o.name,o.parent.name if o.parent else None,o.parent_type,tuple(o.scale)) for o in bpy.data.collections['Worn'].objects])
bpy.ops.wm.open_mainfile(filepath=str(S/'FBX_Roundtrip_Review.blend'))
print('IMPORTED',[(o.name,o.parent.name if o.parent else None,o.parent_type,tuple(o.scale)) for o in bpy.data.collections['Reimport_Worn'].objects])
(S/'FBXRawParentAudit.json').write_text(json.dumps(out,indent=2),encoding='utf8')
