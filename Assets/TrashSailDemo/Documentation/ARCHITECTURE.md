# Production v2 architecture

## Baseline and historical boundary

The functioning host is marus2-example, Unity 6000.5.11f1/HDRP17.5. Initial host revision was 0f9ec3f8a4a30126bcbed8ddbf90800fafa33f06. Checkpoint 7b2f197 preserves the earlier rendered collector prototype. Original project settings/user changes and scene preservation records remain under Evidence/TrashSailDemo and LocalPreservation. PRODUCTION_CONTRACTS.md supersedes v1 control/perception descriptions. Earlier CAMERA.md, DASHBOARD.md and REVIEW-VEHICLE-AGENT.md may describe prototype stages; current run guidance is README.md plus the v2 subsystem documents.

## Operational path

Actual onboard rendering -> immutable completed RGB frame with sequence/capture pose -> one bounded localhost inference request -> YOLOE and marine specialist results on the same PNG -> provenance-preserving visual tracks -> estimated mission model.

MARUS Sonar3D completed polar intensity -> seeded degradation/dropout/latency -> connected measured echo components -> unknown contact tracks -> the same estimated mission model. Preview and autonomy use the same degraded sonar representation; annotation class/instance IDs and collider references never enter operational tracking.

Estimated GNSS/IMU/wind state plus boundary-only CoveragePlanner and those sensor tracks -> DemoMission safety/state decisions -> signed port/starboard commands and collection state -> force-at-mount Rigidbody actuation and physical intake events.

World records, actor identities and collected flags are simulation truth. Only generation, streaming, physical simulation, evaluator and explicitly labelled observer overview read them. Coverage and planning never receive the population list. Observer cameras and render textures are separate from the onboard camera; display filters do not change active inference or safety inputs.

## Inference and uncertainty

No colour heuristic fallback remains in the production detector path. The service uses the actual cached models and class maps documented in MODELS.md; successful empty results differ from faults. Requests/results carry session, sequence, profile, image dimensions and capture time. Old sessions, mismatches, stale simulation frames and excessive wall latency are rejected for control. Reset invalidates callbacks and clears tracks.

Floating eligible categories use the lowest mask/box contact edge intersected with an estimated water plane, with horizon and cropping rejection. Wildlife/unknown observations retain bearing-only evidence without pretending they lie on the surface. Model conflicts remain separate evidence and inhibit collection. Network and inference latency are not hidden by new timestamps.

Sonar remains a coarse 2D multibeam approximation: no resolved true elevation, real acoustic propagation/scattering validation or certified fish classification. Completion timing and pose interpolation are approximate. Unknown/lost contacts persist with uncertainty. See SONAR.md for acquisition and noise details.

## Physics and mission

Coordinates are metres/seconds, +Y up, +Z bow, +X starboard; heading is clockwise from world +Z. Simulated own state may be ideal or noisy/delayed. Twin signed thrusters apply bounded forces at configurable mounts with response lag, watchdog, inertia and drag. Legacy sail fields exist only for migration and exert no production authority. Wind/current are declared disturbances, not wind-routing constraints.

Mission suspends coverage when selecting a persistent eligible visual target and resumes its saved waypoint after abandonment/capture. Freshness, model conflicts, relative motion, measured sonar extent, target association, hull/intake envelope, capacity and alignment gate collection. Bearing-only optical hazards close the intake without inventing a world range. Sensor failure closes collection and latches a depowered state until restart. Remaining actual safety outcomes require rendered acceptance, not source inspection.

Physical capture requires an external body's centre entering the active volume; any body can be captured, including fish. Overspeed ingress is recorded as unsafe. Scoring truth distinguishes rubbish, bycatch and collisions independently of control. Capture storage relocation and seeded reset are explicit simulator operations, not navigation teleportation.

## Rendering and assets

Imported assets retain documented licences, hashes and textured materials (ASSETS.md). The imported boat visual is a proxy over a provisional conservative collision envelope. HDRP ocean rendering is configured by the dedicated scene/helper; physical buoyancy still uses the declared mean-water approximation. Presentation exposes a full-world observer and independent chase while retaining actual sensor views. Visual waves do not establish validated wave-coupled dynamics.

## Integration status

Current code is undergoing serialized lead-owned Unity verification. Source/fixture compilation is not rendered acceptance. GATES.md distinguishes historical results, provisional implementation and pending integrated tests. A small upstream sonar correction rotates local ray direction into world coordinates for the normal-incidence calculation; residual scheduling/pose approximation remains disclosed.
