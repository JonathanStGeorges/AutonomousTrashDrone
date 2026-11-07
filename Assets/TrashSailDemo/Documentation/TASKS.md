# Integration task registry — contract v1

All work is in the existing MARUS 2 host; no Unity/render-pipeline upgrade. Shared checkout, non-overlapping source ownership. Lead alone owns scenes, prefabs, shared contracts/config/asmdefs, editor automation, mission decisions and acceptance. Workers do not run a second Unity instance on this project.

| Task | Owner | Files | Dependency | Acceptance | Status |
|---|---|---|---|---|---|
| Baseline, contracts, autonomy, scene, acceptance | lead | DemoContracts.cs, DemoConfig.cs, DemoMission.cs, DemoRuntime.cs, DemoAcceptance.cs, Editor/*, all asmdefs/scenes/prefabs | host | rendered physical loop; A–H evaluated | baseline inspected; implementing |
| Vessel, environment, collector | vehicle executor | Runtime/Vehicle/*, Tests/VehicleTests.cs, Documentation/VEHICLE.md | contract v1 | Rigidbody actuators; physical any-body capture; sail/tack support; reset | assigned |
| Camera and pixel perception | camera executor | Runtime/Camera/*, Tests/CameraTests.cs, Documentation/CAMERA.md | contract v1 | actual completed rendered frames; pixel detections and tracks; water-plane ranging | assigned |
| MARUS sonar and contact tracking | sonar executor | Runtime/Sonar/*, Tests/SonarTests.cs, Documentation/SONAR.md | contract v1 | actual degraded sonar shared with preview; no annotations to planner | assigned |
| Dashboard, independent review | queued executors | assigned after first handoffs | integrated API | live controls and independent leakage/behaviour audit | queued |

Validation: active-editor bridge compiles/imports, runs Unity EditMode tests, generates standalone build; one rendered player executes repeatable scenarios and writes JSONL, JSON summaries and PNG evidence. Worker fixture checks are provisional until integration. Phase gates are recorded in GATES.md after actual evidence.
