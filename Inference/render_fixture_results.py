"""Render saved inference evidence only; never runs models or modifies source PNGs."""
import argparse
import json
from pathlib import Path
import cv2
from evaluate_frames import contact

p=argparse.ArgumentParser();p.add_argument('comparison',type=Path);args=p.parse_args()
report=json.loads(args.comparison.read_text());root=Path(report['fixtures'])
fixtures={f['fixture']:f for f in json.loads((root/'fixtures.json').read_text())['fixtures']}
for run in report['runs']:
    destination=args.comparison.parent/f"{run['model']}-{run['imgsz']}";destination.mkdir(exist_ok=True)
    for row in run['frames']:
        frame=fixtures[row['fixture']];calibration=dict(frame,waterLevel=0)
        image=cv2.imread(str(root/(row['fixture']+'.png')))
        cv2.putText(image,f"EVALUATOR ONLY {run['model']} {run['imgsz']} expected:{row['expectedKind']}",(5,16),cv2.FONT_HERSHEY_SIMPLEX,.4,(255,255,255),1)
        for i,d in enumerate(row['detections']):
            point=contact(d,calibration);d['contactEstimate']=point
            x1,y1,x2,y2=map(round,d['box']);cv2.rectangle(image,(x1,y1),(x2,y2),(0,220,255),1)
            cv2.circle(image,tuple(map(round,point['pixel'])),3,(255,0,255),-1)
            label=f"{d['label']} {d['score']:.3f} {d['category']}"
            if point['position'] is not None:label+=f" XZ={point['position'][0]:.2f},{point['position'][2]:.2f}m"
            else:label+=' RANGE UNKNOWN'
            cv2.putText(image,label,(5,34+i*18),cv2.FONT_HERSHEY_SIMPLEX,.4,(0,220,255),1)
        cv2.imwrite(str(destination/(row['fixture']+'.png')),image)
args.comparison.write_text(json.dumps(report,indent=2))
print('Saved overlays and estimated contact geometry; no inference executed')
