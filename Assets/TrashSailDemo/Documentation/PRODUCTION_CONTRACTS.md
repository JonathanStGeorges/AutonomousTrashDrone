# Production contract v2 and ownership

The latest user correction makes **twin thrusters** the provisional normal actuator layout. Sailing restrictions are retired from production navigation. Existing source/asset .meta GUIDs and original scenes remain intact. Baseline prototype is checkpointed in Git; unrelated HDRP/terrain/project setting modifications remain unstaged.

## Frame and result

CameraFrame retains immutable completed bottom-left RGB(A), capture-time pose/calibration, simulation timestamp and monotonically increasing sequence. Python request contains session/reset UUID, cameraId, sequence, simulation captureTime, original dimensions, active model profile and base64 unannotated PNG. Python decodes PNG to BGR for Ultralytics; boxes/polygons return in **original-image top-left pixel xyxy/flattened xy** coordinates. Unity converts them to its bottom-left frame for tracking/overlays, using the retained capture-time calibration for ranging.

`InferenceResult.ok=true,empty=true` is a successful negative result; `ok=false` is a fault. Every detection retains model, original classId/label/score/geometry, canonical category, eligibility and conflict. Both requested models see the same identified input; incompatible wildlife/trash evidence remains separate and blocks collection. No score averaging. Broad material labels alone do not prove collector suitability. Active inference profile is independent from display filters. One in-flight request, bounded latest-frame processing, immutable retained source frame, wall-clock request/result age separate from simulation capture age; reject old session/sequence/stale results. No operational heuristic fallback.

Canonical categories: `CollectableRubbish`, `OtherRubbish`, `Wildlife`, `Obstacle`, `Unknown`. VisualTrack adds eligibility/conflict/provenance/model evidence. Mission must check these before selection and intake activation. Camera and sonar optical/acoustic domains remain distinct; sonar stays unknown-contact tracking from degraded MARUS intensity.

## World versus observations

World records contain stable IDs, seeded position/drift, asset kind and collected status. Only simulation, streaming, spectator overview and evaluator may read them. Coverage generators use boundary/sensor range/turning parameters; mission only consumes route progress and estimated sensor tracks. Observer overview must say **full simulated world**. Streaming activates physics/meshes before sensor/manoeuvring range and never restores a collected record during the same mission.

## Actuators

Unity +Y up/+Z bow/+X starboard; metres/seconds; world heading clockwise from+Z. PortThruster at negativeX, Starboard at positiveX. +Z force on port produces positive/right yaw, so right turn uses port greater than starboard. ActuatorCommand.PortThrust/StarboardThrust are normalized signed commands, reverse only if enabled. RequestedSpeed/Heading are telemetry. Apply forces at configurable mount transforms; response rate, maxN, inertia, water drag, command watchdog and finite stopping are explicit. Legacy sail fields temporarily remain for serialized migration only, with zero production authority.

## Implementation ownership

| Lane | Owner | Exclusive files | Gates |
|---|---|---|---|
| Models/service/camera | model executor | Inference/*, Runtime/Camera/*, Tests/CameraTests.cs, Documentation/MODELS.md | actual cached weights/class maps, direct/service comparison, live Unity frames |
| Imported assets/world | assets executor | Assets/TrashSailDemo/Imported/*, Runtime/Ocean/*, Runtime/Vehicle/DemoWorld.cs and DemoActor.cs, Editor/Assets/*, Tools/Assets/*, Documentation/ASSETS.md | licensed downloaded files/hashes, renderable prefabs, persistent records |
| Coverage/presentation | presentation executor | Runtime/Coverage/*, Runtime/Presentation/*, Runtime/DemoDashboard.cs, Tests/CoverageTests.cs, Documentation/PRESENTATION.md | independent onboard camera, coverage suspend/resume, spectator truth labelled |
| Integrator | lead | shared contracts/config/asmdefs; DemoMission/DemoRuntime/DemoEvaluation/DemoAcceptance; DemoVessel/DemoCollector/DemoOwnState; shared scene/prefab builder/tests/docs | physical thrusters, safety fusion, scene assembly, integrated acceptance |

Shared checkout: no overlapping edits, no worker Unity Editor launches, no nested agents. Workers report interface changes before editing shared contracts. Lead serializes Editor import/tests/setup/build/rendered runs. Source edits freeze during each rendered acceptance run. A worker handoff is provisional until integrated.
