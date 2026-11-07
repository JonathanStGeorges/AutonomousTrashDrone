# Production dashboard

Current implementation and controls are documented in [PRESENTATION.md](PRESENTATION.md); [README.md](README.md) gives launch instructions. Default split shows independently rendered observer overview and chase, actual onboard neural RGB, and operational sonar. Maximize/reset/pan/orbit/zoom do not alter the onboard capture camera or active model profile.

The overview explicitly says **full simulated world**. Its optional GT dots read persistent world records for the observer only; T/H observations and planned/travelled routes are separate. Unranged optical observations remain on the RGB image and are counted, never assigned fictitious map coordinates. Route rasterization clips to panel bounds. A travelled route does not establish observed-area coverage.

Model overlays retain label, source provenance, score and conflicts. Combined/YOLOE/TRASH display filters never suppress safety inputs or change inference computation. The telemetry shows primary port/starboard commands, applied forces, requested speed/heading, actuator health, mission reason, target, collection/fill, evaluator outcomes, FPS and inference timing. There are no sail-only, motor-assistance or heuristic-confidence controls in the current dashboard.

Start and Stop call DemoRuntime lifecycle methods. Stop closes collection and idles the physical thrusters; momentum persists. Pause freezes simulation; explicit Stop while paused cancels automatic mission resumption. Manual control remains collection-interlocked. Seed reset reconstructs the selected scenario and clears perception/mission state. Sensor freeze and sonar-noise controls affect real feed health and operational degraded representation. Captured/bycatch/collision scoring is observer-only and never feeds planning.

The lead reports 36 production Unity Edit Mode tests passing. Rendered control usability, matching overlays, view independence and meaningful behavioural outcomes remain distinct acceptance checks; source tests do not prove them. Preserve timestamped screenshot/log evidence for the actual production configuration.
