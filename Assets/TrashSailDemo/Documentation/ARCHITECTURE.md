# Architecture, audit and assumptions

## Installation baseline

Host `marus2-example`, initial branch `main`, revision `0f9ec3f8a4a30126bcbed8ddbf90800fafa33f06`; development branch `feature/trash-sail-demo`. MARUS core submodule `747bcee11adfa0f4bbd60db698b24794ab5581c8`; sensors `c88714d750d0b0d99dcfde77694ec5f37d407ec5` plus the documented local sonar rotation repair. Exact ten-module revisions are in `Evidence/TrashSailDemo/submodule-revisions.txt`. Git LFS integrity checked. Existing modified HDRP/global project settings were snapshotted in `pre-existing-changes.patch` and not intentionally changed/staged. Unsaved ExampleScene was copied into `Assets/TrashSailDemo/LocalPreservation`.

Unity6000.5.11f1/HDRP17.5.0 and an NVIDIA RTX5070 Laptop GPU were present. Existing CameraSensor uses asynchronous GPU readback but lacks an exposed completed sequence/pose contract; SensorSampler also sets hasData prematurely. Existing Sonar3D exposes polar/Cartesian outputs and annotation-bearing SonarReading. The new operational boundary never reads annotation IDs. Existing transform/Target-based demonstration controllers were inspected and not used for autonomous drive. Calm-water height uses the installed MARUS WaterHeightSampler; forces use a replaceable DemoVessel actuator implementation. Docker/WSL commands exist but no external inference/ROS service was required or installed for this feature.

## Pipeline

Rendered camera → bounded completed RGB frame → connected colour components → water-plane range → persistent visual tracks.

MARUS Sonar3D CPU raycasts → completed polar texture → seeded intensity degradation/dropout/latency → connected range-bearing components → unknown temporal contacts.

Those two streams + simulated own-state/wind observations → DemoMission state machine → bounded rudder/sail/motor/collector commands → Rigidbody forces/torques and physical intake trigger. Scenario actor identities/poses live exclusively in world generation, sensing physics and DemoEvaluation. Dashboard map uses estimated tracks. No simulator ground-truth detector overlays are enabled.

## Operational assumptions

- Metres/seconds; +Y up, +Z bow, +X starboard. Simulation timestamps use Time.timeAsDouble. Frame/track coordinate and buffer ownership details are in CONTRACTS.md.
- Calm opaque water at y0, no waves/current in acceptance. Original primitive catamaran is provisional, 3m×2.2m, 110kg. Hull/intake dimensions enter safety and evaluation envelopes.
- RGB controlled synthetic objects are saturated orange/red; muted candidates remain unknown. This is not a trained litter classifier. Fish below water are excluded from the onboard RGB visibility layer; no fish boxes are invented.
- Camera pose is estimated own pose plus calibrated mount. Bottom/contact pixels intersect estimated water plane; horizon/invalid/cropped contact rays are rejected. The generated demo camera uses pitch38°, verticalFOV68° to observe near-intake water while retaining useful forward coverage.
- Sonar is an approximate simultaneous multibeam fan, 96×9=864 rays, requested8Hz,128range bins,0.18–16m,110°horizontal/35°vertical. Its 2D projection does not resolve true contact elevation. Source timing is conservatively estimated from completed texture updates; exact acoustic travel time/refraction/multipath/scattering are not modelled. See SONAR.md.
- Both sonar display and autonomy consume the identical degraded intensity representation. One missing range bin may join fragments within0.25m; measured combined extents are retained so merged objects are not secretly split using truth.
- Own-state zero noise/latency mode is labelled IDEAL; configurable noise/latency are simulation approximations, not certified GNSS/IMU models.
- Optional motor force and approximate true-wind sail polar are explicit. No velocity/Transform following is used in autonomous drive. Reset and post-capture storage relocation are labelled simulation operations, not navigation.

## Safety decisions

Mission phases expose reasons. A unique persistent fresh sonar echo can be associated with a positive visual target only inside a narrow geometric gate, with compatible velocity and measured extent. Multiple/unmatched/merged conflicting contacts remain unknown hazards. Hull/intake width, current speed, processing delay and track uncertainty enlarge the prediction corridor. New narrow far target echoes may cause a collector-closed depowered assessment; genuine hazards cause finite-force steering. Lost tracks retain uncertainty/memory. Stale camera or sonar closes collection and depowers; stopping does not erase inertia.

Intake opening requires positive persistent RGB classification, fresh required sensors, alignment, speed, capacity and clearance. Counter increments require a body's centre entering the active capture volume. Any dynamic body can enter, including fish; overspeed ingress is recorded as unsafe rather than silently discarded. Only evaluation reads labels to distinguish correct capture/bycatch. A conservative oriented hull-plus-intake envelope, not centre distance, is used for minimum hazard separation.

## Upstream change

`marus2-sensors/Runtime/Sonar/Sonar3D.cs` previously dotted sensor-local ray directions with world-space normals. It now caches sensor rotation on the main thread and rotates the ray before the intensity calculation inside the worker job. Dot product is clamped before acos. The small scheduling/rotation mismatch remains part of the declared sonar approximation. No other upstream subsystem is intentionally modified.
