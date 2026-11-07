# Actual two-model camera inference

The operational camera path runs **YOLOE yoloe-26s-seg.pt and Krishna-Jaiswal/yolov8m_marine_best.pt on the same unannotated Unity frame**. The original `Downloads/marineCam/marine_webcam.py` and its environment were preserved. No webcam is opened. No heuristic detector is used as fallback; the obsolete colour detector and its runtime/test dependencies have been removed. The existing source filename/GUID is retained for camera geometry only.

## Launch and evidence

From the project root, run `powershell -File Inference/start.ps1`. The default interpreter is the existing `C:/Users/jonat/Downloads/marineCam/.venv/Scripts/python.exe`. Poll `http://127.0.0.1:8765/health` until `ok:true`. Startup loads weights, embeds prompts once, warms both models and records their **actual** class maps and SHA256 values in `Inference/loaded-models.json`. Stop this project's service with `powershell -File Inference/stop.ps1`.

The tested CPU environment is frozen in `Inference/environment.lock.txt`: torch 2.15.0+cpu, ultralytics 8.4.204, OpenCV 5.0.0. CPU was selected because the installed torch lacks CUDA. No original packages were upgraded. Three warmed combined 416-size blank-input timing requests took 137.46, 128.29 and 123.54 ms; these are timing fixtures, **not evidence of Unity object detection**. Integrated rendering competes for CPU and needs its own measured rates. A CUDA migration is not required by this baseline measurement.

The Unity camera automatically saves its first 20 raw PNG inputs per reset to `Evidence/TrashSailDemo/sourceframes/<sessionUUID>/`, with corresponding service JSON when successful. These are actual onboard source frames, not crops of dashboard screenshots. Run:

```powershell
& C:/Users/jonat/Downloads/marineCam/.venv/Scripts/python.exe Inference/compare_frame.py '<absolute raw Unity PNG>' --output Inference/comparison.json
```

This independently loads the same pinned models, predicts directly, calls the service with the identical PNG, then checks exact returned detections and PNG hash. Do not infer a positive collection result from an empty but successful comparison.

## Preprocessing, contracts and control separation

Unity retains an immutable bottom-left RGBA frame with capture-time calibration and estimated pose. PNG preserves its image orientation; OpenCV decodes it to BGR. Ultralytics handles inference letterboxing at imgsz416 and returns original-image top-left boxes and mask polygons. Unity converts coordinates back to bottom-left and uses the lowest mask edge (box bottom only if no polygon) as an estimated floating-object water contact. Horizon/crop rejection remains active. Wildlife and unknown observations never receive a surface-plane range.

Requests echo session UUID, camera ID, sequence, capture time, width, height and active profile. There is one GPU readback and one HTTP request at most, no inference queue. Reset aborts the client request, rotates its UUID and clears tracks. Mismatched results, active-profile changes and frozen feeds are rejected. Frames exceeding simulation freshness or wall-clock age are displayed only; their detections do not update operational tracks. HTTP/model faults disable camera health, whereas `ok:true,empty:true` is a valid negative observation. Wall inference duration and simulation capture age are separate. Network timeout is 15 seconds, with freshness normally expiring earlier.

`ActiveProfile` selects combined/YOLOE/TRASH computation. `DisplayFilter` only filters model overlays; it never changes the tracks supplied to autonomy. The default required profile is combined. Each model/class owns a perception track; no cross-model score averaging or wildlife suppression occurs. Scores are model outputs, not calibrated probabilities. Overlap over 15% of the smaller box between wildlife and another category flags both evidence records as conflicting and disables eligibility. The mission must still check geometry, age, persistence, sonar clearance and intake suitability.

Only YOLOE plastic bottle, plastic bag, drink can and food container are semantic collection candidates. Other prompt rubbish and specialist material labels are `OtherRubbish`, never eligible on material alone. Specialist animal_* and YOLOE wildlife prompts remain wildlife; boat/person/buoy/rov are obstacles. This mapping is explicit and does not establish that an object is abandoned litter. Plant remains unknown. Actual IDs are in the manifest: e.g. specialist animal_fish=2 and trash_plastic=13; YOLOE fish=9 and plastic bottle=0. IDs are not shared across models.

## Sources, hashes and licences

- [Ultralytics YOLOE documentation](https://docs.ultralytics.com/models/yoloe/) describes the chosen text-prompt model. Weight source: [official assets v8.4.0](https://github.com/ultralytics/assets/releases/tag/v8.4.0). YOLOE SHA256 `48f24206bc8680d60cbbfa296b0140da849669b9515058b72f5a945142df0654`.
- [Specialist publisher model card](https://huggingface.co/Krishna-Jaiswal/yolov8m-marine-trash): pinned revision `a77dc9ce0e8ce6640b0c3800ec03cde9daa39fd3`, weight SHA256 `f5afba16fcb65ad8c74512211b9ae023a878538e2015bc3e10fd00ba8823e037`. This model was trained on underwater ROV imagery, not floating surface litter; this simulation is a domain shift.
- Ultralytics runtime/models use its [AGPL-3.0 licensing](https://github.com/ultralytics/ultralytics/blob/main/LICENSE). The specialist publisher labels its repository MIT; this does not establish that upstream dependencies/model obligations disappear.
- MobileCLIP2 encoder SHA256 `35d7f213e4d75f38514e4656ad3cb91158bd33e3805d8ac349f23b186f66982f`. Apple [MobileCLIP source licence](https://github.com/apple-aiml-research/ml-mobileclip/blob/main/LICENSE) and pretrained model terms are separate; consult the [repository's model terms](https://github.com/apple-aiml-research/ml-mobileclip) before redistribution.

No trained model is represented as validated fish recognition or real-world safe collection. The remaining acceptance gate is positive and negative inference on integrated rendered assets, followed by actuator/collection behaviour; compilation and blank timing fixtures do not satisfy that gate.

## Final fixed-profile lit holdout

The 29-frame seed41 holdout is `Evidence/TrashSailDemo/production/perception-holdout-20261107-132246`. Results and per-frame overlays are `Inference/holdout-evaluation/`; see RESULTS.md for full counts. Across25 visible rubbish frames, originalYOLOE at416/640 produced0/1 rubbish-category appearances and zero eligible targets; the specialist produced6/6 broad material appearances and zero eligible targets. False wildlife appeared on13/14 YOLOE rubbish frames and10/11 specialist frames. These are category-presence counts, not class correctness or bounding-box accuracy. Actual fish recognition occurred in one YOLOE640 and specialist416 view, while other configurations missed it. One empty frame was negative for every model. FML scored0/25 at both sizes on this holdout and was not integrated.

The actual lit source `00-plastic_bottle_gallon-2.5m-45deg.png` passed exact direct/service PNG hash and numeric-output equality in `Inference/holdout-evaluation/direct-service.json`: jellyfish .337, trash_plastic .572 and animal_shells .536, all conflicting and ineligible. Real neural processing and transport work; reliable positive eligible recognition is a genuine acceptance blocker. The next model-development step is labelled surface data spanning rendered and real camera images, controlled fine-tuning and independent held-out evaluation. The requested original models remain active; no heuristics or privileged labels replace their outputs.
