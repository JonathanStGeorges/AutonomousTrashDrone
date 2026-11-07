# Independent production review - 2026-11-07

Reviewer: worker C (/root/vehicle), independent of lead-owned mission/actuation and worker A's neural implementation. Read-only source/evidence review during the active Unity suite. No Unity commands, inference requests or runtime source edits were performed for this review. This is an evidence snapshot, not a final successful-delivery certificate.

## Four earlier findings: repairs verified in current source

1. `DemoMission.ChooseAvoidance` now immediately returns the requested direction when no sonar contacts exist. The previous infinity-score/first-candidate trap is removed.
2. Prediction now uses configured port/starboard directions and moment arms, current applied-force state, bounded response, yaw rate/inertia and drag. It no longer assumes both thrusters always face bow. This remains a coarse discrete predictor rather than a physically validated collision guarantee.
3. Collection eligibility receives the magnitude of estimated vessel velocity minus selected-target velocity. It no longer checks vessel speed alone.
4. Post-capture verification uses a verifying flag through hazard processing rather than returning before avoidance. Target acquisition/collection remain inhibited while contact memory clears; hazard response remains available.

Previously repaired source paths also remain present: coverage does not advance in idle/manual mode; completion returns home and stops within the arrival gate; explicit Stop while paused cancels restart intent; bearing-only optical warnings retain an already chosen sonar avoidance manoeuvre.

Generated scene navigation freshness is CameraMaxAge=1.2 s, explicitly reflecting measured CPU latency. `MayCollect` still requires target age <0.8 s. The relaxed navigation age therefore does not relax the final intake observation-age gate. Camera failures remain latched until restart and stale/mismatched results do not refresh operational tracks. This is source evidence, supplemented by the latest failure scenarios below.

## Ground-truth separation audit

Searches of mission, coverage, camera and sonar operational source found no population `.Records`, world `.Actors`, ClassId or InstanceId input path. Camera detections originate from raw rendered PNG model results, not imported-object identity or annotations. Sonar operational tracking uses degraded intensity, not annotation-bearing raw sonar identities. Own-plant applied forces/inertia in avoidance prediction are permitted plant parameters, not hidden target truth.

`DemoPresentation` reads `OceanPopulation.Records` only to draw explicitly labelled full-simulated-world observer markers. The coverage planner reads boundary parameters and estimated own state; model import and streaming remain simulation-side responsibilities. Camera/sonar observations remain distinct from observer truth. This source audit does not replace a successful metadata-renaming decision-equivalence test.

## Evidence read from files

- `Evidence/TrashSailDemo/editmode-results.json`: **36 passed, 0 failed, 0 skipped**. The reviewer inspected the actual JSON; the lead executed the tests.
- `Evidence/TrashSailDemo/production/production-acceptance-before-20261107-133735447-3ef68d.json`: seven recorded ThrusterBench PASS cases (forward, left, right, reverse, coast, watchdog, stopping), CoverageTurns PASS and UnderwaterHazard PASS. Coverage fixture reports a 20 x 16 m region, 4 m lanes, 1 m turns, measured heading change 130.7157 degrees and waypoint9. These are bounded component/scenario results, not evidence of complete kilometre-area coverage or rubbish collection.
- Current `production-acceptance-results.json` at inspection: runId **20261107-133735447-3ef68d**, source SHA256 **e0607628b3f0aabdb83d43ac68e4be87c5acc89c1f48e28c077a13d171676b95**, status **RUNNING**, active scenario ClearCollection. CameraFailure and SonarFailure report PASS, collector closed/degraded with no collision. Their measured means are approximately 87/90 FPS, 2.60/2.73 camera Hz and 7.45/7.30 sonar Hz, specific to those runs.
- In that current run, PerceptionHoldout PASS explicitly means **acquisition only: 29/29 real frames**, with eight expected canonical-category observations. It does not mean accurate eligible rubbish recognition.
- `Inference/holdout-evaluation/RESULTS.md` evaluates the earlier independent lit seed41 holdout (25 rubbish views plus fish/boat/occluded/empty). All tested model/resolution combinations report **0/25 eligible rubbish**. Original YOLOE gives 0/25 or1/25 rubbish-category presence; the specialist6/25; offline FML0/25 at both sizes. Wrong wildlife/material labels and conflicts are preserved. FML is not supported for production integration by this evidence.
- The same holdout report records exact same-PNG direct/service detection equality on a nonempty actual frame. The matching outputs are jellyfish, plastic and shells, all conflicting/ineligible. This validates transport/model equivalence, not semantic accuracy.

## Remaining material failure and claim limits

**The current pretrained perception does not provide reliable eligible rubbish targets on the evaluated scene imagery.** This is measured failure, not a hypothetical risk. Conservative conflicts/eligibility gates correctly prevent collection from these outputs; they must not be weakened or fed truth to manufacture success. The lead must report failed/not-demonstrated end-to-end physical rubbish collection until a current rendered run supports it.

The suite was still RUNNING at review. Do not treat a stale `production-suite-complete.txt`, an earlier inconclusive acquisition suite, old prototype evidence, or an individual acquisition PASS as an all-green final suite. The newer 29-frame acquisition and older fixed-profile semantic holdout are different evidence sets and must retain their timestamps. Read the finalized runId-matched result when the lead completes it.

No additional demonstrated controller defect was established in this bounded review after the four repairs. That statement is limited to inspected source and available evidence; it is not proof of every avoidance geometry, real-world safety, detector accuracy or all requested acceptance criteria.

## Actual agent handoffs and review separation

- Lead: contracts/configuration, mission integration, twin-thruster dynamics, collection, scene/build assembly and serialized Unity acceptance. Owns final claims and current perception blocker disclosure.
- Worker A (/root/camera): actual pretrained model service, camera protocol/tracking, provenance/conflicts, raw-frame/direct comparison and semantic holdout evidence. Its own fixture success was not accepted as collection success.
- Worker B (/root/sonar, reassigned): imported licensed models/textures, manifests and persistent ocean streaming; earlier sonar groundwork retained. Simulation/observer truth is kept out of operational planning.
- Worker C (/root/vehicle, reassigned): coverage/presentation, independent controller review, pause-stop correction, clipped route drawing and current documentation. Reviewed lead and camera work without editing their runtime source in this pass.

Worker handoffs are provisional until integrated evidence supports the relevant claim. The independent review supports functioning sensor transport, measured actuator/coverage/failure subsets and an explicit unresolved perception-accuracy failure; it does not endorse completed autonomous rubbish collection.
