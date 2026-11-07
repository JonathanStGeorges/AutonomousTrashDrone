# CAD and hardware integration - production v2

Use [CAD_REPLACEMENT.md](CAD_REPLACEMENT.md) for the current step-by-step model replacement procedure. Keep the Rigidbody root, sensor/control contracts and collector boundary stable while replacing the imported visual proxy. Import in metres with bow +Z, up +Y and starboard +X; preserve existing metadata/GUIDs. Update geometry, mass, centre of mass, buoyancy/drag, collision/swept envelopes and calibrated sensor mounts before validating again. Save a custom prefab variant or update the factory intentionally so regeneration does not erase a CAD integration.

## Actuation

Production uses primary port/starboard thrusters. Replace IVehicleActuator with timestamped measured hardware interfaces, calibrated mount axes/moment arms, thrust-response curves, reverse limitations, saturation, watchdog feedback and an independently enforceable emergency stop. Validate surge/yaw mixer signs and stopping/turning envelopes under currents and disturbances. Legacy sail fields carry no production control authority; earlier sailing instructions are historical.

## Perception and sensors

The production path already uses pretrained neural models, not a colour baseline. Existing weights are an engineering starting point, not a validated deployed rubbish/wildlife system. Preserve model/source provenance, licence obligations, meaningful negative results, disagreements and fail-closed stale/error handling. Train/evaluate with representative real rubbish, glare, fouling, weather and non-targets rather than treating synthetic rendered success as transfer validation.

Real camera adapters require calibrated intrinsics/extrinsics, distortion correction, capture timestamps, synchronized attitude and explicit latency/health. Surface-plane ranging is approximate; use suitable additional ranging where it is inadequate. Real sonar needs beam/range calibration, acquisition timing, sound-speed correction, clutter/multipath handling and ego-motion compensation. Unknown contacts remain hazards; the demo has no validated acoustic fish classifier.

The intake requires actual gate/deflector dynamics, storage sensing, jam/force detection, bycatch detection/release and independent interlocks. Simulation centre-in-trigger capture is deliberately simplified. Controlled water trials and hardware-in-loop testing must validate capture and failure behaviour before autonomy is deployed.

The lead reports 36 current Unity Edit Mode tests passed; this does not validate physical hardware or establish the final rendered acceptance result. See GATES.md and the lead's evidence report.
