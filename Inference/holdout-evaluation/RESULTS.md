# Fixed-profile lit holdout — seed 41

Source: `Evidence/TrashSailDemo/production/perception-holdout-20261107-132246`. Twenty-nine original rendered frames: 24 rubbish views at new distances/angles, one additional glare rubbish view, fish, boat, occluded rubbish and empty water. No prompt tuning was performed on this holdout. Expected fixture categories are evaluator truth only; model inputs are solely original PNG pixels.

| Model | Size | Rubbish-category presence /25 | Eligible /25 | Rubbish frames falsely labelled wildlife | Median CPU ms |
|---|---:|---:|---:|---:|---:|
| Original YOLOE | 416 | 0 | 0 | 13 | 81.2 |
| Original YOLOE | 640 | 1 | 0 | 14 | 153.9 |
| Marine specialist | 416 | 6 | 0 | 10 | 147.4 |
| Marine specialist | 640 | 6 | 0 | 11 | 293.2 |
| FML | 416 | 0 | 0 | 0 | 55.0 |
| FML | 640 | 0 | 0 | 0 | 97.3 |

These counts are category presence, not manually annotated box accuracy. Specialist material labels and FML garbage never grant eligibility. FML's tuning-set improvement did not generalize to these corrected lit views; production integration is unsupported.

Fish: YOLOE640 correctly returned fish .331; specialist416 returned animal_fish .498 plus animal_eel .312. The other model/resolution combinations missed it. Boat: YOLOE incorrectly returned styrofoam .298/.827; specialist returned rubbish material labels, and at416 animal_fish .480. Empty: every model returned zero detections. Occluded rubbish: YOLOE416 returned styrofoam .258; all others returned zero. This is a false positive on the occluded scene, not evidence of seeing the hidden object's identity through geometry. One empty scene cannot establish a general false-positive rate.

## Same-frame direct/service check

Actual lit frame `00-plastic_bottle_gallon-2.5m-45deg.png` passed exact PNG SHA256 and exact numeric detection equality. Both paths returned three records: YOLOE jellyfish .337, specialist trash_plastic .572 and animal_shells .536. All three were conflict-marked and ineligible, preserving the contradictory evidence. Wall inference: direct274.7ms, service324.6ms. This proves real transport/model equivalence for a nonempty frame; it does not prove correct semantic recognition.

`direct-service.json` preserves complete matching records and source hash. `comparison.json` preserves all fixed-profile results and timings; model subfolders contain 174 evaluator-only overlays with estimated contact geometry. No annotated output was reused as input. The live service remained unchanged and healthy after evaluation. Production camera source and the original marine webcam script were not edited.

Conclusion: the requested inference pipeline works, but this holdout fails reliable rubbish recognition/eligibility. Conservative wildlife/conflict gates appropriately block collection on these outputs. No autonomous collection success should be inferred from this evaluation.
