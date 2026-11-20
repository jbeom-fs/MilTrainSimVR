"""Read-only h4/final shader, image link, packed texture and visibility comparison."""
import bpy,json,hashlib
from pathlib import Path
S=Path(__file__).resolve().parent
def snapshot(path):
    bpy.ops.wm.open_mainfile(filepath=str(path))
    mats={}
    for m in bpy.data.materials:
        nodes=[];links=[]
        if m.node_tree:
            for n in m.node_tree.nodes:
                inputs=[]
                for sock in n.inputs:
                    if hasattr(sock,'default_value'):
                        v=sock.default_value
                        try:v=list(v)
                        except TypeError:pass
                        if isinstance(v,(str,float,int,bool,list)):inputs.append((sock.identifier,v))
                nodes.append({'name':n.name,'type':n.type,'inputs':inputs,'image':n.image.name if n.type=='TEX_IMAGE' and n.image else None})
            links=[(l.from_node.name,l.from_socket.identifier,l.to_node.name,l.to_socket.identifier) for l in m.node_tree.links]
        mats[m.name]={'nodes':nodes,'links':links}
    images={i.name:{'path':i.filepath,'source':i.source,'packed_sha256':hashlib.sha256(bytes(i.packed_file.data)).hexdigest() if i.packed_file else None} for i in bpy.data.images if i.type!='RENDER_RESULT'}
    visibility={o.name:(o.hide_render,o.hide_viewport,o.hide_get()) for o in bpy.data.objects if o.type=='MESH'}
    return {'materials':mats,'images':images,'mesh_visibility':visibility}
h4=snapshot(S/'Backups/20261104_145838_h4_review_before/ProtectiveClothing.blend')
final=snapshot(S/'ProtectiveClothing.blend')
out={'materials_identical':h4['materials']==final['materials'],'images_identical':h4['images']==final['images'],
    'mesh_visibility_identical':h4['mesh_visibility']==final['mesh_visibility'],'final':final}
out['passed']=all(out[k] for k in ['materials_identical','images_identical','mesh_visibility_identical'])
(S/'JacketMaterialAudit.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf8')
print('MATERIAL_AUDIT',out['passed'])
