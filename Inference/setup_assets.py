"""Verify the selected interpreter/lock and recover missing pinned assets only."""
import argparse
import hashlib
import importlib.metadata
import json
from pathlib import Path
import sys
import urllib.request

HASHES={'yoloe-26s-seg.pt':'48f24206bc8680d60cbbfa296b0140da849669b9515058b72f5a945142df0654','mobileclip2_b.ts':'35d7f213e4d75f38514e4656ad3cb91158bd33e3805d8ac349f23b186f66982f','yolov8m_marine_best.pt':'f5afba16fcb65ad8c74512211b9ae023a878538e2015bc3e10fd00ba8823e037'}
REVISION='a77dc9ce0e8ce6640b0c3800ec03cde9daa39fd3'

def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()

def verify(path):
    actual=digest(path)
    if actual!=HASHES[path.name]:raise RuntimeError('Hash mismatch; existing file preserved: '+str(path))
    return dict(path=str(path),sha256=actual,size=path.stat().st_size)

def main():
    p=argparse.ArgumentParser();p.add_argument('--assets',type=Path,default=Path.home()/'Downloads/marineCam');args=p.parse_args()
    root=Path(__file__).resolve().parent
    failures=[];versions={}
    for line in (root/'environment.lock.txt').read_text(encoding='utf-8-sig').splitlines():
        if '==' not in line:continue
        name,expected=line.strip().split('==',1)
        try:actual=importlib.metadata.version(name)
        except importlib.metadata.PackageNotFoundError:actual='MISSING'
        versions[name]=actual
        if actual.split('+')[0]!=expected.split('+')[0]:failures.append(f'{name}: expected {expected}, found {actual}')
    clip=importlib.metadata.distribution('clip')
    direct=json.loads(clip.read_text('direct_url.json') or '{}')
    expected_clip='23eba4598a4af7aead8e473591c43a1b04022fb2e5a7af13442cf3b23d8a7a2f'
    if direct.get('archive_info',{}).get('hashes',{}).get('sha256')!=expected_clip:failures.append('CLIP archive SHA256 differs from tested lock')
    if failures:raise RuntimeError('Environment was not changed. Select tested Python or explicitly create an isolated environment:\n'+'\n'.join(failures))
    args.assets.mkdir(parents=True,exist_ok=True);records=[]
    for name in ('yoloe-26s-seg.pt','mobileclip2_b.ts'):
        path=args.assets/name
        if not path.exists():
            url='https://github.com/ultralytics/assets/releases/download/v8.4.0/'+name
            temporary=path.with_suffix(path.suffix+'.download')
            print('Downloading pinned official asset '+url,flush=True)
            urllib.request.urlretrieve(url,temporary)
            if digest(temporary)!=HASHES[name]:raise RuntimeError('Downloaded asset hash mismatch: '+name)
            temporary.replace(path)
        records.append(verify(path))
    from huggingface_hub import hf_hub_download
    params=dict(repo_id='Krishna-Jaiswal/yolov8m-marine-trash',filename='yolov8m_marine_best.pt',revision=REVISION)
    try:path=Path(hf_hub_download(**params,local_files_only=True))
    except Exception:path=Path(hf_hub_download(**params))
    records.append(verify(path))
    report=dict(ok=True,python=sys.executable,pythonVersion=sys.version,versions=versions,clipSource=direct,revision=REVISION,assets=records)
    (root/'setup-report.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(dict(ok=True,python=sys.executable,verifiedPackages=len(versions),verifiedAssets=len(records))))

if __name__=='__main__':main()
