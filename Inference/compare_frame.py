"""Compare direct and service predictions on one unannotated saved Unity RGB PNG."""
import argparse
import base64
import json
from pathlib import Path
import time
import urllib.request
from service import Engine

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('frame',type=Path)
    parser.add_argument('--assets',default=str(Path.home()/'Downloads/marineCam'))
    parser.add_argument('--output',type=Path,default=Path(__file__).parent/'comparison.json')
    args=parser.parse_args()
    frame=args.frame.resolve();output=args.output.resolve()
    engine=Engine(args.assets)
    image=engine.cv2.imread(str(frame));h,w=image.shape[:2]
    request=dict(sessionId='direct-service-comparison',cameraId='onboard',sequence=1,captureTime=0,width=w,height=h,profile='combined',imageBase64=base64.b64encode(frame.read_bytes()).decode())
    direct=engine.infer(request)
    wire=urllib.request.Request('http://127.0.0.1:8765/infer',data=json.dumps(request).encode(),headers={'Content-Type':'application/json'})
    with urllib.request.urlopen(wire,timeout=60) as response: service=json.load(response)
    match=direct['detections']==service['detections'] and direct['imageSha256']==service['imageSha256']
    result=dict(frame=str(frame),sameDetections=match,direct=direct,service=service)
    output.write_text(json.dumps(result,indent=2),encoding='utf8')
    print(json.dumps(dict(match=match,detections=len(direct['detections']),directMs=direct['wallInferenceMs'],serviceMs=service['wallInferenceMs'])))
    raise SystemExit(0 if match else 1)

if __name__=='__main__':main()
