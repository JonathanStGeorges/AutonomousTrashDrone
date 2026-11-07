# FML candidate assessment — 2026-11-07

Recommendation: **bounded offline comparison only after corrected Unity frames**, not automatic production replacement. The surface-domain match is promising; the single garbage class cannot establish collector suitability or identify wildlife.

Verified primary sources:

- [Official repository](https://github.com/glazzerini/fml) links the [published Google Drive weights folder](https://drive.google.com/drive/folders/1QTGO9rDWCKzvo5DlphT6a4IzraDRwJ4o). Its unauthenticated HTML lists `yolov8s_fml.pt` and `fasterrcnn_fml.pth`. YOLO weight file ID `1gXY9K_txdq5PqqGNlP9yNqeY9QwAY9y8`; an HTTP HEAD against its download URL returned 200, application/octet-stream, 22,549,859 bytes. **No weights downloaded or loaded.**
- [Dataset DOI](https://doi.org/10.17882/106148) resolves in [DataCite metadata](https://api.datacite.org/dois/10.17882/106148) to SEANOE and declares CC BY 4.0. This is the dataset licence, not proof of a checkpoint licence. SEANOE HTML returned 403 through our web tool.
- GitHub API reports `license:null`; repository tree has no LICENSE. Published checkpoint terms remain unverified. Do not redistribute or claim blanket CC BY coverage for weights/code. Ultralytics runtime obligations remain separate.
- [YOLO evaluator](https://github.com/glazzerini/fml/blob/main/yolo_eval_pycocotools.py) loads the .pt through `ultralytics.YOLO` and uses confidence .5. [Training script](https://github.com/glazzerini/fml/blob/main/yolo_training.py) starts YOLOv8s at 1024 input; it references an undefined `device`, so it is not a complete reproducible environment. No pinned requirements are supplied. [train.yaml](https://github.com/glazzerini/fml/blob/main/train.yaml) defines only class 0, `garbage`.

Inference with the YOLO checkpoint is likely compatible with the existing Ultralytics environment, but actual loading has not been verified. Repository evaluation adds pycocotools, torchvision, matplotlib, Pillow and NumPy. Our existing evaluator can avoid adding the COCO plotting stack for a small manual-box comparison. Faster R-CNN adds a larger torchvision model and is unnecessary for the first comparison.

The dataset documentation describes USV-view surface litter with about 5,300 images and 13,000 boxes, 1920x1080 source resolution. This is a more relevant acquisition domain than underwater ROV material classification. It does **not** prove better performance on tiny simulated bottles, near-field intake views or novel textures. No validated Unity benchmark or wildlife capability is published in the inspected source.

Suggested comparison: 20–40 frozen corrected frames with manually labelled boxes, including visible rubbish at several pixel sizes, wave-only negatives, obstacles and wildlife. Compare current YOLOE/TRASH against FML separately at 416 and 640, keeping source PNGs identical and reporting false positives, misses, box IoU and CPU latency. Preserve model provenance and keep FML `garbage` as OtherRubbish until separate suitability gates exist. This plan requires authorized checkpoint acquisition and explicit handling of the unresolved checkpoint licence; no third model is currently installed.
