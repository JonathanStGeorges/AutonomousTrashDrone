# Operational contracts v1

Unity metres/seconds. World +Y up; vessel +Z forward/+X starboard. Bearings are degrees clockwise/starboard from sensor forward; XZ estimates discard unobserved vertical position. WindTo is the direction air travels; upwind is its negative. Timestamp clock is simulation `Time.timeAsDouble`; health compares capture time, never a generic hasData flag. Display wall-clock age separately while paused.

CameraFrame owns a completed, bottom-left row-major Color32 buffer, capture-time calibration/pose, sequence and capture/completion times. One in-flight GPU request and one pending latency slot maximum; reset invalidates callbacks by generation. Detection overlays always refer to the retained completed frame. Bounding rectangles use that frame's bottom-left pixel coordinates. RGB heuristics are synthetic-demo scores, not probabilities. Range uses bottom/contact pixels and estimated water plane; reject horizon rays.

SonarFrame owns a range-major intensity image and capture pose/coverage. Operational contacts extract ONLY from this degraded image. No collider, class, instance, annotation or true elevation in frames/tracks. Sonar is approximate simultaneous multibeam, not validated acoustics. Noise/dropout/latency affect displayed and operational data identically. Unknown contacts persist with growing uncertainty.

VisualTrack and ContactTrack IDs are local perception IDs. Positions/velocities are estimates; Age is time since actual observation. Collection association is narrow and ambiguous/multiple matches must block. Mission only consumes these tracks, frame health and VehicleState. Scenario truth is allowed only in world creation, simulated sensing, physical interaction and evaluator scoring.

VehicleState comes through IOwnStateSource; zero noise/latency is explicitly IDEAL OWN-STATE mode. ActuatorCommand is bounded rudder/sail/motor/collector, with a timestamp and watchdog. Physics only integrates Rigidbody forces/torques; reset may reposition as a labelled reset operation. Motor assistance is conspicuously labelled and logged. No global object lists or true target transforms enter autonomy.

Collector may capture any external dynamic body physically crossing an active intake; it must not filter truth types. Evaluator alone labels bycatch versus rubbish. Autonomy receives aggregate physical-capture notification, not body identity. Failure/invalid frames never open collector. Reset clears retained frames, tracks, pending callback generations, commands, capture deduplication and counters.
