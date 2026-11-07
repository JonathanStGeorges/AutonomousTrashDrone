# Task registry - production contract v2

Shared checkout, disjoint ownership. Lead serializes all Unity imports, scene setup, tests, rendered runs and builds. Worker source freezes are required before those runs. PRODUCTION_CONTRACTS.md defines interfaces; no worker may silently replace shared DTOs or transfer truth into autonomy.

| Task / owner | Exclusive ownership | Dependencies | Acceptance and validation | Current integration status |
|---|---|---|---|---|
| Lead integrator / root | Contracts/config/asmdefs, DemoMission, DemoRuntime, DemoVessel/Collector/OwnState, scene/prefab/editor assembly, evaluation/acceptance | All three worker APIs, functioning host | Unity compilation/tests; primary-thruster physical loop; safety/failure/reset and rendered evidence | Integrated source, current rendered validation in progress; no new acceptance PASS asserted here |
| Worker A / camera | Inference/*, Runtime/Camera/*, CameraTests, MODELS.md | Completed frame v2 contract, cached pretrained models | Actual weights/class maps/hashes, same-frame direct/service comparison, freshness/conflicts, live Unity source PNGs | Service and camera implementation handed off; source/fixture evidence in MODELS.md; integrated scene evidence lead-owned |
| Worker B / sonar lane reassigned to assets | Imported/*, Runtime/Ocean/*, DemoWorld/DemoActor, Editor/Assets/*, Tools/Assets/*, ASSETS.md | Existing host/HDRP, imported assets, stable root | Downloaded licensed assets/hashes, imported renderer bounds, persistent streaming/collected records | Source/assets handed off; serialized import/render acceptance lead-owned |
| Worker C / vehicle lane reassigned to coverage/presentation | Runtime/Coverage/*, Runtime/Presentation/*, DemoDashboard, CoverageTests, PRESENTATION.md; current documentation refresh | Own-state v2, OceanPopulation observer API, thruster/model telemetry | Boundary-only route, suspend/resume, independent cameras, no-range/conflict visibility, controls | Coverage/test source compile passed; boundary-turn inset and pause-stop fix integrated; rendered verification pending |

Worker C temporarily owned only Runtime.StopMission/SetPaused pause-intent repair with lead authorization, then returned ownership and froze source. Independent review identified mission completion, progress while stopped and optical/sonar steering conflicts; the lead owns their fixes and retests. Earlier worker names retain their routing identities despite changed production responsibilities.

Commands from project root: `powershell -File Inference/start.ps1`; `powershell -File Tools/Invoke-TrashSailDemo.ps1 -Command tests`; generation `-Command ocean` or `-Command setup`; rendered execution `-Command play` or configured `-Command suite`; evidence capture `-Command capture`; build `-Command build`. Run bridge commands sequentially against the existing editor. A command response passing is not automatically a behavioural scenario PASS.

Historical prototype checkpoint 7b2f197 remains unchanged. Production worktree changes are provisional until lead review and final evidence. Do not infer test completion from this registry or from worker handoff alone.
