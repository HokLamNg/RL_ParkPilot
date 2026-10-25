# ParkPilot Assist: from a photo of any car park to a recommended parking manoeuvre

```
photo ─► bays (4 corners each, marked once per camera) ─► occupancy CNN ─► photo-to-ground homography (metres)
                                                                                    │
                                                                                    ▼
manoeuvre drawn on the photo ◄─ path in metres ◄─ Unity builds the car park, trained PPO agent parks (ScenarioRunner)
```

The app works on any flat car park photographed from above (a fixed camera, a drone, a phone held high). Nothing in it is tied to the Unity asset's car park.

## How it works

1. Bays. Each bay is marked by 4 clicks on its painted corners, in any order. The app sorts the corners, takes the two shorter sides as the ends, and guesses the open end (where a car drives in, the aisle side) as the end nearer the middle of the photo. The thick edge shows it: F flips it for the last marked bay, and right-click flips any bay later. A warning appears if a bay comes out wider than long (a wrongly chosen end). For every bay except the reference, the ends are then decided on the ground: the pair of opposite sides that makes the bay parallel (or anti-parallel) to the reference bay, as rows in a car park are. Far rows look shallow in perspective, so their shorter sides in the image can be their long sides. A fixed camera is marked once (`Vision/layouts/<camera>.json`), then every photo from it reuses the layout.
   - Mark every bay near the target: both neighbours, the bays beside those, and a few across the aisle. The simulated car park contains only marked bays. An unmarked bay with a car in it becomes empty ground, and the recommended path may drive through that car. The bays across the aisle also set the aisle width, which matters for the start position.
2. Occupancy CNN (`occupancy_cnn.py`). A small PyTorch CNN in the style of the Week 9 notebook (3 conv + pool layers, 2 fully connected layers, 64 × 64 RGB input, 1.1M parameters) labels each bay free or occupied. Each bay is cropped as the box around its 4 corners, extended upwards because cars stand above their footprint.
   - Trained on CNRPark-EXT patches (about 145k labelled bays, 9 cameras, sun, cloud and rain) with the dataset's own day-based split, so the test days are unseen.
   - Test accuracy 99.0% (31,825 bays). Occupied: precision 99.3%, recall 99.0%. Details: `models/occupancy_cnn_metrics.json`.
3. Photo to metres (`geometry.py`). One reference bay of known size (default 2.5 × 5.0 m, the first bay marked) gives a homography from the photo to the ground plane. A least-squares refinement then uses every marked bay, assuming all are the standard size, with a soft prior that rows are parallel (set `parallel=0` in `refine_homography` for angled or curved layouts). One bay alone fixes the scale poorly far away from it, because of perspective.
4. Scenario. The user clicks a free bay as the target and chooses the start: driving along the aisle, or facing the bay. `Scenarios/scenario.json` lists every bay in metres (centre, heading, width, length, occupied), the target and the start mode.
5. Unity (`Assets/Project/Scripts/ScenarioRunner.cs`, Training scene) rebuilds one training area as that car park at runtime: bays, parked cars where the CNN said occupied, floor and boundary walls. Then the trained policy parks deterministically.
   - Scaling. The layout is scaled so the target's row has the training bay spacing of 3.2 m: scale = 3.2 / pitch, where pitch is the mean centre-to-centre distance from the target to its direct neighbours in the same row (`scaleByBayPitch`, on by default and in the scene). Only if the target has no row neighbour is the reference bay width used instead (a 2.5 m bay then gives ×1.28). The agent judges its position partly from the parked cars its rays see, so the neighbours must sit at the trained spacing. The agent's inputs are all relative to the car and the target bay, so it does not depend on the asset's car park.
   - Bay length. Real bays are proportionally longer than the asset's (2.5 × 5.0 m vs 3.2 × 4.88 m). After scaling they would be 6.4 m long, an easier target than in training. The target bay is therefore trimmed to the trained 4.88 m, keeping its open end exactly where the photo puts it, so it has the shape the agent learned. All other bays keep their full length, so the aisle keeps its real width. Their parked cars are moved to the trained depth, centred in the first 4.88 m from the open end (`parkedCarsAtTrainedDepth`, on by default and in the scene), so the rays see the end of the bay where the target's own observations put it.
   - Bay width. A target bay measured wider than the training width (3.2 m after scaling) is narrowed to it, centred on the measured bay (`clampTargetWidth`). Real bays are a standard width, so the extra is marking error. A too-wide bay let the agent stop inside it while 11 to 15° crooked. Narrower measured bays are kept as they are.
   - Start pose (inside the training distribution). *Along the aisle*: 7 m to one side of the bay, driving towards it, 3.4 m out from the bays' open ends (the middle of a training aisle). It only moves closer when the far side of the aisle (parked car or wall, measured by raycasts) is nearer than 3.4 + 1.6 m, and never closer than the middle of the free aisle. Both sides are tried. *Facing the bay*: 2.5 to 4.5 m in front of it.
   - Narrow aisles. Training aisles are 6.8 m between the bays' open ends (central aisle, 6.1 m for the side aisles), which leaves 6.6 to 7.3 m between parked cars (simulation scale, about 5.3 m real at the 2.5 m-bay scale). If the photo's aisle is narrower than 5 m at simulation scale, the Unity banner says so: the agent was not trained for it.
6. Result. Unity saves `Scenarios/result.png` (top-down view with the path) and `Scenarios/result.json` (outcome, time, final distance in the photo's metres, heading error, path in metres). The app projects the path back onto the photo and saves `Scenarios/assist_<photo>.png`: the photo with the manoeuvre next to the simulation.

## Setup (once)

- Python: the `sb3` venv (`DQN/requirements.txt`) with torchvision 0.17.2 and scipy 1.11.4 added.
- Unity: the Training scene already contains a GameObject `ScenarioRunner` with the ScenarioRunner component. Both the GameObject and the component are saved disabled, so enable both when using the app and disable them again afterwards, like the EvaluationRunner. Model is set to `Assets/Project/Models/ExpFull/full3_s1.onnx` (the adopted policy since 2026-10-17, not yet tested in Assist. Its inputs are detected automatically). It also switches itself off if a trainer is connected.
- Retraining the CNN (optional): `Data/cnrpark` was deleted on the project PC on 2026-10-16 to save space, the trained CNN in git is unaffected. To retrain, download `CNR-EXT-Patches-150x150.zip` from https://github.com/fabiocarrara/deep-parking/releases/tag/archive, unzip it into `Data/cnrpark/patches/` (git-ignored), then run `C:\python-envs\sb3\Scripts\python.exe Vision/train_occupancy_cnn.py` (about 10 minutes on CPU).
- CNN for the Unity showcase: in `Showcase.unity` press Play, then G (renders 200 random car parks and saves labelled bay crops to `Data/unity_patches/`). Then run `C:\python-envs\sb3\Scripts\python.exe Vision/finetune_occupancy_unity.py` (about 4 minutes). It fine-tunes on Unity plus real patches and re-exports `Assets/Project/Models/Vision/occupancy_cnn.onnx`. Result: 99.6% on held-out Unity layouts, 98.88% on real photos.

## Use

Run everything from the project folder.

```powershell
cd "C:\Game Dev\AIRL Learning\Assets testing"
C:\python-envs\sb3\Scripts\python.exe Vision/assist.py --image <photo.jpg> [--layout Vision/layouts/<camera>.json] [--bay-size 2.5 5.0]
```

| Key / mouse | Action |
|---|---|
| Click × 4 (marking mode) | Corners of one bay, any order. `F` flip the open end of the last bay, `U` undo, `S` save the layout |
| Right-click a bay | Flip its open end |
| Click a green bay | Choose the target |
| `1` / `2` | Start driving along the aisle / facing the bay |
| `Enter` | Write the scenario with Unity's defaults (CNN occupancy, single run, target trimmed, early endings on, no trace). Then press Play in Unity, or L if it is already playing |
| `M` | Re-mark the bays |
| `Q` or `Esc` | Quit (in marking mode and when choosing the target) |

In Unity: R replays the manoeuvre, L reloads the scenario after the app wrote a new one, B runs the batch test.

### Batch test: how reliable is the recommendation for this car park?

One run is one sample (even the adopted model fails a few percent of full-task episodes that start near the bay, 3.2% on the training car park with random occupancy and 5.2% on generated car parks in simulation). The batch test runs the same scenario from about 24 starts at 20× speed, about a minute in total:
- along the aisle on both sides of the bay, 5 to 9 m away, at the preferred distance from the open ends and 0.5 m closer to the target row (`batchStartOffsets`, starts further out leave no room to swing in narrow aisles)
- facing the bay, 2.5 to 5.5 m in front of it.

Starts that would overlap a parked car or wall, or lie less than 2 m inside the area boundary, are skipped. It writes `Scenarios/batch_result.csv` (one row per start), `Scenarios/batch_summary.json` (success rate, mean time, distance and heading error of the parked runs, outcome counts) and `Scenarios/result_batch.png` (every path: green = parked, red = collision or out of bounds, orange = any other outcome, including stuck, no progress and timeout).

```powershell
C:\python-envs\sb3\Scripts\python.exe Vision/assist.py --image <photo.jpg> --layout <layout.json> --target 3 --batch --wait
```

Then press Play in Unity. The app saves `Scenarios/assist_batch_<photo>.png`: the classified photo next to all the paths, with the success rate.

Diagnostic `--occupancy`: `cnn` (default) uses the CNN's occupancy. `neighbours` keeps only the target's direct neighbours (same row, same facing, beside it) and empties every other bay. `none` empties the whole car park. Comparing batch tests across these shows how much the surrounding parked cars affect the agent.

Diagnostic `--back-barriers`: a 1 m barrier (tagged like a wall) along the back edge of every bay, at its full real length, as a kerb with a fence would be. It is 1 m high because the ray sensors only see objects at their height. Diagnostic `--no-trim`: the target bay keeps its full measured length. `--no-early-endings`: switches off the stuck and no-progress endings (training shortcuts), so the agent gets the full 30 s time limit. Diagnostic `--trace`: Unity writes one CSV row per decision (position, speed, actions, the first 9 observations, shaping terms and rays) to `Scenarios/trace.csv`, or `Scenarios/traces/run_XX.csv` per start in a batch test.

`--occupancy`, `--no-trim`, `--back-barriers`, `--no-early-endings`, `--trace` and `--batch` only take effect in non-interactive mode (with `--target`). Pressing Enter in the window always writes a scenario with Unity's defaults.

Non-interactive (testing): `--target <bay index> --start aisle|facing [--wait]`. It writes the scenario, `Scenarios/photo_classified.png` and `Scenarios/layout_preview.png` (top-down layout in metres). `--wait` waits for Unity and saves the result image.

Example (CNR-EXT camera 6, after downloading the full frames into `Data/cnrpark/full/`, which are not on the project PC since 2026-10-16):

```powershell
C:\python-envs\sb3\Scripts\python.exe Vision/assist.py --image Data/cnrpark/full/FULL_IMAGE_1000x750/SUNNY/2016-01-12/camera6/2016-01-12_1150.jpg --layout Vision/layouts/camera6_marked.json --target 3 --start aisle --wait
```

## Limitations

- Mirrored simulation. Bay corners are stored clockwise as seen in the photo, and the ground frame reads them as anticlockwise, so the simulated car park is the mirror image of the photographed one. The path drawn back on the photo is correct (the same homography maps it back), but "left" and "right" in Unity's output (batch start labels, traces) are swapped relative to the photo. Details: `docs/System-Reference/11-Assist-App.md`.
- Start "facing the bay" only works since 2026-10-11. Before that, `ScenarioRunner` also added the aisle starts for it, and they won whenever one fitted.

- Flat ground is assumed (one homography). Ramps or multi-level car parks are out of scope.
- Bay marking is manual, once per camera. Automatic line detection is future work.
- Geometry comes from bays of one standard size. Mixed sizes (disabled bays, motorbike bays) are mapped with some error.
- The agent was trained on nose-in bays with aisles about 6.8 m wide (simulation scale). Much narrower aisles, parallel-parking spaces or angled bays have not been tested.
- The simulation is a stand-in for the real car, a kinematic car of the trained size. The recommendation is a manoeuvre plan, not vehicle control.

## Data credit

CNRPark+EXT: G. Amato, F. Carrara, F. Falchi, C. Gennaro, C. Meghini, C. Vairo, "Deep learning for decentralized parking lot occupancy detection", *Expert Systems with Applications*, 72, 2017. Licence: Open Database License (ODbL) 1.0. http://cnrpark.it/
