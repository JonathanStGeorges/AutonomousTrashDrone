# ARCHIVED: vehicle-agent prototype review

The findings below are preserved verbatim as historical review evidence; their repair statuses reflect that earlier snapshot, not the current production worktree. Current primary-thruster/pretrained-inference contracts are in PRODUCTION_CONTRACTS.md. Production integration outcomes belong to GATES.md and the lead's latest timestamped evidence. The lead subsequently reported 36 Unity Edit Mode tests passing; no final rendered acceptance claim is made by this archive header.

---

# Independent vehicle-agent integration review

Reviewed camera, sonar, mission, runtime reset and evaluation source on 2026-11-07. This is source-level evidence; rendered acceptance and Unity test execution belong to the lead. No claim of rendered correctness follows from this review.

## Defects reported and repair status

- HIGH: the original collector rejected overspeed bodies after physical intake ingress, silently hiding fast bycatch. Repaired in DemoCollector: any body with its centre inside an active intake and available capacity is captured, with OverspeedCaptureCount recording unsafe entry. No truth-class filter. Added a fast-fish event/count/reset regression. Runtime and test sources compile with Unity's bundled Roslyn; Unity execution remains pending.
- HIGH: mission sonar-target association initially exempted a single nearby return without persistent or velocity-consistent association. Lead reports adding extents, velocity and persistence gates. Rendered ambiguous-target and crossing-contact tests remain required.
- HIGH: mission-budget RETURN_HOME could be overwritten by a selected-target approach, mixing home distance with target bearing. Lead reports clearing the target in the return-home branch.
- HIGH: stopping during the startup delay could be undone by the delayed StartMission coroutine. Lead reports Runtime.StopMission cancellation and dashboard routing repaired.
- MEDIUM: Runtime.Start modifies Physics.queriesHitTriggers globally. Preserve and restore the previous value on teardown to avoid changing unrelated simulator behaviour; repair status not independently verified.

## Sensor fidelity observations

Camera detections are derived from rendered pixels, not names, annotations or actor transforms. The camera adapter uses completed asynchronous readback, capture sequence/timestamps and a generation guard for reset. Overlay snapshots correspond to the processed frame. Bottom-left orientation defaults and near-intake projection must still be checked against rendered fixtures. Camera pose is estimated own state plus the calibrated mounting transform.

The sonar adapter consumes the MARUS sonar image after Texture2D updateCount advances. Degraded image intensities drive both operational extraction and display; raw annotation-bearing sonarData is not supplied to the tracker. Contacts remain unknown and age into increasing uncertainty.

Sonar acquisition timing remains approximate: the adapter estimates capture time one sample interval earlier while its pose history does not prove correspondence to the exact raycast schedule. Translation uncertainty is represented, but turning can add range-dependent angular error. The source normal/ray rotation repair improves intensity geometry but does not establish exact acquisition-pose alignment. Prefer a source completion event carrying capture pose/time or conservatively propagate angular uncertainty.

Sonar localization uses the source transform directly, so nonideal own-state noise/latency does not affect sonar pose in the same way as camera ranging. Declare that ideal pose approximation or route sonar localization through the same estimated-state interface.

At the default geometry the sonar origin z1.65 lies ahead of the hull bow z1.5, and its forward fan avoids the hull. This is geometry-dependent: verify own-hull occlusion again after any CAD or mount change. The source raycast layer handling is not a general hull-exclusion guarantee.

## Reset and acceptance evidence

Camera reset invalidates pending callbacks; sonar clears pending frames/tracks and ignores the initial source interval. These source paths avoid unbounded queues. Actual repeated resets must verify counters, fresh sequences, callbacks and actor cleanup together.

Lead reports replacing commanded tack counts with measured heading tack counts for sailing acceptance. Verify physical upwind progress and distinct port/starboard legs with motor disabled. A successful MetadataVariant mission alone does not prove decision equivalence with baseline: compare same-seed sensor/decision traces with documented physics tolerance. Separate successful repeated runs do not by themselves prove all reset state is cleared.

Evaluation uses hull/intake geometry for separation rather than vessel-centre distance. Crossing and underwater-only scenarios still require measured minimum clearance, collector state transitions, absence of bycatch/collisions and mission resumption. Source review cannot establish those outcomes.

## Verification performed

Standalone compilation of the vehicle runtime and NUnit test source using the installed Unity 6000.5.11f1 reference assemblies and bundled Roslyn completed with exit code 0 after the overspeed patch. No Unity process, rendered test or Edit Mode test runner was launched by this worker. All source edits were frozen before the lead's next rendered run.
