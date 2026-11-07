"""Offline real-model evaluation. Run only when Unity's live inference is stopped.

Truth CSV: frame,label,category,x1,y1,x2,y2 (top-left original pixels).
Use label=NONE for an exhaustively reviewed empty image. Missing CSV frames are
UNSCORED, never presumed negative. Label '*' matches any label in that category.
Truth is used only after predictions; it never enters models or Unity control.
"""
import argparse
import csv
import json
import math
from pathlib import Path
import time
from service import Engine

def iou(a,b):
    intersection=max(0,min(a[2],b[2])-max(a[0],b[0]))*max(0,min(a[3],b[3])-max(a[1],b[1]))
    union=(a[2]-a[0])*(a[3]-a[1])+(b[2]-b[0])*(b[3]-b[1])-intersection
    return intersection/union if union>0 else 0

def score(detections,truth,threshold):
    available=set(range(len(truth)));matches=[];false_positives=[]
    for detection in sorted(detections,key=lambda d:d['score'],reverse=True):
        candidates=[(iou(detection['box'],truth[i]['box']),i) for i in available
                    if truth[i]['category']==detection['category'] and truth[i]['label'] in ('*',detection['label'])]
        best=max(candidates,default=(0,-1))
        if best[0]>=threshold:
            available.remove(best[1]);matches.append(dict(prediction=detection,truth=truth[best[1]],iou=best[0]))
        else:false_positives.append(detection)
    return dict(tp=len(matches),fp=len(false_positives),fn=len(available),matches=matches,falsePositives=false_positives,misses=[truth[i] for i in sorted(available)])

def contact(detection,calibration):
    box=detection['box'];polygon=detection.get('polygon',[])
    if polygon:
        bottom=max(polygon[1::2]);xs=[polygon[i] for i in range(0,len(polygon),2) if polygon[i+1]>=bottom-1]
        x=sum(xs)/len(xs);y=bottom
    else:x=(box[0]+box[2])/2;y=box[3]
    observation=dict(pixel=[x,y],position=None,reason='range unknown')
    if not calibration or detection['category'] not in ('CollectableRubbish','OtherRubbish'):return observation
    w,h=calibration['width'],calibration['height'];bottom_y=h-y
    if bottom_y<1 or bottom_y>=h-1:return observation
    tangent=math.tan(math.radians(calibration['verticalFov'])/2)
    ray=[(x/w*2-1)*w/h*tangent,(bottom_y/h*2-1)*tangent,1]
    m=calibration['cameraToWorld'];prefix='m' if 'm00' in m else 'e'
    origin=[m[prefix+'03'],m[prefix+'13'],m[prefix+'23']]
    direction=[sum(m[f'{prefix}{r}{c}']*ray[c] for c in range(3)) for r in range(3)]
    magnitude=math.sqrt(sum(v*v for v in direction));direction=[v/magnitude for v in direction]
    water=calibration['waterLevel']
    if origin[1]<=water+.05 or direction[1]>=-.045:return observation
    distance=(water-origin[1])/direction[1]
    if distance<=0 or distance>35:return observation
    observation.update(position=[origin[i]+direction[i]*distance for i in range(3)],reason='estimated water contact (not truth)')
    return observation

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('session',type=Path);p.add_argument('--truth',type=Path)
    p.add_argument('--output',type=Path,required=True);p.add_argument('--limit',type=int,default=20)
    p.add_argument('--stride',type=int,default=1);p.add_argument('--iou',type=float,default=.3)
    p.add_argument('--imgsz',type=int,default=416);p.add_argument('--assets',default=str(Path.home()/'Downloads/marineCam'))
    args=p.parse_args();session=args.session.resolve();output=args.output.resolve();output.mkdir(parents=True,exist_ok=True)
    truth={}
    if args.truth:
        with args.truth.open(newline='',encoding='utf-8-sig') as stream:
            for row in csv.DictReader(stream):
                entries=truth.setdefault(row['frame'],[])
                if row['label']!='NONE':entries.append(dict(label=row['label'],category=row['category'],box=[float(row[k]) for k in ('x1','y1','x2','y2')]))
    engine=Engine(args.assets,imgsz=args.imgsz)
    reports=[]
    for path in sorted(session.glob('*.png'))[::max(1,args.stride)][:max(1,min(200,args.limit))]:
        image=engine.cv2.imread(str(path));started=time.perf_counter()
        detections=engine.predict(image,'combined');elapsed=(time.perf_counter()-started)*1000
        sidecar=path.with_suffix('.calibration.json');calibration=json.loads(sidecar.read_text()) if sidecar.exists() else None
        report=dict(frame=path.name,wallInferenceMs=elapsed,detections=detections,scoring='UNSCORED',models={})
        if path.name in truth:
            report['scoring']='MANUAL EXHAUSTIVE TRUTH'
            for model in ('YOLOE','TRASH'):report['models'][model]=score([d for d in detections if d['model']==model],truth[path.name],args.iou)
        for index,detection in enumerate(detections):
            detection['contactEstimate']=contact(detection,calibration)
            x1,y1,x2,y2=map(round,detection['box']);colour=(20,20,240) if detection['conflict'] else (30,220,50)
            engine.cv2.rectangle(image,(x1,y1),(x2,y2),colour,1)
            text=f"{detection['model']}:{detection['label']} {detection['score']:.2f} {detection['category']}"
            engine.cv2.putText(image,text,(4,18+index*18),engine.cv2.FONT_HERSHEY_SIMPLEX,.4,colour,1)
            point=tuple(map(round,detection['contactEstimate']['pixel']));engine.cv2.circle(image,point,3,(255,0,255),-1)
        engine.cv2.imwrite(str(output/(path.stem+'-overlay.png')),image)
        reports.append(report)
    summary=dict(sourceSession=str(session),manualTruth=str(args.truth) if args.truth else None,iou=args.iou,imgsz=args.imgsz,frames=reports,modelManifest=engine.manifest)
    (output/'evaluation.json').write_text(json.dumps(summary,indent=2),encoding='utf8')
    print(json.dumps(dict(frames=len(reports),scored=sum(r['scoring']!='UNSCORED' for r in reports),detections=sum(len(r['detections']) for r in reports))))

if __name__=='__main__':main()
