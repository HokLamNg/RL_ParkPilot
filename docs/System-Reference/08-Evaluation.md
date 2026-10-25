# 08 Evaluation (`EvaluationRunner`, test sets, analysis scripts)

Sources: `Assets/Project/Scripts/EvaluationRunner.cs` (315 lines), `Evaluation/`, `Figures/` (checked on 2026-10-17).

## Purpose

Training statistics are noisy (exploration) and come from changing training conditions. Every model decision in this
project is made on a deterministic evaluation on fixed, reproducible test layouts: every model and baseline drives
exactly the same car parks from exactly the same start poses, so results can be compared episode by episode.

## How to run

1. In `Training.unity`, enable the GameObject with the `EvaluationRunner` (keep it disabled while training, it also
   switches itself off if a trainer is connected).
2. Set the model (or a Model Queue of several models, each evaluated in turn on the same layouts, the file name
   becomes the label), the mode and the test set (below).
3. Press Play with no trainer running. It runs at time scale 20 and stops Play mode when done (`quitWhenDone`).

One session cannot mix models with different inputs (9-input, 11-input bay-size, 11 + 124 near-ray models). The
runner configures the agents' observation switches from the first model in `Awake`, before the agents are enabled, and
logs an error if the queue mixes input signatures (lines 87-97).

## Settings

| Setting | Default | Meaning |
|---|---|---|
| `mode` | Policy | `Policy` (a model), `Random` or `RuleBased` (the agent's heuristic baselines, `06-Agent.md`) |
| `model`, `modelQueue` | | `.onnx` model(s) |
| `deterministic` | on | use the policy's mean action |
| `actionNoise` | (0, 0) | evaluation with sampled actions: Gaussian noise (steer, throttle std) added to the mean action, redrawn each decision. Unity's own stochastic mode does not resample, so use this instead, with `deterministic` on |
| `stages` | 4, 6, 10 | curriculum stages to test, in order |
| `episodesPerArea` | 10 | layouts recorded per area per stage |
| `seedBase` | 100000 | layout seeds = `seedBase + stage x 1000 + area index` |
| `geometryRandomization`, `occupancyRandomization`, `proceduralLayouts` | off | which test set (below). Occupancy randomisation parks 0-27 cars on the training car park and fills 0-90% of the bays of a generated car park (`04-Environment.md`) |
| `farStarts` | off | round 6: every episode starts far from the bay, anywhere in the aisles (10-40 m route distance, any heading, on the training car park partly at the showcase's centre hand-over pose), seeded like the layouts, with the 60 s limit of far starts (`06-Agent.md`). Off = the usual starts in front of the bay |
| `timeScale`, `quitWhenDone`, `outputFolder` | 20, on, `Evaluation` | |

With 40 areas: 400 episodes per stage, 1,200 per evaluation (3 stages). Stage 4 targets only inner-row bays, stage 6
inner and outer rows, stage 10 any bay, so corner and sideways bays appear only in stage 10.

## How identical layouts are guaranteed (lines 120-215)

- Areas are ordered by position (z, then x), so area *i* is always the same area.
- At the start of each stage every area is forced to that stage (`stageOverride`), its test-set switches are set
  (including `farStartOverride`), and its random generator is reseeded with `seedBase + stage x 1000 + i` (`ParkingEnvironment.Reseed`).
- Each area's episodes are counted by `LayoutIndex` (1, 2, 3, ... after the reseed). Exactly layouts 1 to
  `episodesPerArea` are recorded. Episodes that began before the latest reseed carry an older `SeedEpoch` and are ignored.
- Features that are off draw no random numbers (`04-Environment.md`), so adding a feature does not change existing test
  layouts.

Repeatability caveat. Runs at time scale 20 are not always exactly repeatable. Re-running a model can change a
small number of outcomes: round 4 reproduced all 1,200 outcomes exactly across sessions, while `finetune_occ_s3` varied
between 802 and 817 parked on the generated set across sessions. In round 6 the effect was larger:
`round6_s1` evaluated in two sessions on the same day parked in one session but not the other in 25 to 74 of the 1,200
episodes per set (the ending changed in 25 to 94),
with more stuck endings in the second session and success 0.6 to 1.8 points lower. The layouts are identical
and the stuck and no-progress endings count physics steps, so the cause is not known yet (possibly inference or physics
differences between sessions, the first session shared the machine with DQN training). Only compare models evaluated
in the same session (put them in one Model Queue), and treat differences of a few episodes as noise.

## Test sets used in the project

| Set | Switches | File suffix | Used for |
|---|---|---|---|
| Original | all off | (none) | every model since Experiment A |
| Geometry | `geometryRandomization` | `_geomrand` | geometry fine-tune |
| Training car park + occupancy | `occupancyRandomization` | `_occrand` | occupancy and later rounds |
| Generated car parks + occupancy | `occupancyRandomization` + `proceduralLayouts` | `_occrand_proc` | rounds 3-6 (generator at full difficulty), the 95% target of round 6 |
| Far starts, training car park | `occupancyRandomization` + `farStarts` | `_occrand_far` | round 6 |
| Far starts, generated car parks | `occupancyRandomization` + `proceduralLayouts` + `farStarts` | `_occrand_proc_far` | round 6 |

Round 6 (the pilots, `round6_s1-s3` and the DQN models `rdqn2_s1-s3`) was evaluated on five sets: original,
training car park + occupancy, generated car parks + occupancy and the two far-start sets. The
full run (`full3_s1`, `full3_s2`) was compared with `round6_s1` and `rdqn2_s2` on the same five sets in one session on
2026-10-17 (files `Evaluation/*_20261017-*`).

The adoption rule since round 3: a new model must be better on generated car parks without losing on the training
car park and the original set. Round 6: generated car parks at least 95% as the
target, far starts and the showcase centre batch reported, no loss on the original set, the training car park set or the
aisle hand-over batch. `round6_s1` was adopted at 94.2% on generated car parks (best or level-best on every set, all
three seeds better than `round5_s1` on every set). From round 6b on, all models compared for adoption must come from the
same evaluation session (see the repeatability caveat above). The full run had to match or beat `round6_s1` on all
five sets. In the 2026-10-17 session `full3_s1` reached 94.8% on generated car parks (the closest
to the 95% target so far, still short of it), was better than `round6_s1` on generated car parks, the training car
park and the original set (paired comparison) and level on both far-start sets, so it was adopted. `round6_s1` reproduced its 2026-10-15 afternoon results in all but one episode
(far starts on generated car parks, 80.6% against 80.7%).

## Output (one CSV and one summary per model and set)

File name: `Evaluation/<label>_<mode>[_stochastic][_noise<s>-<t>][_geomrand][_occrand][_proc][_far]_<yyyymmdd-hhmmss>.csv`
and `..._summary.md`.

CSV columns (lines 152-155, 217-238), one row per episode:

| Column(s) | Meaning |
|---|---|
| `label, mode, deterministic, stage, area, area_episode` | which model, set position |
| `outcome, success, steps, seconds` | `Parked`, `Collision`, `Stuck`, `NoProgress`, `Timeout`, `OutOfBounds`. Time = steps x 0.02 s |
| `target_bay, group, occupied_neighbours` | target name, bay group, 0-2 |
| `spawn_heading_deg, spawn_lateral_m, spawn_distance_m, spawn_fallback` | start pose relative to the bay (`04-Environment.md`) |
| `approach, turn_before_entry_deg, finish_direction, entries, entry_speed` | how it drove in (`06-Agent.md`) |
| `final_distance_m, final_heading_err_deg, soft_contacts, reward, collided_with` | end state |
| `bay_width_m, bay_length_m, aisle_width_m, pillar_beside, wall_behind, row_end` | target bay layout (aisle and layout flags only on generated car parks) |
| `occupied_bays, total_bays, neighbour_left, neighbour_right` | how full the car park was, which neighbour was occupied |
| `spawn_far, spawn_route_m` | round 6: far start (1 / 0) and route distance from the start to the bay (m, -1 when no route map was built) |

The summary states the seeds and which test-set switches were on (with far starts: "Far starts ON (anywhere in the
aisles, 10-40 m route distance, 60 s limit)"), then gives, per stage: success, collision, stuck, no-progress, timeout
and out-of-bounds rates, mean time to park, mean final distance and heading error over all episodes (failures
included), and success by bay group.

## Statistics used in the analyses

- Paired comparison: because two models drive the same episodes, compare episode by episode: count the episodes
  only model A parks and only model B parks.
- Logistic regression of failure on situation factors (start distance, neighbours, bearing of the bay, aisle width,
  bay size, pillar, wall, occupancy), reported as odds ratios per standard deviation with 95% intervals.

## Figures and failure tables

| Figures | Made from | Content |
|---|---|---|
| `Figures/expA_*`, `expB_*`, `expD_*` | TensorBoard logs in `results/`, evaluation CSVs | Experiment A, B and the algorithm comparison |
| `Figures/fail_distance_*` | the 2026-10-11 evaluations of `finetune_occ_s3`, `round4_bay_s1-s3` and `round5_s1-s3` on the two occupancy sets (training car park and generated car parks) | failure by start distance, neighbours, bearing, side offset, bay group |
| `Figures/fail_aisle_width.png` | the same models' generated-car-park evaluations (CSVs with layout columns) | failure by aisle width. The failure tables also cover pillar, wall, row end, bay size and occupancy |

Notes:
- The charts follow the style of the course notebooks (plain matplotlib, one figure with axis labels, title and legend,
  learning curves smoothed with a moving average, training curves as the mean over the 3 seeds). They were
  regenerated this way on 2026-10-15. The older images are in `Figures/archive/`. Training charts per run are
  described in `07-Training.md`.
- Experiment A's figures use only the Experiment A session (2026-10-05). `expA_evaluation.png` and
  `expA_stage10_breakdown.png` were regenerated on 2026-10-20, because they had mixed in a re-evaluation of
  `expA_curric_s3` from 2026-10-10 (curriculum mean on stage 10: 84.2% instead of 83.7%).
- "Bearing of the bay" (in the failure analyses) is derived: the angle between the car's nose and the direction to the
  bay centre at the start (0° = bay dead ahead, 180° = behind).
