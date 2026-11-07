# Replacing the placeholder and moving toward hardware

## CAD replacement

Keep the `Trash Sail Drone — physics root` GameObject, Rigidbody, DemoVessel, own-state source, sensor adapters, collector and mission references stable. Replace only the contents of **Visual Model — replace with CAD** initially. Import FBX/OBJ at1 Unity unit=1metre; verify bow+Z/up+Y/starboard+X, model origin and measured overall dimensions. Remove imported drive scripts and colliders until deliberately configured.

Then rebuild the separate Collision Geometry using simple convex primitive/compound colliders suitable for a moving Rigidbody. Update vessel length/width/mass/centre of mass, buoyancy sampling points/force limits, drag, rudder authority and sail polar coefficients based on measured physical parameters. Reposition camera/sonar calibration mounts, gate/intake/capture volume and storage. Confirm the sonar origin remains ahead of hull colliders, submerged in calm water, with the intake inside coverage above minimum range. Re-run projection, own-hull/occlusion, capture, clearance, sailing and all acceptance scenarios after any geometry change.

The scene builder intentionally regenerates a known placeholder. Save a custom prefab variant or modify the factory/configuration to preserve a CAD variant when rerunning setup. Preserve .meta GUIDs for existing referenced assets. Do not change the physics root scale to compensate for a wrongly scaled mesh.

## Real sensors and inference

Replace ICameraFrameSource with a camera driver providing actual calibrated intrinsics, lens distortion correction, exposure/capture timestamp and immutable completed frames. Use hardware/monotonic clock synchronization and explicit latency/dropout health. Replace the synthetic colour detector with a trained detector/segmenter validated on local water, glare, rubbish and non-rubbish. Record model class coverage, licence, preprocessing and measured performance. Water-contact ranging needs real camera extrinsics, roll/pitch, water/wave estimates and uncertainty; consider stereo/depth/radar fusion where surface-plane intersection is inadequate.

Replace ISonarFrameSource with the actual sonar's polar intensity/calibration/timing representation. Validate mounting, beam pattern, minimum range, bottom/water-surface clutter, sound-speed assumptions, latency, multipath and ego-motion compensation. Unknown contacts must remain hazards. A fish classifier requires labelled acoustic training/evaluation; this demo supplies no validated fish classifier.

## Real control and collection

Replace IVehicleActuator with timestamped hardware commands plus watchdog, independently enforced emergency stop, verified feedback and safe gate state. Fit a measured sail polar/apparent-wind model, rudder hydrodynamics, leeway and wind/current estimation. Implement feasibility-aware path planning under currents, gusts and real turning/stopping limits. Test fail-safe recovery from no-go stalls and loss of positioning.

The intake needs a physical gate/deflector design, storage sensing, jam/force detection, bycatch sensing/release and independent interlocks. Simulation trigger entry is an engineering simplification; safe real collection cannot be inferred from these runs. Perform controlled water trials and hardware-in-loop testing before autonomous deployment.
