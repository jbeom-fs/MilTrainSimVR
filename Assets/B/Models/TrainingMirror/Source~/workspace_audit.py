"""Read-only inventory outside the authorized delivery directory."""
import hashlib, json, pathlib, subprocess, sys
ROOT = pathlib.Path(__file__).resolve().parents[5]
OUT = pathlib.Path(__file__).resolve().parent
DELIVERY = 'Assets/B/Models/TrainingMirror/'
def inventory():
    paths = subprocess.check_output(['git', 'ls-files', '-z', '--cached', '--others', '--exclude-standard'], cwd=ROOT).decode().split('\0')
    result = {}
    for rel in sorted(set(paths)):
        if not rel or rel.startswith(DELIVERY): continue
        p = ROOT / rel
        if p.is_file():
            s = p.stat()
            result[rel] = [s.st_size, s.st_mtime_ns]
    return result
if sys.argv[1] == 'before':
    data = {'files': inventory(), 'status': subprocess.check_output(['git','-c','core.quotepath=false','status','--short'],cwd=ROOT).decode()}
    (OUT/'workspace_before.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Baseline files:', len(data['files']))
else:
    before=json.loads((OUT/'workspace_before.json').read_text(encoding='utf-8'))
    after=inventory()
    changes=[p for p in sorted(set(before['files'])|set(after)) if before['files'].get(p)!=after.get(p)]
    result={'outside_delivery_changed_paths':changes,'baseline_files':len(before['files']),'after_files':len(after),'method':'git tracked + nonignored untracked inventory; file size and mtime_ns; no Unity execution'}
    (OUT/'workspace_after.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False))
    assert not changes
