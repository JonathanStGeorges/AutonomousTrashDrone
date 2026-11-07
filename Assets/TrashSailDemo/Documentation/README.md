# Trash Sail / MARUS engineering demonstration

This project is an engineering proof of concept using the functioning installed **MARUS 2 example**, Unity **6000.5.11f1**, HDRP **17.5.0**, Windows and the existing MARUS submodules. The legacy `marus-core`/`marus-example` documentation is useful background but is not this installed API. No Unity upgrade, paid ocean asset, ROS service, model download or Python inference service is required.

## Launch

1. Open `C:/Users/jonat/Documents/projects/marus2-example` using Unity 6000.5.11f1.
2. Open `Assets/TrashSailDemo/Scenes/TrashSailDemo.unity` and press Play.
3. The initial scenario starts automatically. Use **Stop mission** to close intake/depower while physics continues; **Pause time** freezes the simulation. **Reset seed** rebuilds scenario actors and clears sensor tracks/capture state.
4. To regenerate the assets, leave Play Mode and select **Trash Sail Demo → Create or Update Demo**. This is idempotent: it replaces the dedicated demo scene/prefab rather than duplicating objects. Existing original scenes are not replaced. Unsaved original-scene changes are copied under `LocalPreservation` before switching.
5. **Trash Sail Demo → Run Acceptance Suite** runs rendered scenarios. **Window → General → Test Runner → EditMode** executes `TrashSailDemo.Tests`.
6. **Trash Sail Demo → Build Windows Player** creates `Builds/TrashSailDemo/TrashSailDemo.exe`. Build availability and actual test results are recorded separately in the final verification report.

Default collection mode is conspicuously **MOTOR ASSIST ENABLED**. Disable Motor assist for sailing experiments; the Upwind and LowWind scenarios explicitly disable the motor. Upwind starts at a declared55° close-hauled heading because a stationary unpowered rudder cannot pivot a vessel pointing directly into wind.

## Dashboard

The left view follows the physical vessel. The right shows actual rendered RGB with image-derived boxes and the operational MARUS sonar intensity image. Lower panels show estimated tracks/goal and telemetry. `T` means camera track; `H`/`U` means unknown sonar contact, not a recognised fish. Scores prefixed H are heuristic values, not calibrated probabilities. The evaluator scoreboard explicitly measures rubbish/bycatch/collisions and never feeds autonomy.

Controls include start/stop, pause, manual W/A/D, view switch, scenario cycle, seed, sensor freeze, overlay toggles, motor assist and sonar intensity noise. A sensor freeze leaves the last real image visible and visibly stale. Pause also shows wall-clock image age; simulation timestamps do not falsely advance while paused.

## Configuration and CAD

`DemoConfiguration.asset` contains dimensions, mass, centre of mass, intake width/capacity, forces, wind, sensor resolution/rates/noise/latency, safety margins and mission time budget. Play Mode clones this asset before modifying scenario settings. The prefab exposes separate Visual Model, Collision Geometry, sensor mounts, collector and Rigidbody root. See `VEHICLE.md` and `CAD_AND_HARDWARE.md`.

## Evidence and limits

Logs/screenshots/results are under `Evidence/TrashSailDemo`. `Documentation/VERIFICATION.md` records the final pass/fail/not-run status, software state and limitations; source compilation alone is not rendered validation. Do not use earlier interrupted-run screenshots as acceptance evidence. The demo is not validated sail hydrodynamics, real fish recognition, real acoustic scattering or a safety-certified controller.
