# MARUS autonomous surface collector - production v2

The current demonstration uses **primary twin thrusters**, imported textured objects and **pretrained neural image inference only**. It runs in the installed MARUS 2 example project with Unity 6000.5.11f1/HDRP 17.5 on Windows. The original project name and TrashSailDemo paths remain for GUID/history stability; they do not imply active sail propulsion. ROS and paid assets are not required. A local Python inference service is required.

## Launch

Convenience launcher from the project root: `powershell -File Tools/Start-TrashOceanDemo.ps1`. It starts/verifies inference, checks the open editor, generates the ocean scene and enters Play Mode sequentially. Add `-Compact` for the compact scene. `-Player` requires the current Windows build at Builds/TrashOceanDemo/TrashOceanDemo.exe; its existence alone is not a build-validation claim. The explicit equivalent steps follow.

Open `C:/Users/jonat/Documents/projects/marus2-example` in the installed Unity editor, outside Play Mode. From that project root run these commands sequentially:

```powershell
powershell -File Inference/start.ps1
powershell -File Tools/Invoke-TrashSailDemo.ps1 -Command ocean
powershell -File Tools/Invoke-TrashSailDemo.ps1 -Command play
```

The service launcher verifies readiness and project ownership on localhost:8765. Its default interpreter is the existing marineCam virtual environment; see MODELS.md for pinned weights, class maps, licence conditions and logs. The ocean command creates/updates imported prefabs and `Assets/TrashSailDemo/Scenes/TrashOceanDemo.unity` using the existing editor. It preserves unsaved unrelated scenes before switching. Do not invoke concurrent editor operations or regenerate while playing.

Equivalent menus: **Trash Sail Demo / Create or Update Production Ocean**, then Play. To create the compact test environment use **Create or Update Compact Demo** or the bridge `-Command setup`; its scene is `Scenes/TrashSailDemo.unity`. Merely opening an older serialized scene does not guarantee its assets match current source: regenerate after an update.

Dashboard **Stop mission** closes the intake and idles thrusters while inertia and physics continue. **Pause time** freezes simulation; explicit Stop while paused cancels mission resumption. Start clears a latched sensor fault only after feeds are usable. Stop Editor Play Mode with `powershell -File Tools/Invoke-TrashSailDemo.ps1 -Command stop`. Stop this project's inference service with `powershell -File Inference/stop.ps1`.

## Watch and control

Default layout shows independent observer overview and chase, actual onboard RGB with model boxes, and operational sonar. Overview says **full simulated world**: its small GT dots are observer-only world records. T and H overlays are estimated RGB and sonar contacts. Unranged wildlife remains bearing-only and is not assigned an invented map position. Drag overview to pan, drag chase to orbit, scroll to zoom; maximize any view or reset to split. These controls do not modify the sensor camera.

Combined/YOLOE/TRASH buttons are display filters, not inference-profile or autonomy switches. Conflicting model evidence is retained. Thruster commands, applied forces, actuator health, requested speed/heading, intake and storage appear in telemetry. Controls retain seeded reset, manual/autonomous mode, sensor freezing and sonar noise.

## Scope and evidence

The large scenario defaults to a 1000 x 1000 m boundary, 350 rubbish records, 16 fish and five background boats, with nearby physical streaming. A favourable seeded arrangement places initial rubbish along the first lane; it does not move objects into the camera. Boundary-only coverage starts north along x0; collected-target diversions preserve the route index. Planned/travelled lines are not proof of observed area. At 6 m spacing the complete kilometre-square route is roughly 167 km, far beyond a short demonstration or the generated one-hour mission budget.

Use GATES.md for provisional production status. Runtime logs/screenshots/editor responses and raw camera PNG/JSON pairs live under `Evidence/TrashSailDemo`; service logs and model records live under `Inference`. Older evidence and Git checkpoint `7b2f197` describe the archived colour-heuristic/sailing prototype and must not be cited as pretrained/twin-thruster acceptance. New integrated rendered results remain lead-owned and pending until explicitly recorded.

See ARCHITECTURE.md, VEHICLE.md, CAD_REPLACEMENT.md, MODELS.md, ASSETS.md, SONAR.md and PRESENTATION.md. No claim is made of calibrated trash probabilities, validated fish recognition, acoustic scattering, vehicle hydrodynamics or safety certification.

Current test update (lead-reported, 2026-11-07): 36 Unity Edit Mode tests passed. This is separate from the production rendered acceptance suite, whose finalized outcomes remain lead-owned.
