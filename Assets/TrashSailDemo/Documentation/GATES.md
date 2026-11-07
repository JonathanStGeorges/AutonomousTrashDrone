# Production verification gates — 2026-11-07

This demonstration is runnable, but successful neural autonomous collection has failed acceptance. Healthy inference is not proof of correct recognition. Historical heuristic/sailing results at checkpoint 7b2f197 are excluded from production claims.

| Phase | Status | Actual evidence |
|---|---|---|
| 1 — Installation/contracts | PASS | Existing MARUS2 host preserved; Unity6000.5.11f1/HDRP17.5; contract v2 and coordinated ownership. |
| 2 — Real inference | PASS technically; FAIL recognition | Both requested pretrained weights hashed and loaded; live unannotated onboard frames; exact nonempty direct/service comparison. Held-out images produced zero eligible rubbish targets. |
| 3 — Imported assets | PASS | Fish, four debris models and MARUS boat proxy; rendered fixtures, material/UV audit, normalized bounds and 25 verified source hashes. |
| 4 — Ocean/presentation | PASS | Seed17 1km square/350 rubbish/16 fish/5 boats; persistent records; independent observer/chase/RGB/sonar; camera identity/calibration/profile preserved through five view modes. |
| 5 — Autonomous collection | FAIL | Actual 300.057s at timescale1: zero captures, zero bycatch/collisions, 15 avoidances. Search-to-diversion-to-capture-to-resume remains unverified. No target was fabricated. |
| 6 — Safety/verification | PARTIAL | 36 Edit Mode tests pass. Seven physical thruster cases, physical coverage turn, no-wind navigation and sensor-failure checks pass. Fish-correlated sonar avoidance passes. Confirmed-target interruption and ambiguous association are BLOCKED by the absent eligible neural target. |

The original intake-sonar geometry failed four of five rendered near-field cases. The revised .15m submerged mount with33 vertical rays passed all five near-field cases. Windows build and a rendered Direct3D11 player smoke passed; the first attempt failed under system-memory pressure before the service-thread repair. Final large-world performance is measured in large-performance-final.json; manual control begins at31.97s, so autonomous and manual intervals are separated.

Evidence: Evidence/TrashSailDemo/production, Unity test XML/JSON, and Inference/holdout-evaluation. Run IDs distinguish COMPLETE from INTERRUPTED reports. The obsolete ambiguous-association PASS in acceptance-before-approach-audit.json had no confirmed target; use causal-hazard-acceptance-results.json and the tightened evaluator. Incomplete reports are not successful acceptance.

Read INDEPENDENT_REVIEW.md, MODELS.md and the final verification report. The next collection dependency is a detector validated on representative surface-camera imagery, including wildlife/boat negatives and held-out data. Relaxing conflict gates would not establish success.