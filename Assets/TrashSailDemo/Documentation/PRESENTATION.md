# Coverage and observer presentation

Production navigation uses primary twin thrusters. Sailing restrictions and motor-assistance controls are absent from the production dashboard. The actuator layout is provisional.

## Wiring

Add CoveragePlanner and DemoPresentation to the DemoRuntime GameObject. After runtime configuration and population initialization, call Coverage.Initialize(Config, ownState.Position) and Presentation.Initialize(runtime). Assign Dashboard.Runtime. Runtime must stop positioning its old MainCamera when Presentation is active. The presentation disables that old observer camera and owns two independent render textures and cameras. It never modifies the onboard sensor camera, sensor calibration, image buffer, inference profile or capture resolution.

Mission integration calls UpdateProgress(estimatedState) every update, including diversions. Suspend preserves the current waypoint index; Resume continues from the same goal. ResetCoverage regenerates from boundary/configuration and clears the travelled trail. Initialize stores the starting point. Route completion is explicit and should terminate coverage rather than restart at a hidden target.

## Coverage assumptions

First lane is x0 northward from home. Adjacent lanes alternate north/south with sampled turns. Remaining positive-x lanes are followed by negative-x lanes; the transfer between halves is transit across a boundary end, not an observed-area claim. Lane spacing is limited to twice the declared useful detection range. TurningRadius defaults to 3 m and is capped at half lane spacing. Curved waypoints are a desired route, not proof of achieved physical curvature: runtime thruster dynamics and avoidance must still validate tracking. Compact and kilometre-scale boundaries are selected by LargeOcean.

The cyan trail is the actual estimated vessel route, including diversions; blue is planned coverage. Neither is an area-observation mask or proof of complete rubbish detection. Trail storage is bounded at 20,000 samples, initially spaced at least 1 m apart; when full it decimates older points and increases spacing, retaining an approximate whole-route history. The planner reads no world records, actor lists, sensor object identities or simulator target positions.

## Views and controls

Default split shows overview and chase simultaneously alongside RGB and sonar. Overview, chase, RGB or sonar may be maximized independently. Reset views restores the split and full-boundary overview. Drag the overview to pan, wheel to zoom, or enable follow. Drag chase to orbit and wheel to zoom. All view changes leave onboard rendering and neural inference unchanged.

The overview is labelled 'Observer overview — full simulated world'. Small world-record dots show rubbish, fish and boats at map scale without enlarging their physical geometry. The GT toggle controls these observer-only dots. Observed camera tracks T and sonar tracks H are a separate toggle. World truth never enters CoveragePlanner or mission planning. Overview north is up and a vertical scale is displayed.

RGB display filters combined / YOLOE / TRASH affect drawing only. ActiveProfile is reported separately. Provenance and conflict rendering are supplied by the camera adapter. Start, Stop, pause/resume, manual/autonomous, seeded reset, sensor freeze and sonar-noise controls retain the runtime lifecycle APIs.

## Verification scope

CoverageTests exercise boundary containment/first-lane direction, suspend/resume/reset and independence from scenario seed. Independent observer render textures are evident in source; actual maximization/capture independence, HDRP rendering, input usability and physical route tracking require the lead's serialized Unity run. No worker-launched Unity validation is claimed.

## Integration audit and repairs

Coverage turn apices now retain a hull/safety margin inside the mission boundary interlock; both compact and large boundaries have regression cases. Overview/chase outputs are letterboxed to their render-texture aspect, so maximization does not distort geometry or change camera projection. Current bearing-only optical tracks are explicitly counted on the RGB panel and noted on the overview without inventing map positions. These counts include all model evidence even when display filtering hides a model's boxes.

Dashboard Stop calls Runtime.StopMission (including manual takeover). Runtime now clears the pending pause-resume intent on Stop and scenario reset; pausing preserves intent only after its internal stop. Thus Stop while paused prevents mission restart when time resumes.

Source audit: neural overlay snapshots include no-range tracks, provenance, confidence and conflict flags; observer view switching never references SensorCamera or ActiveProfile. Coverage resume keeps its saved waypoint after target loss/capture. Lead-owned findings reported separately: mission must terminate/return on coverage completion and stop at home, avoid advancing route progress in manual/idle mode, and combine optical bearing hazards with sonar-safe candidate selection rather than overwriting that selection.
