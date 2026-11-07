# Reproduce the inference service

The default deliberately reuses the working interpreter at `C:/Users/jonat/Downloads/marineCam/.venv/Scripts/python.exe`, cached assets in `Downloads/marineCam`, and the normal Hugging Face cache. It does not assume a new checkout contains these dependencies.

```powershell
powershell -NoProfile -File Inference/setup.ps1
powershell -NoProfile -File Inference/start.ps1
```

`setup.ps1` verifies the explicitly selected executable, all 51 pinned packages in `environment.lock.txt`, the tested CLIP source-archive hash, both official Ultralytics assets and the specialist at its pinned HF revision. Missing weights are downloaded from the exact recorded official URLs and verified by SHA256. Existing mismatched weights fail verification and are preserved. It does not change packages by default. `setup-report.json` records the interpreter, package versions and model paths/hashes. The default verification passed on 2026-11-07 with 51 packages and three assets.

For a new **isolated** environment only, explicitly use:

```powershell
powershell -NoProfile -File Inference/setup.ps1 -Python 'C:/path/to/python.exe' -CreateEnvironment -EnvironmentPath 'C:/path/to/new-project-venv'
powershell -NoProfile -File Inference/start.ps1 -Python 'C:/path/to/new-project-venv/Scripts/python.exe'
```

This optional branch installs pinned requirements into a new environment and refuses an existing environment directory. It was not exercised in this session; default existing-environment verification was exercised. Availability of the exact pinned wheels/archive on another platform is a separate reproducibility constraint. No unconstrained dependency upgrade or CUDA conversion occurs.

`start.ps1` creates a hidden persistent process, waits up to 45 seconds for readiness and records its project root/PID. It is idempotent for an already healthy service. `stop.ps1` verifies both the persisted PID and live project identity before requesting shutdown. `/health` must report `ok:true` before Unity starts. The service verifies all three weight hashes, including the specialist. The service was stopped and restarted through these scoped scripts after that verification change; all requested models warmed successfully.

Raw-frame comparisons and the initial 27-fixture tuning report are in `fixture-evaluation-complete/RESULTS.md`. Those fixtures showed poor recognition and false wildlife labels; they are not proof of successful collection. The fixed-profile lit holdout is complete: `holdout-evaluation/RESULTS.md` records 29 frames (seed41), zero eligible rubbish targets, frequent false wildlife labels, and FML zero rubbish hits at both416/640. Exact direct/service comparison on lit positive-output PNG `00-plastic_bottle_gallon-2.5m-45deg.png` matched all three numeric detections and imagehash; all three were conflict-marked and ineligible. Category-presence counts are not class correctness or box accuracy. FML remains an offline-only comparison artifact under `local-only/`; its checkpoint is excluded from Git and is not loaded by production. The original requested two-model prompt profile remains intact.

The service now uses a single HTTP/inference OS thread, matching Unity's one-inflight contract; health may wait for the current inference. This avoids creating per-request native Torch allocator pools. See `memory-benchmark.json` when present for the measured repeated-request resource check.
