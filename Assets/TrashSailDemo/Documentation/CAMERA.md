# Production camera and pretrained neural perception

The generated production scene configures an actual onboard HDRP camera at **640 x 384 pixels and 3 requested captures/inference requests per simulation second**, with 68-degree vertical FOV and a 38-degree downward mounting pitch. These scene-builder values override older default/prototype settings. Achieved rates depend on rendering and inference latency and must be measured. No colour detector or heuristic fallback supplies production detections.

## Capture and transport

DemoCameraPerception uses a small adapter around the rendered-camera approach of installed MARUS CameraSensor. It does not treat upstream hasData as a completed-frame signal. Begin-render records sequence, simulation capture time, calibrated pose and FOV; end-render schedules asynchronous GPU readback. Only successful completion provides immutable bottom-left RGBA pixels. The pipeline bounds GPU/readback and HTTP inference work rather than queuing stale frames. Reset rotates the inference session and invalidates callbacks.

The same unannotated source PNG goes to the configured pretrained profile on localhost. Default combined inference runs YOLOE and the marine specialist; actual weights, licences, hashes, runtime environment and class maps are in MODELS.md and Inference/loaded-models.json. Ultralytics results use original-image top-left boxes/mask polygons. Unity converts them to the retained source frame's bottom-left coordinates. Overlay snapshots match that frame, including provenance, model score, conflict and selected-target status. Scores are model outputs, not claims of calibrated real-world probability.

Request/result matching checks session, camera, sequence, dimensions, capture timestamp and active profile. Successful empty detections are valid negative observations. Network/model errors, mismatches, stale simulation captures or excessive wall age do not refresh operational tracks. Stale source images may remain visible with explicit health/age. ActiveProfile changes computation; DisplayFilter changes only drawing, never safety evidence.

## Visibility, geometry and tracking

The camera **includes underwater layer 9**. HDRP water transparency/refraction, object depth, occlusion and actual rendered pixels determine whether a submerged fish is visible. Own hull layer8, observer-only12 and UI5 are excluded. A visible fish can produce genuine model evidence; an invisible fish must not gain an optical box from simulator truth. Camera code does not read actor lists, names/tags, annotation boxes or true target positions.

Floating eligible categories estimate water contact using the lowest segmentation-mask edge, or box bottom when no polygon is available, transformed through calibrated rays and estimated own pose/water surface. Cropped contacts, horizon geometry and invalid intersections are rejected. Wildlife/unknown evidence never receives an invented surface-plane range. Such tracks remain bearing-only, still appear on RGB overlays and can inhibit collection; they are counted as unranged rather than positioned on the overview map.

Tracks retain model/class provenance, eligibility and conflicts; incompatible evidence is not averaged away. Capture age and uncertainty remain distinct from inference completion. The simplified water-plane estimate is sensitive to waves, object protrusion, camera calibration and visibility, and is not a validated depth sensor.

## Verification boundary

The lead reported **36 Unity Edit Mode tests passing in the current production run** (2026-11-07). This is lead-provided execution evidence, not a run by this document author. It does not establish rendered object detection, reliable fish recognition or physical collection acceptance. Raw actual source PNGs and corresponding model JSON are saved under Evidence/TrashSailDemo/sourceframes; use Inference/compare_frame.py for independent same-input service/direct comparison. See MODELS.md and final lead acceptance evidence for measured outcomes and limitations.

For real deployment replace capture with calibrated timestamped sensor data, validate models on representative marine imagery and classes, measure precision/recall and uncertainty, synchronize own-state and preserve health/conflict interlocks. No simulator-only visibility assumption should silently become a hardware guarantee.
