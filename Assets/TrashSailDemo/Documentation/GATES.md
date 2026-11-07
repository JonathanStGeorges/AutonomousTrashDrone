# Verification gates - production v2

Status snapshot: documentation refresh during lead-owned Unity verification, 2026-11-07. This file deliberately does not manufacture new rendered acceptance results. Consult timestamped editor responses, test XML, scenario JSON/JSONL, source PNG/model JSON and screenshots under Evidence/TrashSailDemo, plus Inference records. The lead must append exact current-run outcomes after verification.

| Gate | Status | Evidence / remaining requirement |
|---|---|---|
| Existing host and v2 contracts | PASS (inspection) | Installed MARUS2/Unity6000.5.11f1/HDRP17.5; PRODUCTION_CONTRACTS.md and inspected local source. This does not establish current final build success. |
| Coverage runtime and test-source compilation | PASS (source only) | Bundled Roslyn with installed Unity references compiled CoveragePlanner and CoverageTests after boundary-inset repair. Unity test execution is a separate gate. |
| Pretrained service/fixtures | PROVISIONAL | Worker A records actual weights/classes/hashes and service fixtures in MODELS.md. Blank/timing fixtures are not positive Unity detection or collection evidence. |
| Imported assets/persistent ocean | PROVISIONAL | Worker B provides local files/hashes/licences and code; native import, waterlines, materials and runtime streaming need integrated evidence. |
| Primary-thruster sensor-to-physical-collection loop | NOT YET RECORDED | Requires actual neural detections from onboard frames, fresh sonar gating, actuator forces and physical intake events in the current configuration. |
| Wildlife/unknown/occluded/conflicting hazards | NOT YET RECORDED | Requires collector closure, meaningful clearance against full envelope, no bycatch/collision, no invented optical range and safe resumption. |
| Sensor/model faults and reset | NOT YET RECORDED | Requires stale/dropout/service-fault interlocks, successful empty-result distinction, no cross-session callbacks, reset counters/tracks/records and pause-stop semantics. |
| Coverage and presentation | PROVISIONAL | Source provides saved coverage index, full-world labelled observer, separate render textures and display-only filters. Verify physical route following, turn margin and camera-frame continuity during view changes. |
| Performance and reproducibility | NOT YET RECORDED | Measure actual rendering/inference/sonar rates and latency, seeds, interventions, duration and reset repetitions; no 30 FPS claim follows from compilation. |
| Standalone build/current final delivery | NOT YET RECORDED | Lead-owned build, launch and evidence review remain required. |

No current gate is marked BLOCKED without a concrete failed command/blocker. 'Not yet recorded' means this documentation writer has no finalized current acceptance result, not that the lead's active run has failed. Preserve any actual failures and limitations in the final report; do not relax safety criteria to turn a run green.

## Historical prototype

Checkpoint 7b2f197 and older evidence contain colour-heuristic, motor-assisted and simplified sail experiments. They are useful historical regression/debugging records only. Their capture counts, tacking, timing and screenshots must not be relabelled as production pretrained-inference or twin-thruster validation. Retained v1 contracts and review documents are historical where they conflict with PRODUCTION_CONTRACTS.md.
