# Placeholder vessel, world actors and intake

The provisional vessel is a 3 m by 2.2 m catamaran, 110 kg, with a stable Rigidbody root. +Z is forward, +X is starboard, +Y is up; distances use metres and the timestep uses simulated seconds. `DemoWorld.BuildVessel` constructs separate visual, collision, sensor-mount and collection children. This is a configurable assumption for an engineering proof of concept, not a final hull design.

## Physical model

`DemoVessel` applies four-point spring/damper buoyancy around the calm-water equilibrium y=0, gravity, longitudinal/lateral drag and yaw drag. An optional wired MARUS `WaterHeightSampler` supplies surface height; the unwired default uses the same calm y=0 assumption. The upstream sampler currently returns zero without Crest. No paid ocean package is required. This model has inertia and finite turning/deceleration. It does not overwrite operational velocity or position; only explicit scenario reset clears motion and moves the vessel.

Sail thrust is an approximate true-wind polar: zero inside the configured 40-degree no-go cone, a smooth rise through the next 18 degrees, strongest on a beam reach, weaker running downwind. Force scales with trim and wind speed squared (5 m/s reference; bounded at 2x), and is zero below 0.5 m/s. This omits aerodynamic lift/drag decomposition, apparent-wind optimization, heel coupling, hydrodynamic validation and CFD. Rudder yaw torque depends on measured water speed, so a stationary vessel pointing directly upwind cannot pivot magically. The Upwind scenario starts at a documented 55-degree initial tack; subsequent tacking must come from the mission actuator commands.

Motor assistance is a separate force term, controlled by `Config.MotorAssistance`, and is ignored when disabled. `MotorUseSeconds`, `MotorDrive` and `SailDrive` expose actual use to logging/dashboard. At command expiry the collector closes and thrust depowers; momentum remains and drag slows the boat. The mission must steer a feasible avoidance path.

## Geometry and sensing

Twin hull colliders are separate from the visual primitives. Camera mount defaults to `(0, 1.65, 1.35)` at 32 degrees downward; sonar defaults to `(0, -0.38, 1.65)`. The intake is centred at `(0, -0.08, 1.8)` with configurable width, 0.9 m height and 0.8 m fore/aft depth. Its front edge is z=2.2, supporting the declared camera close-range blind-zone policy. Layer 8 is own hull, layer 9 underwater actors, layer 10 floating targets, layer 11 obstacles. Lead scene assembly owns the opaque water plane and boundary markers.

Trash visuals are controlled saturated orange/red bottle, can and packaging primitives for the pixel-detector baseline. Their real acoustic/interaction collider spans y=-0.35 to +0.25. Fish have real box colliders and scripted kinematic paths; crossing fish start at `(-4,-0.3,7)` and travel +X at a seeded speed of 0.62–0.68 m/s, crossing the centreline after approximately 5.9–6.5 simulated seconds. They reverse at +/-6 m and never react to boat position. The underwater-only hazard is at `(0,-0.7,6.2)`. The fixed obstacle is at `(7,0,17)`. Clear targets are at z=8,15,22. MetadataVariant changes names only. OccludedTarget puts a 4 m wide wall before the orange target and also introduces muted-orange uncertain packaging (RGB 180,140,110), intended to yield a low-score unknown pixel track.

## Physical collection and truth boundary

`DemoCollector` requires active actuator gating, remaining capacity and the external Rigidbody centre actually inside its trigger bounds. Normal collection policy also requires safe relative horizontal speed. If an unexpectedly fast body nevertheless enters the open intake, physics captures it and increments `OverspeedCaptureCount`; the evaluator must fail the unsafe event rather than silently let it pass through. Near misses and mere contact with the trigger's edge do not count. It deduplicates bodies, counts once and moves a captured body into the basket only after the event. It deliberately does not inspect tags, names or `DemoActor.Kind`: fish and unknown bodies can be captured, allowing bycatch evaluation to fail honestly. The body-reference `Captured` event is evaluator-only; the mission may read aggregate capture count, not identity. `DemoWorld` uses that physical event to suspend the captured actor's scripted movement.

Reset destroys previous scenario actors, including stored captures, clears collector deduplication/counts, resets vessel pose/velocity/commands, clears own-state latency buffers and reseeds scenario generation. Sensor and mission resets remain the integrator's responsibility.

## Own-state and CAD replacement

`DemoOwnState` is the explicitly simulated GNSS/IMU/wind boundary. Zero configured noise/latency is labelled ideal state. Optional uniform position/velocity/wind noise and heading perturbation use a seeded RNG; a bounded history applies latency. This is a simple debugging model, not calibrated sensor error.

Import future CAD in metres, apply the source unit conversion and place its mesh beneath `Visual Model — replace with CAD`, preserving the Rigidbody root. Keep +Z forward/+Y up and place the waterline at local y=0. Replace visual hulls/mast/sail but retain or rewire CameraMount, SonarMount, collection and actuator transforms. Update length, beam, mass, centre of mass, collision capsules/convex hulls, buoyancy points, drag, sail polar/area force scale, rudder torque, actuator limits, intake size and storage capacity. Re-run stability, clearance, sensing and capture tests after replacement; a prettier mesh does not validate physics.

## Validation status

`VehicleTests` covers no-go polar behavior, actuator clamp/expiry, capture gating and an actual intake fixture showing fish capture is possible and duplicate/reset counting is enforced. Runtime source and test source compile with the installed Unity 6000.5.11f1 reference assemblies and bundled Roslyn compiler (2026-11-07). The tests still require the lead's Unity EditMode execution. Rendered scenario behavior and quantitative sailing/collision evidence must come from the integrated run; this subsystem alone makes no end-to-end success claim.
