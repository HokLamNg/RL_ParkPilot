# 07 Training (configs, build, warm starts, monitoring)

Sources: `Config/*.yaml` (23 files), the run folders in `results/` (checked on 2026-10-17).

## Toolchain

| Part | Version |
|---|---|
| Unity ML-Agents package | 4.1.0 (`Packages/manifest.json`) |
| Python trainer | `mlagents` 1.1.0, `mlagents-envs` 1.1.0, torch 2.2.2+cu121 (CUDA 12.1 build), Python 3.10.11, in `C:\python-envs\mlagents`. Since the main round 6 run `mlagents-learn` trains on the GPU automatically (`--torch-device cpu` forces the CPU). Without an NVIDIA GPU, install the CPU build of torch 2.2.2 |
| Algorithm | PPO from ML-Agents (SAC for one comparison, DQN via Stable-Baselines3 in Experiment C and the project's own `DQN/rdqn.py` since round 6, `13-DQN-Baseline.md`) |

## How a training run works

1. Build the training player: in the editor, File > Build Profiles, Windows, with only
   `Assets/Project/Scenes/Training.unity` in the scene list, built to `Builds/ParkPilot/ParkPilot.exe` (default
   options). Rebuild after every script, prefab or scene change. The .exe does not pick up edits, and the build always
   uses the saved scene file.
2. Start the trainer from the project folder, one window per seed:
   ```
   mlagents-learn <config> --run-id=<name> --seed=<n> --env=Builds/ParkPilot/ParkPilot.exe --no-graphics --num-envs=4 --base-port=<port> [--initialize-from=<run>] [--env-args <flags>]
   ```
   - Each Unity instance holds the 40 training areas of `Training.unity`, so `--num-envs=4` gives 160 agents sharing
     one policy.
   - Several seeds run side by side with different `--base-port` values (5005, 5015, 5025 have been used).
   - `--env-args` must be the last option: everything after it is passed to the Unity executable. Used for the
     observation switches `--bay-size-obs` and `--near-rays` (`06-Agent.md`). Round 5, round 6 and the full run use
     both.
   - `--initialize-from=<run>` starts from another run's checkpoint (fine-tuning). `--resume` continues the same run
     (e.g. after a crash).
   - Each config's header comment gives the exact commands used for its runs.
3. ML-Agents writes `results/<run-id>/`: `ParkingAgent/` (checkpoints `ParkingAgent-<step>.pt/.onnx`, `checkpoint.pt`,
   TensorBoard events), `ParkingAgent.onnx` (the final model), `configuration.yaml`, `run_logs/`. On 2026-10-16 the
   intermediate `ParkingAgent-<step>.pt/.onnx` files of all finished runs were deleted to save space (12.6 GB). Kept:
   every TensorBoard log (all graphs can still be drawn), every final `ParkingAgent.onnx`, every `checkpoint.pt` (so any
   run can be resumed), the 21M checkpoints of `rdqn_s1-s3` (round 6b's warm start) and all files of the full run, which
   was still running then. Experiment C's SB3 checkpoints (`.zip`) were not affected. `results/` is git-ignored and exists only on this PC.
   - A forced stop (killing the trainer) does not write `run_logs/training_status.json`, so `--resume` restores the
     network and step count but restarts the curriculum at its first lesson (as in the pause of `full3`). Stop with
     Ctrl+C in the trainer's own window to keep the lesson.
   - Parallel runs are limited by Windows' memory commit limit, not by CPU or GPU: each CUDA trainer reserves 1-5 GB.
     Three full runs (12 Unity instances) used about 75 of 90 GB (64 GB RAM plus a 26 GB page file), and a fourth and
     fifth run failed to start with a `MemoryError`. A larger page file raises the limit.
4. The final `.onnx` is copied into `Assets/Project/Models/<folder>/` for evaluation and use (`09-Models.md`).

Unity runs at time scale 20 with no frame-rate cap (`engine_settings` in every config). Physics stays at 50 Hz
in simulated time.

## PPO settings

All PPO configs share: lambda 0.95, 3 epochs, network 2 x 256 with input normalisation. The configs up to round 5 also
share batch 1024, gamma 0.99 and time horizon 128, and so do the full run (third attempt) and stall tests B and C. The
round 6 configs (pilots and main run) and stall test A use batch 2048, gamma 0.995 and time horizon 256 (a far
drive is 200-400 decisions and the parking reward must reach back over it). They differ in:

| Config | lr (schedule) | entropy bonus (schedule) | clip (schedule) | buffer | steps | Environment parameters |
|---|---|---|---|---|---|---|
| `parking_ppo_stage1.yaml` | 3e-4 (linear) | 5e-3 | 0.2 | 10,240 | 0.5M | stage 0, shaped |
| `parking_ppo_stage1_sparse.yaml` | same | | | | 0.5M | stage 0, sparse |
| `parking_ppo_stage1_sparse_noend.yaml` | same | | | | 0.5M | stage 0, sparse, early endings off |
| `parking_ppo_stage3_shaped.yaml` | same | | | | 3M | stage 3, shaped |
| `parking_ppo_stage3_sparse.yaml` | same | | | | 3M | stage 3, sparse |
| `parking_ppo_stage3_sparse_noend.yaml` | same | | | | 3M | stage 3, sparse, early endings off |
| `parking_ppo_curriculum.yaml` | 3e-4 (constant) | 5e-3 | 0.2 | 10,240 | 64M | 11-lesson curriculum (development run) |
| `parking_ppo_final_curriculum.yaml` | 3e-4 (constant) | 5e-3 | 0.2 | 10,240 | 40M | 11-lesson curriculum, stages 0-10 |
| `parking_ppo_final_nocurric.yaml` | 3e-4 (constant) | 5e-3 | 0.2 | 10,240 | 40M | stage 10 from the start |
| `parking_ppo_finetune_geomrand.yaml` | 3e-4 | 5e-3 | 0.2 | 10,240 | 15M | stage 10, geometry randomisation |
| `parking_ppo_finetune_occrand.yaml` | 3e-4 | 5e-3 | 0.2 | 10,240 | 15M | + occupancy randomisation |
| `parking_ppo_finetune_proc.yaml` | 3e-4 | 5e-3 | 0.2 | 10,240 | 20M | + `procedural_layout` 0.5 |
| `parking_ppo_finetune_proc_cont.yaml` | 1e-4 | 1e-3 | 0.2 | 10,240 | 20M | same as above (continuation) |
| `parking_ppo_round4_bay.yaml` | 1e-4 | 1e-3 | 0.2 | 10,240 | 40M | layout curriculum, bay-size inputs |
| `parking_ppo_round5.yaml` | 1e-4 | 1e-3 | 0.1 | 40,960 | 40M | layout curriculum, hard cases, near rays, see below |
| `parking_ppo_round6_pilot_straight.yaml` | 1e-4 (constant) | 1e-3 (constant) | 0.1 (constant) | 40,960 | 10M | round 5's final conditions (stage 10, `geometry_share` 0.5, occupancy, `procedural_layout` 0.5, difficulty 2, `hard_case_share` 0.5, `stuck_steps_near_bay` 400) + `far_start_share` 0.4, `route_distance` 0 |
| `parking_ppo_round6_pilot_route.yaml` | same | | | | 10M | identical except `route_distance` 1 |
| `parking_ppo_round6.yaml` | 1e-4 (linear) | 1e-3 (linear) | 0.1 (constant) | 40,960 | 80M | as the route pilot: far starts 0.4, `route_distance` 1, `route_clearance` 0.3, `empty_bay_cost` 3 |
| `parking_ppo_full.yaml` | 3e-4 (linear) | 5e-3 (constant) | 0.2 (linear, ML-Agents decays it to 0.1) | 10,240 | 200M | 18-lesson curriculum from scratch, see below |
| `parking_ppo_fp_straight.yaml` | 3e-4 (constant) | 5e-3 (constant) | 0.2 (constant) | 40,960 | 20M | stall test A: stages 0-10 only (reward 3.1), from scratch with the bay-size and near-ray inputs, straight-line distance, round 6's batch, gamma and time horizon |
| `parking_ppo_fp_ppoA.yaml` | same | | | 10,240 | 20M | stall test B: as A, but route distance and Experiment A's batch, gamma and time horizon |
| `parking_ppo_fp_both.yaml` | same | | | 10,240 | 20M | stall test C: as A, but Experiment A's batch, gamma and time horizon (the control) |
| `parking_sac_stage1.yaml` | SAC: lr 3e-4, batch 256, buffer 500k, 10k initial steps, tau 0.005, 10 steps per update, initial entropy coefficient 0.5, horizon 64 | | | | 0.5M | stage 0, shaped |

Where no schedule is given for the entropy bonus and the clip, ML-Agents uses the learning-rate schedule (constant in
all configs between `parking_ppo_curriculum.yaml` and the round 6 pilots). Checkpoints: every 0.5M steps (80 kept) in
round 4 to round 6, every 2M steps (100 kept) in the full run and every 1M steps (20 kept) in the stall tests. Stall
tests A and C were stopped by hand at about 15.5M steps, B ran to its 20M limit.

### Stage curriculum (Experiment A)

`parking_ppo_final_curriculum.yaml` and `parking_ppo_curriculum.yaml` step `curriculum_stage` from 0 to 10 (the stages
in `04-Environment.md`). A lesson ends when the smoothed mean episode reward reaches 3.1 (`measure: reward`,
`signal_smoothing: true`), after at least 500 episodes (first lesson) or 1,000 (others). The no-curriculum counterpart
starts at stage 10.

### Layout curricula (rounds 4 and 5)

ML-Agents lessons can only advance on reward or on training progress. The reward mixes both kinds of car park, so these
use fixed progress thresholds (share of `max_steps`):

| Round 4 | 0-8M | 8-16M | 16-40M |
|---|---|---|---|
| `procedural_layout` | 0.25 | 0.4 | 0.5 |
| `procedural_difficulty` | 0 | 1 | 2 |

| Round 5 | 0-6M | 6-14M | 14-22M | 22-40M |
|---|---|---|---|---|
| `procedural_layout` | 0.1 | 0.25 | 0.4 | 0.5 |
| `procedural_difficulty` | 0 | 1 | 2 | 2 |
| `hard_case_share` | 0.25 | 0.35 | 0.5 | 0.5 |

Round 5 also sets `geometry_share` 0.5 and `stuck_steps_near_bay` 400 for the whole run. Round 6 (pilots and main run)
has no curriculum: all its environment parameters are fixed (table above).

### Full run curriculum (`parking_ppo_full.yaml`)

The full run trains the whole path of `round6_s1` (seven configs and about 200M steps of fine-tunes) in one run from
scratch, with the final inputs from step 0: bay-size and near-ray inputs (`--env-args --bay-size-obs --near-rays`, no
widening). Fixed for the whole run: `sparse_reward` 0, `route_clearance` 0.3, `empty_bay_cost` 3. The reward distance is
the straight line until G7 and the route along drivable space from G7 on (`route_distance` 0 then 1), as in the chain.
PPO settings are Experiment A's (batch 1024, buffer 10,240, gamma 0.99, time horizon 128): the first two attempts used
round 6's settings and stalled in stage 2 or 3, and three short tests showed those settings were the cause.

One curriculum of 18 lessons. Every other environment parameter has a lesson list with the same lesson names and
the same completion criteria, so all parameters move to the next lesson together. The stage lessons S0-S9 end on
Experiment A's reward threshold (smoothed reward 3.1, at least 1,000 episodes per lesson, where Experiment A's S0 needed
500). The averaging window differs from Experiment A's: ML-Agents sizes one reward buffer by the largest
`min_lesson_length` in the config and clears it at every lesson change, so each stage lesson here averages over all
its episodes up to 20,000, against the last 1,000 in Experiment A. From S10 on, lessons end at fixed
shares of the 200M steps (`measure: progress`, `min_lesson_length` 20,000 episodes), so a slow start shortens only the
earliest of these lessons: in `full3_s1` S10 lasted about 1.6M steps and G1 about 7M. A stage lesson can still stall
(`full3_s3`). The last lesson G7 has no completion criterion and runs to the end.

| Lesson | Ends at | `curriculum_stage` | `geometry_randomization` | `geometry_share` | `occupancy_randomization` | `procedural_layout` | `procedural_difficulty` | `hard_case_share` | `stuck_steps_near_bay` | `far_start_share` |
|---|---|---|---|---|---|---|---|---|---|---|
| S0-S9 (`S0_FixedBayNear` ... `S9_SharperTurnarounds`) | reward 3.1 | 0-9 | 0 | (1) | 0 | 0 | (2) | 0 | 0 | 0 |
| `S10_FullTask` | 0.3 (60M) | 10 | 0 | (1) | 0 | 0 | (2) | 0 | 0 | 0 |
| `G1_Geometry` | 0.35 (70M) | 10 | 1 | 1 | 0 | 0 | (2) | 0 | 0 | 0 |
| `G2_Occupancy` | 0.425 (85M) | 10 | 1 | 1 | 1 | 0 | (2) | 0 | 0 | 0 |
| `G3_GeneratedWide` | 0.475 (95M) | 10 | 1 | 0.5 | 1 | 0.1 | 0 | 0.25 | 400 | 0 |
| `G4_GeneratedWalls` | 0.525 (105M) | 10 | 1 | 0.5 | 1 | 0.25 | 1 | 0.35 | 400 | 0 |
| `G5_GeneratedFull` | 0.575 (115M) | 10 | 1 | 0.5 | 1 | 0.4 | 2 | 0.5 | 400 | 0 |
| `G6_GeneratedHalf` | 0.65 (130M) | 10 | 1 | 0.5 | 1 | 0.5 | 2 | 0.5 | 400 | 0 |
| `G7_FarStarts` | end (200M) | 10 | 1 | 0.5 | 1 | 0.5 | 2 | 0.5 | 400 | 0.4 (and `route_distance` 1) |

Values in brackets have no effect while the switch they belong to is off. What each lesson adds, and the earlier round
it repeats: G1 geometry randomisation (`finetune_geom`), G2 occupancy randomisation (`finetune_occ`), G3-G6 generated
car parks with round 5's shares, difficulty levels, hard cases, geometry share 0.5 and the 8 s stuck window near the bay
(round 5), G7 far starts in 40% of episodes and the route-distance reward (round 6). The thresholds after S10 are 30, 35,
42.5, 47.5, 52.5, 57.5 and 65% of the run (the first two attempts used 25 to 60%).

## Lineage of the trained models

Each fine-tune starts from the model named in its config header (`--initialize-from`):

```
final_curric_s3 (Experiment A, from scratch, 40M)
  -> finetune_geom_s1 (geometry randomisation, 15M)
     -> finetune_occ_s3 (+ occupancy randomisation, 15M)                [adopted until round 5]
        -> finetune_proc_s1-s3 (+ generated car parks, 20M)             [not adopted]
           -> finetune_proc2_s1-s3 (continuation, gentler, 20M)         [not adopted]
        -> finetune_occ_s3_bay (widened: +2 bay-size inputs)
           -> round4_bay_s1-s3 (40M)                                    [not adopted]
        -> finetune_occ_s3_r5 (widened: +2 bay-size inputs, +124 near-ray inputs)
           -> round5_s1-s3 (40M)                                        [round5_s1 adopted until round 6]
              round5_s1 -> r6pilot_straight, r6pilot_route (far starts, 10M) [route distance chosen]
                 r6pilot_route -> round6_s1-s3 (80M)                    [round6_s1 adopted until 2026-10-17]

full_s1-s3 (from scratch, entropy bonus 2e-3)                           [stopped: stalled in stage 3]
full2_s1-s3 (from scratch, entropy bonus 5e-3)                          [stopped: stalled in stage 2 or 3]
full3_s1-s3 (from scratch, 200M, Experiment A's PPO settings)           [full3_s1 adopted, s3 stopped in stage 4]
```

Round 3 and round 4 trained before the geometry-drift fix (`04-Environment.md`). Round 5 is the first round on the fixed
build. `full3_s1-s3` were started on 2026-10-16 to give the final PPO model from a single config (the first
two attempts, `full_s1-s3` and `full2_s1-s3` with round 6's PPO settings, stalled in stage 3 and were stopped). The rule
was to adopt one only if it matched or beat `round6_s1` on the five test sets of `08-Evaluation.md`, evaluated in the
same session. `full3_s1` did: better on generated car parks, the training car park and the
original set, level on both far-start sets, so it was adopted on 2026-10-17. `full3_s2` finished but is worse from far starts on generated car parks, and `full3_s3` was stopped at about 75M steps, still in stage 4. One config
from scratch can therefore produce the final model, but not reliably across seeds. The DQN models of round 6 and round 6b (`rdqn_s1-s3`, `rdqn2_s1-s3`) are documented in `13-DQN-Baseline.md`.

## Warm start with new inputs

`--initialize-from` needs the same network shapes, so a model cannot simply start from one with fewer inputs. Rounds 4
and 5 therefore started from a copy of `finetune_occ_s3` widened with the new inputs (`finetune_occ_s3_bay`,
`finetune_occ_s3_r5`), so that at step 0 the network computed exactly what the source computed:

- Checkpoint layout (policy and critic): first layer reads `[main rays 68 | vector 9 or 11 | near rays 124]`, in sensor
  name order. The vector and ray inputs each have their own normaliser.
- Bay-size inputs: the vector normaliser gets 2 entries (mean 0, variance 1/3) and the first layer 2 zero-weight columns.
- Near rays: a new normaliser (mean 0, variance 1/4) and 124 zero-weight columns.
- The optimiser state is left out (its moments have the old shapes), so ML-Agents starts it fresh and prints
  "Failed to load for module Optimizer:value_optimizer. Initializing" at the start of training. This is expected.
- Both widened models were checked by loading them into ML-Agents, exporting and comparing actions with onnxruntime
  (difference 0.0), and the round-4 step-0 model was evaluated in Unity episode for episode against `finetune_occ_s3`
  (identical in all 1,200 episodes).

The round 6 runs and the full run need no widening: the pilots start from `round5_s1`, which already has all three
inputs, and the full run starts from scratch with them.

## Monitoring during training

- TensorBoard: `tensorboard --logdir results` shows the `Parking/...` statistics (`06-Agent.md`), plus ML-Agents'
  own (`Environment/Cumulative Reward`, `Policy/Entropy`, losses, and `Environment/Lesson Number/<parameter>` for
  curricula).
- `docs/Training-Monitor.md` holds dated snapshots of these statistics: overall success on generated car parks and the
  training car park, collisions, stuck and entropy per window of steps (seed mean with the spread over seeds), the
  situation statistics (pillar beside the bay, narrow aisle, close start, hard case, neighbours, and since round 6 far
  start against near start on both kinds of car park) and the kinds of stuck and of collision obstacles.
- Long runs were checked every few minutes for crashes, NaN values, collapsing success, stalled curriculum lessons and
  collapsing entropy. On the stalled first full run the entropy check would have flagged the problem at about 14M
  steps, and the stall check flagged it at 23M.
- Training statistics are stochastic (exploration noise) and from training conditions. Adoption decisions use the
  deterministic evaluation (`08-Evaluation.md`).

### Training charts

The committed `Figures/round6_*.png` and `Figures/rdqn2_*.png` (2026-10-15) and the 14 charts `Figures/full3_s1_*.png`
of the adopted run (2026-10-17) plot each run's TensorBoard statistics in the style of the course notebooks: plain
matplotlib, one figure per statistic, one line per seed, curves smoothed with the Week 10 moving average
`np.convolve(y, np.ones((w,)) / w, mode="valid")` over 50 logged points. The curriculum phase is not smoothed.

| Runs | Statistics |
|---|---|
| all | `reward`, `episode_length`, `success`, `success_generated`, `success_training_car_park`, `collisions`, `stuck`, and when the run logged them `success_far_generated`, `success_far_training_car_park` (far starts) and `curriculum_lesson` (runs with an ML-Agents curriculum, such as the full run) |
| PPO | `policy_loss`, `value_loss`, `entropy`, `learning_rate` |
| DQN | `td_loss`, `q_value`, `curriculum_phase`, `near_greedy_success` |

## Practical notes

- Logged speeds with 4 environments per run. On the CPU (up to round 5 and the two round 6 pilots): about
  2,800-3,800 steps per second per run (one run alone is limited by the trainer at about 3,800, three side by side
  reach about 2,800-2,900 each), so 40M steps took about 3.5-4 hours with three seeds side by side,
  and the two pilots ran at about 3,300 steps per second each (side by side, 10M in 51 min). On the GPU (from the main
  round 6 run on): 80M steps in about 10 hours alongside DQN training. The full run `full3` ran at about 1,700 steps per second per seed with three
  seeds side by side and about 2,900 after `full3_s3` was stopped, and `full3_s1` and `s2` reached 200M about 24 hours
  after the resume. Stall tests A and C ran at about 2,650 steps per second side by side, B at
  about 3,500. Why three runs side by side were this much slower is not known.
- A run that stopped can continue with `--resume` (same run ID, without `--initialize-from`). After a forced stop the
  curriculum starts again at its first lesson (step 3 above). A policy that has already learned the early lessons
  passes them again quickly.
- The evaluation runner, scenario runner and showcase never change the agent's observation switches while a trainer is
  connected (they check `Academy.Instance.IsCommunicatorOn`), so a training build is not affected by them.
