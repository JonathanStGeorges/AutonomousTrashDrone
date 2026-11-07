"""Run only in a coordinated idle Unity/player window. Sequential real HTTP inference."""
import argparse
import base64
import json
from pathlib import Path
import time
import urllib.request
import psutil
from PIL import Image

p=argparse.ArgumentParser();p.add_argument('frame',type=Path);p.add_argument('--count',type=int,default=200);p.add_argument('--output',type=Path,required=True);args=p.parse_args()
with urllib.request.urlopen('http://127.0.0.1:8765/health',timeout=5) as response:health=json.load(response)
assert health['service']=='TrashSailInference'
process=psutil.Process(health['pid']);width,height=Image.open(args.frame).size
request=dict(sessionId='memory-benchmark',cameraId='onboard',profile='combined',captureTime=0,width=width,height=height,imageBase64=base64.b64encode(args.frame.read_bytes()).decode())
samples=[];first=None;identical=True
def memory(index):
    info=process.memory_info()
    sample=dict(requests=index,rssBytes=info.rss,privateBytes=getattr(info,'private',None),vmsBytes=info.vms,threads=process.num_threads())
    samples.append(sample);print(json.dumps(sample),flush=True)
memory(0);started=time.perf_counter();times=[]
for index in range(args.count):
    request['sequence']=index+1
    wire=urllib.request.Request('http://127.0.0.1:8765/infer',data=json.dumps(request).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(wire,timeout=20) as response:result=json.load(response)
    assert result['ok'] and result['sequence']==index+1 and result['sessionId']=='memory-benchmark'
    current=dict(detections=result['detections'],imageSha256=result['imageSha256'])
    if first is None:first=current
    else:identical &= first==current
    times.append(result['wallInferenceMs'])
    if (index+1)%25==0 or index+1==args.count:memory(index+1)
report=dict(pid=process.pid,frame=str(args.frame.resolve()),requests=args.count,sameNumericDetectionsEveryRequest=identical,elapsedSeconds=time.perf_counter()-started,samples=samples,inferenceMs=times,firstResult=first)
args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,indent=2))
print(json.dumps(dict(completed=args.count,identical=identical,elapsed=report['elapsedSeconds'])),flush=True)
