"""Offline tuning experiment only. Never changes the live inference profile."""
import argparse
import hashlib
import json
from pathlib import Path
import statistics
import time
from service import Engine, PROMPTS, category

def extract(result,name):
    if result.boxes is None:return []
    out=[]
    for box,confidence,cls in zip(result.boxes.xyxy.cpu().tolist(),result.boxes.conf.cpu().tolist(),result.boxes.cls.cpu().tolist()):
        label=str(result.names[int(cls)])
        canonical,eligible=category('YOLOE' if name.startswith('YOLOE') else name,label)
        if name.startswith('YOLOE') and label in ('bottle','can','bag','plastic jug','detergent bottle','water bottle','aluminum can','metal can','garbage bag','trash bag','plastic container'):canonical,eligible='CollectableRubbish',True
        if label in ('trash','garbage'):canonical,eligible='OtherRubbish',False
        out.append(dict(model=name,label=label,classId=int(cls),score=confidence,box=box,category=canonical,eligible=eligible))
    return out

def main():
    p=argparse.ArgumentParser();p.add_argument('session',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--no-fml',action='store_true');p.add_argument('--holdout',action='store_true');args=p.parse_args()
    session=args.session.resolve();output=args.output.resolve();output.mkdir(parents=True,exist_ok=True)
    fixtures=json.loads((session/'fixtures.json').read_text())['fixtures'];assert 1<=len(fixtures)<=200
    images={f['fixture']:None for f in fixtures}
    engine=Engine(str(Path.home()/'Downloads/marineCam'))
    for f in fixtures:images[f['fixture']]=engine.cv2.imread(str(session/(f['fixture']+'.png')))
    from ultralytics import YOLO,YOLOE
    fml_path=Path(__file__).resolve().parent/'local-only/yolov8s_fml.pt'
    fml=None if args.no_fml else YOLO(str(fml_path))
    experiment=dict(fixtures=str(session),purpose=('fixed-profile holdout' if args.holdout else 'tuning set')+'; presence counts are not box IoU accuracy',
        fml=None if fml is None else dict(source='https://drive.google.com/file/d/1gXY9K_txdq5PqqGNlP9yNqeY9QwAY9y8/view',sha256=hashlib.sha256(fml_path.read_bytes()).hexdigest(),classes=fml.names,licence='unverified checkpoint terms; local-only comparison, no redistribution'),runs=[])
    profiles=[('YOLOE-original',engine.yolo,.25),('TRASH',engine.specialist,.3)]+([] if fml is None else [('FML',fml,.25)])
    def run(name,model,threshold,size):
        model.predict(source=next(iter(images.values())),device='cpu',imgsz=size,conf=threshold,verbose=False)
        rows=[]
        for fixture in fixtures:
            started=time.perf_counter();result=model.predict(source=images[fixture['fixture']],device='cpu',imgsz=size,conf=threshold,verbose=False)[0]
            detections=extract(result,name);elapsed=(time.perf_counter()-started)*1000
            kind=fixture['expectedKind'];wanted={'trash':{'CollectableRubbish','OtherRubbish'},'fish':{'Wildlife'},'boat':{'Obstacle'},'empty':set(),'occludedtrash':set(),'underwaterfish':{'Wildlife'}}.get(kind,set())
            rows.append(dict(fixture=fixture['fixture'],expectedKind=kind,wallMs=elapsed,detections=detections,expectedCategoryPresent=any(d['category'] in wanted for d in detections),eligiblePresent=any(d['eligible'] for d in detections),unexpectedCategories=[d for d in detections if d['category'] not in wanted]))
        report=dict(model=name,imgsz=size,confidence=threshold,medianMs=statistics.median(r['wallMs'] for r in rows),trashDetected=sum(r['expectedCategoryPresent'] for r in rows if r['expectedKind']=='trash'),trashEligible=sum(r['eligiblePresent'] for r in rows if r['expectedKind']=='trash'),fishDetected=sum(r['expectedCategoryPresent'] for r in rows if r['expectedKind']=='fish'),boatDetected=sum(r['expectedCategoryPresent'] for r in rows if r['expectedKind']=='boat'),emptyDetections=sum(len(r['detections']) for r in rows if r['expectedKind']=='empty'),frames=rows)
        experiment['runs'].append(report);(output/'comparison.json').write_text(json.dumps(experiment,indent=2))
        print(json.dumps({k:v for k,v in report.items() if k!='frames'}),flush=True)
    for name,model,threshold in profiles:
        for size in (416,640):run(name,model,threshold,size)
    if args.holdout:return
    engine.yolo=YOLOE(str(Path.home()/'Downloads/marineCam/yoloe-26s-seg.pt'))
    engine.yolo.set_classes(PROMPTS+['bottle','can','bag','trash','garbage'])
    experiment['variantPrompts']=PROMPTS+['bottle','can','bag','trash','garbage']
    for size in (416,640):run('YOLOE-expanded',engine.yolo,.25,size)
    tailored=['plastic jug','detergent bottle','water bottle','aluminum can','metal can','garbage bag','trash bag','plastic container']+PROMPTS[9:]
    engine.yolo=YOLOE(str(Path.home()/'Downloads/marineCam/yoloe-26s-seg.pt'))
    engine.yolo.set_classes(tailored)
    experiment['tailoredPrompts']=tailored
    for size in (416,640):run('YOLOE-tailored',engine.yolo,.25,size)

if __name__=='__main__':main()
