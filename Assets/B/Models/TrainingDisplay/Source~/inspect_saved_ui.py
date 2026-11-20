"""Read-only Unity YAML analysis; does not load or execute Unity."""
import re,json,pathlib,yaml,math,hashlib
SRC=pathlib.Path(__file__).resolve().parent;ROOT=SRC.parents[4]
def vec(v,keys='xy'):return [float(v[k]) for k in keys]
def parse(path):
    text=path.read_text(encoding='utf-8-sig');docs={};lines={}
    headers=list(re.finditer(r'^--- !u!(\d+) &(-?\d+).*$',text,re.M))
    for i,m in enumerate(headers):
        typ=int(m[1]);fid=int(m[2])
        if typ not in (1,4,224,223,114):continue
        body=text[m.end():headers[i+1].start() if i+1<len(headers) else len(text)]
        if not body.strip():continue
        v=next(iter(yaml.safe_load(body).values()));v['_type']=typ
        docs[fid]=v;lines[fid]=text.count('\n',0,m.start())+1
    transforms={v['m_GameObject']['fileID']:fid for fid,v in docs.items() if v['_type'] in (4,224) and 'm_GameObject'in v}
    def name(t):return docs[docs[t]['m_GameObject']['fileID']]['m_Name']
    def pathof(t):
        p=docs[t].get('m_Father',{}).get('fileID',0)
        return (pathof(p)+'/' if p in docs else '')+name(t)
    def children(t):return [c['fileID'] for c in docs[t].get('m_Children',[]) if c['fileID'] in docs]
    canvases=[]
    for fid,v in docs.items():
        if v['_type']!=223:continue
        go=v['m_GameObject']['fileID'];t=transforms[go];r=docs[t]
        canvases.append({'name':name(t),'path':pathof(t),'rect_fileID':t,'line':lines[t],'size':r.get('m_SizeDelta'),'pivot':r.get('m_Pivot'),'scale':r.get('m_LocalScale')})
    wanted=['TQ_StatusBoard','PB_StatusBoard','K1_GuideBoard','PC_TutorialBoard','KD1_CourseBoard','EntranceBoard']
    boards=[]
    for c in canvases:
        if c['name'] not in wanted and not(c['name']=='Canvas' and c.get('size')=={'x':720,'y':460}):continue
        t=c['rect_fileID'];base=vec(docs[t]['m_SizeDelta']);elements=[]
        def walk(t,psize,ppivot,pos=(0,0),sc=(1,1),relative=''):
            for ch in children(t):
                r=docs[ch]
                if r['_type']!=224:continue
                lo=vec(r['m_AnchorMin']);hi=vec(r['m_AnchorMax']);pivot=vec(r['m_Pivot']);delta=vec(r['m_SizeDelta']);anchor=vec(r['m_AnchoredPosition'])
                sz=[psize[j]*(hi[j]-lo[j])+delta[j] for j in range(2)]
                cp=[pos[j]+sc[j]*(anchor[j]+psize[j]*(lo[j]+(hi[j]-lo[j])*pivot[j]-ppivot[j])) for j in range(2)]
                cs=[sc[j]*vec(r['m_LocalScale'])[j] for j in range(2)]
                mn=[cp[j]-cs[j]*sz[j]*pivot[j] for j in range(2)];mx=[cp[j]+cs[j]*sz[j]*(1-pivot[j]) for j in range(2)]
                rel=(relative+'/' if relative else '')+name(ch)
                go=docs[r['m_GameObject']['fileID']]
                elements.append({'path':rel,'rect_fileID':ch,'line':lines[ch],'activeSelf':go.get('m_IsActive'),'bounds_ui_units':[mn,mx],'rotation':r.get('m_LocalRotation'),'outside_canvas':any(mn[j]<-base[j]/2-.5 or mx[j]>base[j]/2+.5 for j in range(2))})
                walk(ch,sz,pivot,cp,cs,rel)
        walk(t,base,vec(docs[t]['m_Pivot']))
        boards.append({**c,'elements':elements})
    return {'scene':str(path.relative_to(ROOT)).replace('\\','/'),'canvases':canvases,'boards':boards}
results=[parse(ROOT/'Assets/B/Scene'/name) for name in ['B_FirstAid.unity','B_CBRN.unity']]
(SRC/'saved_ui_inspection.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
for scene in results:
    print(scene['scene'])
    for c in scene['canvases']:print('CANVAS',c['path'],c['size'],c['pivot'],c['scale'])
    for b in scene['boards']:
        print('BOARD',b['path'],'elements',len(b['elements']))
        for e in b['elements']:
            if e['outside_canvas']:print('OUTSIDE',e['path'],e['activeSelf'],e['bounds_ui_units'])
