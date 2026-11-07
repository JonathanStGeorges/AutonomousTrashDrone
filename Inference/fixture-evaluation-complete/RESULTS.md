# Actual rendered-fixture comparison

Input: `Evidence/TrashSailDemo/production/perception-fixtures-20261107-130422`, 27 original PNGs: 24 rubbish views (four assets, 3/5/8 m, 0/90 degrees), one fish, one boat, one empty view. These are tuning fixtures, not an independent holdout. Expected categories come from the evaluator manifest and never enter model inference. Counts below mean at least one expected canonical category appeared; they are **not bounding-box precision/recall**.

| Model/profile | Input size | Rubbish-category presence /24 | Semantically eligible /24 | Median CPU ms |
|---|---:|---:|---:|---:|
| YOLOE original | 416 | 0 | 0 | 77.7 |
| YOLOE original | 640 | 1 | 0 | 135.1 |
| Marine TRASH | 416 | 2 | 0 | 141.2 |
| Marine TRASH | 640 | 4 | 0 | 289.8 |
| FML | 416 | 4 | 0 | 54.0 |
| FML | 640 | 5 | 0 | 94.5 |
| YOLOE expanded plain prompts | 416 | 0 | 0 | 77.8 |
| YOLOE expanded plain prompts | 640 | 1 | 0 | 138.3 |
| YOLOE tailored asset-type prompts | 416 | 0 | 0 | 77.0 |
| YOLOE tailored asset-type prompts | 640 | 0 | 0 | 132.8 |

Expanded prompts add bottle/can/bag/trash/garbage to the original list. Tailored prompts are plastic jug, detergent bottle, water bottle, aluminum can, metal can, garbage bag, trash bag, plastic container plus the original wildlife/boat/person/buoy prompts. Each variant uses freshly loaded weights and prompt embeddings. The production service remains unchanged.

Important failures: YOLOE labels most trashbag views jellyfish (up to .948), cleaner bottles bird/person, and the fish itself bird (~.29). Thus the JSON `fishDetected` means only Wildlife category, **not correct fish classification**. The boat is missed as an obstacle and is sometimes wrongly labelled styrofoam/material trash. Marine specialist frequently reports shells/eel/ROV on rubbish. No model detected anything in the one empty fixture; one negative image is insufficient to characterize false-positive rates.

FML640 detects the two 3 m tin views and three trashbag views; it misses all gallon and detergent bottles and 19/24 rubbish views overall. All three non-rubbish fixtures give zero FML detections. Its single class is `garbage`, kept OtherRubbish with eligibility false. A generic garbage label must not bypass wildlife conflicts or physical suitability gates.

The checkpoint was downloaded only for this authorized local comparison from the official repository's linked Google Drive file `1gXY9K_txdq5PqqGNlP9yNqeY9QwAY9y8`. Size 22,549,859 bytes; SHA256 `bd7a544ce5536ed7130bb540f5e70f7b95a625c23539d472de46d862136af11b`; actual loaded map `{0: garbage}`. It is in `Inference/local-only/` and excluded from Git. Checkpoint licensing remains unresolved; no redistribution is intended. Dataset CC BY 4.0 is not asserted to cover the weights.

`comparison.json` preserves every original class, score, box, category and per-frame timing. Subfolders contain evaluator-only overlays with original-image boxes and estimated water-contact points/ranges where suitable. These PNGs are output evidence, never inference input. Use `compare_fixtures.py` to reproduce while Unity is stopped; `render_fixture_results.py` redraws stored results without inference.

Conclusion: neither prompt broadening nor the FML checkpoint resolves these fixtures sufficiently for reliable autonomous collection. Preserve the requested combined profile and conservative interlocks. Check rendering, visible texture/detail, object pixel sizes and independent later angles/seeds before deciding whether retraining or another detector is justified. Do not present this tuning exercise as operational collection success.
