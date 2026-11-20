import pathlib,subprocess,json,sys,hashlib
SRC=pathlib.Path(__file__).resolve().parent
ROOT=SRC.parents[4]
ALLOWED='Assets/B/Models/TrainingDisplay/'
META='Assets/B/Models/TrainingDisplay.meta'
def inventory():
    names=subprocess.check_output(['git','ls-files','-z','--cached','--others','--exclude-standard'],cwd=ROOT).decode().split('\0')
    data={}
    for rel in sorted(set(names)):
        if not rel or rel.startswith(ALLOWED) or rel==META:continue
        p=ROOT/rel
        if p.is_file():
            s=p.stat();data[rel]=[s.st_size,s.st_mtime_ns]
    return data
if sys.argv[1]=='before':
    assert not (SRC/'workspace_before.json').exists(),'Preserve the original baseline'
    result={'files':inventory(),'status':subprocess.check_output(['git','-c','core.quotepath=false','status','--short'],cwd=ROOT).decode()}
    (SRC/'workspace_before.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Baseline existing files:',len(result['files']))
else:
    old=json.loads((SRC/'workspace_before.json').read_text(encoding='utf-8'))['files'];new=inventory()
    changes=[p for p in sorted(set(old)|set(new)) if old.get(p)!=new.get(p)]
    result={'baseline_count':len(old),'after_count':len(new),'outside_authorized_paths_changed':changes,'method':'git tracked/nonignored untracked file size + nanosecond modification time; excludes authorized TrainingDisplay folder and its sibling .meta'}
    (SRC/'workspace_after.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False));assert not changes
