# 11 ParkPilot Assist (photo of a real car park -> recommended manoeuvre)

Sources: `Vision/assist.py` (445 lines), `Vision/geometry.py` (154 lines), `Assets/Project/Scripts/ScenarioRunner.cs`
(917 lines), `Vision/layouts/*.json`, `Vision/README.md`. All checked on 2026-10-17.

## Purpose

The original project goal: a user gives a photo of any car park, the system finds which bays are free, builds that
car park in simulation, lets the trained RL agent park in the chosen bay and shows the manoeuvre on the photo. Assist
is the research and test tool for that pipeline on real photos (CNR-EXT camera 6). The Showcase (`12-Showcase.md`) is
the polished demo inside Unity and does not use Assist.

```
photo -> bays (4 corners each, from a layout file or marked by hand) -> occupancy CNN per bay
      -> homography photo -> ground metres -> Scenarios/scenario.json
      -> Unity ScenarioRunner rebuilds the car park, policy parks -> Scenarios/result.json + result.png
      -> path projected back onto the photo -> Scenarios/assist_<photo>.png
```

Python and Unity talk only through files in `Scenarios/` (the app polls the modification time of `result.json` or
`batch_summary.json`). There is no socket connection.

## Setup

| Part | What |
|---|---|
| Python | venv `C:\python-envs\sb3` (torch 2.2.2+cu121, torchvision 0.17.2+cu121, opencv-python 4.11, scipy 1.11.4. CUDA 12.1 build since 2026-10-14. Without an NVIDIA GPU, use the CPU builds of the same versions) |
| CNN weights | `Vision/models/occupancy_cnn.pt` (base CNRPark model, hard-coded in `assist.py` line 30, see `10-Occupancy-CNN.md`) |
| Unity | `Training.unity`, GameObject `ScenarioRunner` with the `ScenarioRunner` component. In the saved scene both the GameObject and the component are disabled. Enable both when using the app and disable them again afterwards. Model: `ExpFull/full3_s1` (assigned 2026-10-17, was `round6_s1` from 2026-10-15 and `round5_s1` before) |
| Photos | e.g. CNR-EXT full frames in `Data/cnrpark/full/` (git-ignored, deleted on the project PC on 2026-10-16, download again to use them) |

Run from the project folder:

```
C:\python-envs\sb3\Scripts\python.exe Vision/assist.py --image <photo.jpg> [--layout Vision/layouts/<camera>.json] [--bay-size 2.5 5.0]
```

Without `--layout` the app looks for `Vision/layouts/<photo file name>.json`. For CNR-EXT frames (named by time) that
file does not exist, so pass the camera's layout explicitly.

## Step 1: bays (`geometry.py`, `assist.py` `mark_bays`)

A bay is 4 image points in a fixed order: p0, p1 = the open end (aisle side, where the car drives in), p2 behind
p1, p3 behind p0.

- Marking (no layout file, or key M): click the 4 painted corners of a bay in any order. `order_quad` sorts them
  around their centre, takes the pair of shorter sides (in the image) as the ends, and picks as the open end the end
  nearer the image centre (in elevated views the aisle usually runs through the middle). F flips the open end of the
  last bay, U undoes, S saves. The first bay marked is the reference (shown with `*`).
- Layout files (`Vision/layouts/`): `{"reference", "bay_size", "bays": [[[x, y] x 4], ...]}` in image pixels.
  - `camera6_marked.json`: 8 bays marked by the user on camera 6 (front row 0-4, back row 5-7), reference bay 0.
    This is the reliable layout.
  - `camera6.json`: 19 bays estimated by hand from the image, reference bay 2. Its corners are inconsistent (aisle about
    3.5 m instead of about 8.7 m, spacing 2.8 m instead of 2.4 m) and it caused the misleading early camera-6 failures.
    Do not use it for conclusions.
- Only marked bays exist in the simulation. An unmarked bay with a car in it becomes empty ground, and the path may
  drive through it. Mark both neighbours of the target and the bays across the aisle.

## Step 2: occupancy (`assist.py` `classify`)

Each bay is cropped with `geometry.crop_box` (bounding box lifted 0.35 of its height, padded 10%, clamped to the image)
and classified by the base CNN: `p(occupied) > 0.5` means occupied. The photo is drawn with green (free), red
(occupied) and yellow (target) outlines, the thick edge marking the open end, and each label shows `index p(occupied)`.
Only free bays can be clicked as the target.

## Step 3: photo to metres (`ground_homography`)

The car park is assumed flat, so one homography H maps image pixels to ground metres.

1. `homography_from_reference`: the reference bay's 4 corners are mapped to a `width x length` rectangle (default
   2.5 x 5.0 m, from the layout or `--bay-size`). Ground frame: x along the reference bay's open end, z from its open
   end towards its back.
2. `align_to_reference`: for every other bay, the ends are re-chosen on the ground as the side pair that makes the
   bay parallel or anti-parallel to the reference (far rows are foreshortened, so their short sides in the image can be
   their long sides). Changed bays are written back to the layout file.
3. `refine_homography` (only with 3 or more bays): least squares (scipy, soft-L1 loss) over all bays, so each one
   maps as close as possible to a standard `width x length` rectangle with right angles, plus a soft prior (weight 10)
   that bays face the same or the opposite way as the reference, plus an anchor term keeping the reference bay in
   place. One bay alone gets the scale wrong far away from it (perspective). Set `parallel=0` for angled layouts.
4. `bay_rectangle`: each bay becomes centre (cx, cz), yaw (degrees, Unity convention: 0 = the parked car faces +z,
   positive towards +x), width and length.

Bays that come out wider than 0.8 x their length are reported (`sideways_bays`): almost always a wrongly chosen open
end. Right-click such a bay to flip it.

The ground frame is a mirror image of the car park. `order_quad` always stores the corners clockwise as seen in
the image, and the reference rectangle (0,0), (w,0), (w,l), (0,l) is anticlockwise seen from above. Checked
numerically on both camera-6 layouts (2026-10-11): the simulated car park is the photo's car park mirrored. The path
is projected back with the same H, so the drawing on the photo is correct, and a mirrored car park is still a valid
car park for a car whose steering is symmetric. But left and right in Unity output (batch start labels such as
"aisle left", traces, log statements like "enters left of centre") are swapped relative to the photo.

## Step 4: the scenario file (`build_scenario`)

Pressing Enter (interactive) or passing `--target` writes `Scenarios/scenario.json`:

| Field | Meaning |
|---|---|
| `bays` | list of `{id, cx, cz, yaw, width, length, occupied}` in ground metres. The target is never occupied |
| `target_index`, `start_mode` | target bay, `aisle` (key 1) or `facing` (key 2) |
| `metric_bay_width` | real width of the reference bay (for scaling when the target has no row neighbour) |
| `occupancy_mode` | `cnn`, or diagnostics `neighbours` / `none` (below) |
| `source_image`, `image_size`, `homography_image_to_ground` | for reference |
| `batch`, `trim_target`, `back_barriers`, `early_endings`, `trace` | only written by the non-interactive path (flags below). Missing fields take the Unity defaults |

Command-line options (non-interactive mode, used for testing and batch runs):

| Option | Effect |
|---|---|
| `--target N` | skip the window: classify, write the scenario, `photo_classified.png` and `layout_preview.png` (top-down layout in metres). Stops if bay N is classified occupied |
| `--start aisle\|facing` | start mode |
| `--wait` | wait for Unity, then save the result image |
| `--batch` | ask Unity for the batch test instead of one run |
| `--occupancy neighbours\|none` | diagnostic: keep only the target's direct neighbours' CNN state, or empty the car park |
| `--no-trim` | diagnostic: the target keeps its full measured length |
| `--back-barriers` | diagnostic: a 1 m high wall along the back of every bay |
| `--no-early-endings` | no stuck or no-progress endings, only the time limit (MaxStep 1500 = 30 s) |
| `--trace` | Unity writes one CSV row per decision |

## Step 5: Unity builds the car park (`ScenarioRunner`)

`Awake` configures the agent's inputs from the assigned model (`ParkingAgent.ConfigureFor`) unless a trainer is
connected. `Start` disables itself if a trainer is connected or no model is set, keeps only the first training area
(lowest z, then x) and disables the other 39, and sets the agent to inference with the model (deterministic by default).
Then `LoadAndRun` reads the scenario file. Key L reloads it, R replays, B runs the batch test.

Two kinds of scenario:
- Layout mode (`bays` present): the car park from the photo, below.
- Named mode (no `bays`): the training car park, `target` and `occupied` given as bay names (e.g. `Bay_N2_W2`),
  optional start pose or a seeded spawn stage. Used for checks in the asset's car park.

Layout mode (`BuildLayout`), in order:

1. Scale. `scale = 3.2 / pitch`, where `pitch` is the mean centre-to-centre distance from the target to its direct
   row neighbours (same facing within 20 degrees, 0.5 to 1.6 bay widths to the side, `scaleByBayPitch` on). Without a
   row neighbour it uses `metric_bay_width`, else the median bay width. Reason: the agent judges position partly from
   the parked cars its rays see, so the neighbours must sit at the trained 3.2 m spacing. The layout is
   centred on the area and the area's half extents are set to the layout plus `boundaryMargin` 8 m.
2. Bays. The area's `Bays` object is destroyed and rebuilt as `Bay_L00`, `Bay_L01`, ... (`ReplaceSpots`).
   - The target is trimmed to the trained length 4.88 m with its open end kept where the photo puts it
     (`trainingBayLength`, off with `--no-trim`), and narrowed to 3.2 m if measured wider (`clampTargetWidth`).
   - Other bays keep their full scaled length, so the aisle keeps its real width.
   - Parked cars sit at the trained depth (centred in the first 4.88 m from the open end) in every bay
     (`parkedCarsAtTrainedDepth`), so the rays see the bay end where the target's observations put it.
3. Floor and walls. The asset's painted ground (`AssestGround`) and its walls are hidden. A grey floor and four
   1.2 m walls (tagged Wall) are built around the layout. Optional back barriers.
4. Occupancy from the file, applied immediately so the start search sees the cars.
5. Start pose (`ChooseStart`), first valid candidate wins. A candidate is valid if the car is inside the area (2 m
   margin) and overlaps nothing.
   - `pose`: the start given in the file.
   - `aisle`: 7 m to each side of the target along its open-end line, facing along the aisle towards the bay, at a
     distance from the open ends of `min(3.4, max(middle of the free aisle, far side - 1.6))` (raycasts across the aisle
     to cars or walls), then at the middle of the free aisle. Both sides are tried.
   - Fallback: facing the bay, 4.5, 3.5 then 2.5 m in front of its open end.

   The banner warns when the measured aisle gap is under 5 m (simulation scale). Training aisles leave 6.6-7.3 m between
   parked cars.

Fixed bug (2026-10-11): `facing` used to start in the aisle. Until then the aisle candidates were added when
`start_mode == "aisle"` or when no candidate existed yet, which was always true for `facing`. They came first in the
list, so a `facing` scenario only started facing the bay if every aisle candidate was blocked. Now the aisle candidates are added only for `aisle` (and unknown modes), so `facing` uses the facing starts.
Single runs labelled "facing" before the fix were aisle starts. The batch test was never affected.

## Step 6: running and results

- The run uses `ParkingEnvironment.ActiveScenario` (`04-Environment.md`): every reset rebuilds exactly this scenario.
  Time scale 1 (watchable), early endings as in training unless switched off.
- The first 0-step "timeout" that ML-Agents reports when it starts its own first episode is ignored.
- At the end time stops, the final pose stays on screen, and Unity writes:
  - `Scenarios/result.png`: 1400 x 1400 orthographic top-down view with the path (blue) and start marker (orange).
  - `Scenarios/result.json`: outcome, success, seconds, final distance (converted back to the photo's metres),
    heading error, `nose-in` / `reverse-in`, target, scale, model, and `path_xz` in the scenario's frame (layout
    metres).
- The app then projects `path_xz` onto the photo with H^-1 (orange dot = start, yellow = end) and saves
  `Scenarios/assist_<photo>.png`: the photo with the path next to the simulation, with a one-line summary.

### Batch test (key B, or `--batch`)

The same scenario from up to 24 starts at time scale 20:
- along the aisle on both sides at 5, 6, 7, 8, 9 m (`batchLateral`), each at the preferred distance and 0.5 m closer to
  the target row (`batchStartOffsets` 0, -0.5): up to 20 starts
- facing the bay at 2.5, 3.5, 4.5, 5.5 m (`batchFacing`): up to 4 starts.

Starts that overlap a car or wall, or lie less than 2 m inside the area boundary, are skipped (`ValidStart`).
Output: `batch_result.csv` (one row per start), `batch_summary.json` (success rate, mean time, distance and heading of
the parked runs, outcome counts) and `result_batch.png` (green = parked, red = collision or out of bounds, orange = any
other outcome such as stuck, no progress or timeout). The app combines it with the photo into
`assist_batch_<photo>.png`.

### Trace (`--trace`)

One CSV row every 5 physics steps (= every decision) plus the final state: position, yaw, speed, steering, both
actions, the first 9 observations (bay-size and near-ray inputs of newer models are not written), the shaping terms
from `ParkingAgent.GetTrace()` and the 17 main rays as tag index and distance. Single run: `Scenarios/trace.csv`.
Batch: `Scenarios/traces/run_XX.csv` (old files deleted at batch start).

## Results so far

| Test | Model | Result |
|---|---|---|
| User-marked camera 6 (`camera6_marked.json`, 8 bays, target 0), batch, scale x1.34 | `finetune_occ_s3` | 24/24 parked, mean 4.4 s, 0.10 m from centre, 0.9 degrees (also the current `Scenarios/batch_summary.json`, 2026-10-10) |
| Same layout, single aisle start | `finetune_occ_s3` | parked in 4.7 s, 0.28 m, 1.4 degrees |
| Hand-estimated `camera6.json`, consistent scene | `finetune_occ_s3` | 0/17, caused by the inconsistent layout |

The adopted `full3_s1` is assigned since 2026-10-17 but has not been tested in Assist yet. Its
predecessor `round6_s1` (assigned 2026-10-15) was not tested in Assist either.

## Limitations

- Flat ground (one homography). Ramps and multi-level car parks are out of scope.
- Bays are marked by hand once per camera. All bays are assumed one standard size.
- The simulated car park is mirrored (above) and scaled so that the target row's bay spacing (or, without row
  neighbours, the bay width) is the training car park's 3.2 m, so the simulated car is about 1.3 times smaller than a
  real one relative to the bays. The output is a manoeuvre plan, not vehicle control.
- Nose-in perpendicular bays only. Angled and parallel bays are not supported by the agent.
- `Vision/README.md` was updated on 2026-10-15 (scaling by bay pitch, parked cars at trained depth, flags) and on
  2026-10-17 (model `full3_s1`).
