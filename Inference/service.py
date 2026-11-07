"""Loopback-only same-frame YOLOE and marine specialist inference. No webcam."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import threading
import time
from http.server import BaseHTTPRequestHandler, HTTPServer

REVISION = 'a77dc9ce0e8ce6640b0c3800ec03cde9daa39fd3'
PROMPTS = ['plastic bottle', 'plastic bag', 'drink can', 'food container', 'paper cup', 'cardboard', 'fishing net', 'rope', 'styrofoam', 'fish', 'sea turtle', 'jellyfish', 'dolphin', 'shark', 'crab', 'bird', 'boat', 'person', 'buoy']
ELIGIBLE = set(PROMPTS[:4])
WILDLIFE = set(PROMPTS[9:16])

def category(model, label):
    if label in WILDLIFE or label.startswith('animal_'): return 'Wildlife', False
    if model == 'YOLOE' and label in ELIGIBLE: return 'CollectableRubbish', True
    if label.startswith('trash_') or label in PROMPTS[:9]: return 'OtherRubbish', False
    if label in ('boat', 'person', 'buoy', 'rov'): return 'Obstacle', False
    return 'Unknown', False

def overlap(a, b):
    intersection = max(0, min(a[2], b[2])-max(a[0], b[0])) * max(0, min(a[3], b[3])-max(a[1], b[1]))
    smaller = min(max(0, a[2]-a[0])*max(0, a[3]-a[1]), max(0, b[2]-b[0])*max(0, b[3]-b[1]))
    return intersection / smaller if smaller else 0

class Engine:
    def __init__(self, assets, device='cpu', imgsz=416):
        import cv2
        import numpy as np
        import torch
        import ultralytics
        from ultralytics import YOLO, YOLOE
        from huggingface_hub import hf_hub_download
        self.cv2, self.np, self.device, self.imgsz = cv2, np, device, imgsz
        self.lock = threading.Lock()
        assets = Path(assets).resolve()
        os.chdir(assets)  # Ultralytics text encoder cache; existing file is preserved.
        yolo = assets / 'yoloe-26s-seg.pt'
        specialist = Path(hf_hub_download('Krishna-Jaiswal/yolov8m-marine-trash', 'yolov8m_marine_best.pt', revision=REVISION, local_files_only=True))
        self.yolo = YOLOE(str(yolo))
        self.yolo.set_classes(PROMPTS)
        self.specialist = YOLO(str(specialist))
        self.manifest = {'device': device, 'imgsz': imgsz, 'torch': torch.__version__, 'ultralytics': ultralytics.__version__, 'opencv': cv2.__version__, 'revision': REVISION,
                         'classes': {'YOLOE': self.yolo.names, 'TRASH': self.specialist.names},
                         'weights': {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in (yolo, specialist, assets/'mobileclip2_b.ts')}}
        expected = {'yoloe-26s-seg.pt': '48f24206bc8680d60cbbfa296b0140da849669b9515058b72f5a945142df0654', 'mobileclip2_b.ts': '35d7f213e4d75f38514e4656ad3cb91158bd33e3805d8ac349f23b186f66982f', 'yolov8m_marine_best.pt': 'f5afba16fcb65ad8c74512211b9ae023a878538e2015bc3e10fd00ba8823e037'}
        for p, digest in self.manifest['weights'].items():
            if Path(p).name in expected and digest != expected[Path(p).name]: raise ValueError('Asset SHA256 mismatch: '+p)
        self.predict(np.zeros((256,416,3), dtype=np.uint8), 'combined')

    def predict(self, image, profile):
        if profile not in ('combined', 'YOLOE', 'TRASH'): raise ValueError('Unsupported active profile')
        detections = []
        models = [('YOLOE', self.yolo, .25), ('TRASH', self.specialist, .30)]
        for name, model, threshold in models:
            if profile != 'combined' and profile != name: continue
            result = model.predict(source=image, device=self.device, imgsz=self.imgsz, conf=threshold, verbose=False, save=False)[0]
            if result.boxes is None: continue
            polygons = result.masks.xy if result.masks is not None else []
            for i, (box, score, cls) in enumerate(zip(result.boxes.xyxy.cpu().tolist(), result.boxes.conf.cpu().tolist(), result.boxes.cls.cpu().tolist())):
                label = str(result.names[int(cls)])
                canonical, eligible = category(name, label)
                detections.append(dict(model=name, classId=int(cls), label=label, score=float(score), box=box,
                    polygon=polygons[i].flatten().tolist() if i<len(polygons) else [], category=canonical, eligible=eligible, conflict=False))
        for a in detections:
            for b in detections:
                if a is b: continue
                if (a['category']=='Wildlife') != (b['category']=='Wildlife') and overlap(a['box'], b['box']) >= .15:
                    a['conflict']=b['conflict']=True
                    a['eligible']=b['eligible']=False
        return detections

    def infer(self, request):
        started = time.perf_counter()
        png = base64.b64decode(request['imageBase64'], validate=True)
        image = self.cv2.imdecode(self.np.frombuffer(png, dtype=self.np.uint8), self.cv2.IMREAD_COLOR)
        if image is None: raise ValueError('PNG decode failed')
        h,w = image.shape[:2]
        if (w,h) != (request['width'],request['height']): raise ValueError('Declared dimensions mismatch')
        if not self.lock.acquire(blocking=False): raise RuntimeError('Inference busy; latest frame only')
        try: detections=self.predict(image, request['profile'])
        finally: self.lock.release()
        result={key:request[key] for key in ('sessionId','cameraId','sequence','captureTime','width','height','profile')}
        result.update(ok=True, empty=not detections, detections=detections, models=['YOLOE','TRASH'] if request['profile']=='combined' else [request['profile']], device=self.device, wallInferenceMs=(time.perf_counter()-started)*1000, wallCompleted=time.time(), imageSha256=hashlib.sha256(png).hexdigest())
        return result

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--assets', default=str(Path.home()/'Downloads/marineCam'))
    parser.add_argument('--device', default='cpu')
    parser.add_argument('--imgsz',type=int,default=416)
    parser.add_argument('--port',type=int,default=8765)
    args=parser.parse_args()
    out=Path(__file__).resolve().parent
    engine=Engine(args.assets,args.device,args.imgsz)
    (out/'loaded-models.json').write_text(json.dumps(engine.manifest,indent=2),encoding='utf8')
    class Handler(BaseHTTPRequestHandler):
        def respond(self, value, status=200):
            data=json.dumps(value).encode()
            self.send_response(status); self.send_header('Content-Type','application/json'); self.send_header('Content-Length',str(len(data))); self.end_headers(); self.wfile.write(data)
        def do_GET(self):
            if self.path=='/health': self.respond(dict(ok=True,service='TrashSailInference',pid=os.getpid(),projectRoot=str(out.parent),**engine.manifest))
            else: self.respond(dict(ok=False,error='Not found'),404)
        def do_POST(self):
            size=int(self.headers.get('Content-Length','0'))
            if size>12000000: self.respond(dict(ok=False,error='Request too large'),413); return
            request={}
            try:
                request=json.loads(self.rfile.read(size))
                if self.path=='/shutdown':
                    if request.get('owner')!='TrashSailDemo' or request.get('pid')!=os.getpid() or request.get('projectRoot')!=str(out.parent): raise ValueError('Matching project lifecycle PID required')
                    self.respond(dict(ok=True)); threading.Thread(target=self.server.shutdown,daemon=True).start(); return
                if self.path!='/infer': self.respond(dict(ok=False,error='Not found'),404); return
                self.respond(engine.infer(request))
            except Exception as exc:
                self.respond(dict(ok=False,empty=False,error=str(exc),**{k:request[k] for k in ('sessionId','cameraId','sequence','captureTime') if k in request}),400)
    # Unity permits only one outstanding frame. Reuse this OS thread for every
    # prediction: per-request threads can retain large native Torch allocator pools.
    # Health requests may wait for the current (~300 ms) inference to complete.
    server=HTTPServer(('127.0.0.1',args.port),Handler)
    print(json.dumps(dict(status='READY',**engine.manifest)),flush=True)
    server.serve_forever()

if __name__=='__main__': main()
