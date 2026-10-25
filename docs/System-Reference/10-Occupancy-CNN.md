# 10 Occupancy CNN (bay free or taken, from a camera photo)

Sources: `Vision/occupancy_cnn.py`, `Vision/train_occupancy_cnn.py`, `Vision/finetune_occupancy_unity.py`,
`Vision/export_occupancy_onnx.py`, `Vision/geometry.py` (`crop_box`), `Vision/assist.py` (`classify`),
`Assets/Project/Scripts/ShowcaseController.cs` (`RenderPhotoAndQuads`, `CropBox`, `GenerateSynthetic`, `ClassifyBays`,
`CnnCheck`), the metrics files in `Vision/models/` and `Scenarios/showcase/results.csv`. All checked on 2026-10-17.

## Purpose

The parking policy needs to know which bays are taken. In the product this comes from a single overhead photo
(a CCTV view of the car park): every marked bay is cropped from the photo and a small CNN calls it free or occupied. The
result decides which bays the user may pick and which bays get a parked car in the simulated scenario. The CNN does not
drive and is not part of the RL policy.

## Network (`Vision/occupancy_cnn.py`)

| Part | Detail |
|---|---|
| Input | RGB patch resized to 64 x 64, normalised per channel as (x - 0.5) / 0.5, i.e. values in [-1, 1] |
| Features | 3 blocks of conv 3x3 (padding 1) + ReLU + max-pool 2: 3 -> 32 -> 64 -> 128 channels, 64 -> 8 pixels |
| Classifier | flatten 8,192 -> FC 128 + ReLU -> dropout 0.3 -> FC 2 |
| Output | 2 logits, class 0 = free, class 1 = occupied |
| Size | 1,142,210 parameters (about 4.6 MB as `.pt`) |

`classify_crops(model, crops)` resizes and normalises each crop, takes the softmax and returns `p(occupied)` per crop and
the call `occupied = p > 0.5`.

## Data and training

### Base model (`Vision/train_occupancy_cnn.py`, writes `Vision/models/occupancy_cnn.pt`)

- Data: CNR-EXT patches (Amato et al. 2017, ODbL 1.0): real photos of one car park from cameras 1 to 9, on
  different days in sunny, overcast and rainy weather, in `Data/cnrpark/patches` with the dataset's own label files
  (`LABELS/train.txt`, `val.txt`, `test.txt`, checked: every split uses cameras 1 to 9 only. The older CNRPark cameras
  A and B listed in `CNRPark+EXT.csv` are not used). `Data/cnrpark` was deleted from the project PC on 2026-10-16 to
  save space (2.2 GB). The trained CNN (`occupancy_cnn.pt` and `.onnx`) is in git and unaffected. Retraining or
  fine-tuning the CNN needs the dataset downloaded again (see `Vision/README.md`). `Data/` is git-ignored, so each team
  member needs their own copy.
- Splits: the dataset's official day-based splits (no day appears in two splits, so the test images are not
  near-copies of training images): train 94,493, validation 18,647, test 31,825 patches.
- Training: random horizontal flip and colour jitter (brightness 0.3, contrast 0.3, saturation 0.2) on the training
  set, 6 epochs, Adam lr 1e-3, batch 256, cross-entropy. The checkpoint with the best validation accuracy is kept.
- Result (`occupancy_cnn_metrics.json`): best validation 98.78%, test 99.04%. Precision for "occupied"
  99.29%, recall 99.03%.

| Real test set (rows = truth, columns = call) | called free | called occupied |
|---|---|---|
| free | 13,419 | 130 |
| occupied | 177 | 18,099 |

### The domain gap

The base model scores 45.7% on photos of the Unity car park (`unity_val_acc_before`), below chance for a balanced
set. Unity renders (flat colours, box-shaped cars, clean lighting) look nothing like the real photos, so a fine-tune on
Unity images was needed for the showcase.

### Synthetic Unity data (Showcase, key G)

`ShowcaseController.GenerateSynthetic` (key G in the Arrive step, not shown in the UI) renders `syntheticLayouts` = 200
random variations of the showcase car park. For each one:

- occupancy share drawn from 0.1 to 0.9, every bay occupied with that probability
- each parked car gets a random colour (HSV: any hue, saturation 0 to 0.8, value 0.15 to 0.95)
- the CCTV camera moves by up to +/-8 m sideways, -8 to +6 m in height, +/-6 m in depth, looks at a jittered point near
  the centre, and its field of view changes by +/-10 degrees
- the directional light's intensity is scaled by 0.5 to 1.3 and its direction turned (+/-20 degrees pitch, +/-60
  degrees yaw)

Every bay that is fully inside the 1000 x 750 photo is cropped with the same box as the CNN uses at run time (below),
resampled to a 96 x 96 PNG and saved to `Data/unity_patches/free/` or `occupied/`. The label comes from the scene, so it
is always correct. `labels.txt` gets one line per patch: `relative-path label layout-number`. The camera and light are
restored afterwards. The car park is not: `Restart()` draws a new random occupancy, and the random car colours
(MaterialPropertyBlocks on the parked cars) stay until the scene is reloaded.

The current set: 5,366 patches from 200 layouts, 2,745 occupied and 2,621 free.

### Fine-tune (`Vision/finetune_occupancy_unity.py`, writes `Vision/models/occupancy_cnn_unity.pt`)

- Starts from `occupancy_cnn.pt`.
- Split by layout: every 7th layout (layout number divisible by 7) is held out, so validation patches come from
  layouts the model never saw, each a random re-render of the same Showcase car park: 4,605 train and 761
  validation patches.
- Training set = the Unity training patches plus 30,000 randomly chosen real training patches, so the model keeps
  its accuracy on real photos.
- Same augmentation, 4 epochs, Adam lr 3e-4, batch 256. No best-checkpoint selection: the weights after epoch 4 are kept.
- At the end it exports the ONNX file (next section) by running `export_occupancy_onnx.py` on the new weights.

Result (`occupancy_cnn_unity_metrics.json`):

| Set | Before | After |
|---|---|---|
| Unity validation (held-out layouts) | 45.7% | 99.6% |
| Real test set (CNRPark-EXT) | 99.04% | 98.88% |

| Unity validation (rows = truth) | called free | called occupied |
|---|---|---|
| free | 410 | 1 |
| occupied | 2 | 348 |

| Real test (rows = truth) | called free | called occupied |
|---|---|---|
| free | 13,406 | 143 |
| occupied | 215 | 18,061 |

The costly error is an occupied bay called free (row "occupied", column "called free"): the user could then choose
a bay with a car in it.

## Export to ONNX (`Vision/export_occupancy_onnx.py`)

```
C:\python-envs\sb3\Scripts\python.exe Vision/export_occupancy_onnx.py [weights.pt]
```

- Default weights: `Vision/models/occupancy_cnn.pt` (the base model). Pass `occupancy_cnn_unity.pt` to export the
  fine-tuned one (the fine-tune script does this itself).
- Writes `Assets/Project/Models/Vision/occupancy_cnn.onnx`, opset 15, input `input [batch, 3, 64, 64]`, output
  `logits [batch, 2]`, batch size dynamic.
- Checks the file with onnxruntime on 8 random inputs and stops if any logit differs from PyTorch by more than 1e-4.

The committed `occupancy_cnn.onnx` is the fine-tuned model (2026-10-11). Running the export script
without an argument would overwrite it with the base model, which fails on Unity images.

## Inference in Unity (Showcase)

1. Photo (`RenderPhotoAndQuads`): the `cctvCamera` renders a 1000 x 750 image. Each bay's four ground corners are
   projected into the image. A bay counts as visible only if all four corners are inside the image and in front of
   the camera. Hidden bays are not classified and cannot be chosen.
2. Crop (`CropBox`): the bounding box of the bay's four corners, widened by 10% of its width on each side, extended
   10% of its height downwards and 45% of its height upwards (cars stand above their ground footprint, so the box is
   lifted).
3. Resample and normalise (`ClassifyBays`): 64 x 64 sample points evenly spread over the crop, each read with
   bilinear filtering, converted to (x - 0.5) / 0.5 in channel-first order.
4. Run all visible bays in one batch with the Inference Engine (`Worker`, CPU backend), softmax of the two logits
   gives `p(occupied)`.
5. Decide: `p(occupied) >= occupiedThreshold` (0.5, Inspector) means taken (red), otherwise free (green).

If no `occupancyCnn` is assigned in the Inspector, the showcase uses the scene's true occupancy instead (p = 1 or 0)
and the status line says "ground truth, no CNN assigned".

### Confidence shown to the user

The hover tooltip on a bay says "Photo detection is X% confident this bay is free" (or taken). X is
`1 - p(occupied)` for a free call and `p(occupied)` for a taken call, so it is always at least 50%. It is the CNN's
softmax output for that photo, not a parking success rate and not a calibrated probability.

### Measured in the showcase

Every showcase run logs a CNN check against the scene's truth over the visible bays (`CnnCheck`, columns `cnn_visible`,
`cnn_correct`, `cnn_false_free`, `cnn_false_occupied`, `cnn_p_target` in `Scenarios/showcase/results.csv`). Over the
324 logged runs from 2026-10-11 to 2026-10-17 (all after the fine-tuned model was deployed): 9,666 of 9,720 bay calls
correct (99.4%), 0 taken bays called free, 54 free bays called taken. The 60 runs of 2026-10-17 had no wrong call.
These runs use the showcase's default camera and light, which are easier than the jittered training renders.

## Inference in the Python Assist app

`Vision/assist.py` crops each marked bay with `geometry.crop_box` and calls `classify_crops` (`classify`, line 72). It
loads `Vision/models/occupancy_cnn.pt`, the base model (line 30), because the app works on real photos, where the
base model is slightly better (99.04% vs 98.88% on the real test set). Details of the app are in `11-Assist-App.md`.

## Python and Unity crops are almost the same

| | Python (`geometry.crop_box` + torchvision) | Unity (`CropBox` + `ClassifyBays`) |
|---|---|---|
| Box | lift 0.35 h, then 10% padding on all sides (0.45 h above in total) | same box, written directly |
| Outside the image | box clamped to the image | not clamped, the texture's wrap mode supplies the pixels |
| Resize to 64 x 64 | PIL resize of the cut-out (antialiased) | 64 x 64 bilinear point samples |

The differences matter only near the image edge and for strong downscaling. The synthetic training patches were saved
with Unity's method at 96 x 96 and then resized by torchvision during training, so the fine-tune saw both.

## Known limits

- One fixed threshold (0.5) for both directions. Calling a taken bay free is the worse error and a higher threshold
  for "free" would trade false-taken calls for fewer false-free calls. This has not been tuned.
- The crop is an axis-aligned box in the image. For bays seen at a steep angle, or sideways bays, the box can include
  much of a neighbouring bay. The lifted box was designed for bays whose open end faces the camera.
- The Unity fine-tune data comes from the showcase car park only (its layout is fixed, only occupancy, colours, camera
  and light change) with one car shape. Generated car parks
  (`05-Generated-Car-Parks.md`) were not part of the CNN data.
- Softmax outputs of a network trained this way are usually over-confident. The percentage shown to users is not
  calibrated.
