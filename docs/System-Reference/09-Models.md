# 09 Models (inventory, inputs, lineage, which one is in use)

Source: `Assets/Project/Models/` (input sizes read from every `.onnx` file with onnxruntime on 2026-10-11, the round 6
files checked again on 2026-10-15, `ExpFull` added 2026-10-17) and the configs (`07-Training.md`). Training output lives
in `results/<run-id>/` (git-ignored). The files below are copies of each run's final `ParkingAgent.onnx` (DQN: exported
by its trainer, `13-DQN-Baseline.md`).

## The models in use

`ExpFull/full3_s1.onnx` (adopted 2026-10-17): the parking policy for the showcase (assigned in
`Showcase.unity`) and the model for the Assist app. Trained from scratch with
one config, `Config/parking_ppo_full.yaml` (200M steps, 18-lesson curriculum, third full-run attempt). Inputs
68 + 11 + 124 (main rays, vector with bay size, near rays), the same as rounds 5 and 6. Deterministic evaluation in one
session with `round6_s1` and `rdqn2_s2` (1,200 episodes per set): 94.8% on generated car parks with occupancy, 96.8% on the training car park with occupancy, 99.2% on the original set, 91.9% and
81.4% from far starts (training car park, generated). Better than `round6_s1` on generated car parks,
the training car park and the original set (paired comparison), level on both far-start sets.
Showcase batches 30/30 with the centre hand-over and 30/30 with the aisle hand-over. Not yet tested in
Assist.

Before it: `ExpR6/round6_s1.onnx` (adopted 2026-10-15, 94.2% on generated car parks in its first
session), before that `ExpR5/round5_s1.onnx` (adopted 2026-10-11), and before that
`ExpOcc/finetune_occ_s3.onnx`. The Assist app's model is a scene setting: since 2026-10-17 the `ScenarioRunner` in
`Training.unity` has `full3_s1` assigned (was `round6_s1`, its component is disabled, as it should be outside app
use).

Second model: `ExpR6/rdqn2_s2.onnx` (DQN, round 6b): the showcase's `alternativeModel` (assigned in
`Showcase.unity`), which the driver can choose in Settings instead of the PPO
model. In the 2026-10-17 session it scored 91.8% on generated car parks against 94.8% for `full3_s1` (worse,
37 against 72 episodes), and it is better from far starts (94.7% and 84.6% against 91.9% and 81.4%). Trained with `DQN/rdqn.py` (plain PyTorch), exported with the same three inputs (68 + 11 + 124) and the
same output names as the PPO models, so the showcase can switch between them. Deterministic evaluation in one session
with `round6_s1`: 91.8% on generated car parks (`round6_s1` 92.4%, level), 94.2% on
the training car park with occupancy, 98.0% on the original set, 94.8% and 84.6% from far starts (training car park,
generated). Showcase batches 30/30 (centre hand-over) and 28/30 (aisle hand-over).

## Input signatures

ML-Agents exports one input per sensor, in sensor-name order: `obs_0` main rays (68), `obs_1` vector (9 or 11), `obs_2`
near rays (124). The runners read these sizes to configure the agent (`06-Agent.md`), and one evaluation session cannot
mix signatures.

| Signature | Models |
|---|---|
| 68 + 9 + `action_masks` 1 (legacy) | the three files in the folder root |
| 68 + 9 | ExpA, ExpC (Experiment C's DQN), ExpD, ExpGeom, ExpOcc, ExpProc |
| 68 + 11 | ExpBay |
| 68 + 11 + 124 | ExpR5, ExpR6 (PPO and DQN), ExpR6Pilot, ExpFull |

All policies output `continuous_actions` and `deterministic_continuous_actions` (2 values: steer, throttle). The DQN
exports write the greedy action's steering and throttle values (from a table of 15 actions) into both outputs.

## Inventory

| Folder / files | What | Trained with | Result |
|---|---|---|---|
| `curric6_ppo_s1_64M.onnx`, `curric_ppo_s1.onnx`, `stage1_ppo_s1.onnx` (root) | legacy development models | before 2026-10-04 | They have a dummy discrete action branch (Behavior Parameters had Discrete Branches = 1 until it was fixed). They do not load with the current prefab. `curric6_ppo_s1_64M` was the development main model (89.8% deterministic) |
| `ExpA/expA_curric_s1-s3` | Experiment A, curriculum, from scratch | `parking_ppo_final_curriculum.yaml`, 40M | 86.5% +/- 2.0 overall, 83.7% +/- 4.6 on stage 10 |
| `ExpA/expA_nocurric_s1-s3` | Experiment A, no curriculum | `parking_ppo_final_nocurric.yaml`, 40M | 75.9% +/- 5.9 overall |
| `ExpD/expD_ppo_s1-s3`, `expD_sac_s1-s3` | PPO vs SAC, stage 0 | `parking_ppo_stage1.yaml`, `parking_sac_stage1.yaml`, 0.5M | PPO 99.1% +/- 0.9, SAC 44.9% +/- 24.6 (stage 0, 400 layouts) |
| `ExpC/expC_dqn_lr1e4_s1-s3` | DQN, lr 1e-4 | `DQN/train_dqn.py`, 0.5M | 94.4% +/- 9.5 |
| `ExpC/expC_dqn_s1-s3` | DQN, lr 3e-4, final | same, lr 3e-4 | 0.0% (collapsed) |
| `ExpC/expC_dqn_s1-s3_200k` | DQN lr 3e-4, 200k-step checkpoint | | 98.9% +/- 1.9 (post hoc) |
| `ExpGeom/finetune_geom_s1-s3` | fine-tune: geometry randomisation | `parking_ppo_finetune_geomrand.yaml`, 15M from `final_curric_s3` | original 90.3% +/- 0.3, geometry set 91.2% +/- 0.5 |
| `ExpOcc/finetune_occ_s1-s3` | fine-tune: + occupancy randomisation | `parking_ppo_finetune_occrand.yaml`, 15M from `finetune_geom_s1` | original 92.7% +/- 0.4, occupancy set 85.8% +/- 1.1. `finetune_occ_s3` was the adopted model until round 5 |
| `ExpProc/finetune_proc_s1-s3` | round 3: + generated car parks | `parking_ppo_finetune_proc.yaml`, 20M from `finetune_occ_s3` | not adopted: worse than the start model on every set |
| `ExpProc/finetune_proc2_s1-s3` | round 3 continuation | `parking_ppo_finetune_proc_cont.yaml`, 20M more | not adopted: level on generated car parks, worse elsewhere |
| `ExpBay/finetune_occ_s3_bay_step0` | check model: `finetune_occ_s3` widened with 2 bay-size inputs, not trained | widened copy, no training (`07-Training.md`) | identical to `finetune_occ_s3` in all 1,200 episodes |
| `ExpBay/round4_bay_s1-s3` | round 4: bay-size inputs + layout curriculum | `parking_ppo_round4_bay.yaml`, 40M | not adopted: `round4_bay_s1` 70.0% on generated car parks (better), but worse on the training car park |
| `ExpR5/finetune_occ_s3_r5_step0` | check model: widened with bay-size and near-ray inputs, not trained | widened copy, no training | actions identical to `finetune_occ_s3` (offline check) |
| `ExpR5/round5_s1-s3` | round 5: near rays, hard cases, longer stuck window near the bay, layout curriculum, fixed geometry drift | `parking_ppo_round5.yaml`, 40M | adopted `round5_s1`. All three seeds better than `finetune_occ_s3` on generated and training car parks |
| `ExpR6Pilot/r6pilot_straight`, `r6pilot_route` | round 6 A/B pilot: far starts (40%) with straight-line or route distance in the reward, 1 seed each | `parking_ppo_round6_pilot_straight.yaml`, `parking_ppo_round6_pilot_route.yaml`, 10M from `round5_s1` | route distance chosen: level on far starts, better on generated car parks (90.2% against 88.0%) and the original set |
| `ExpR6/round6_s1-s3` | round 6: far starts (40%), route distance reward, 80M from `r6pilot_route` | `parking_ppo_round6.yaml`, 80M | adopted `round6_s1`, the PPO model until 2026-10-17. All three seeds better than `round5_s1` and `r6pilot_route` on every set |
| `ExpR6/rdqn2_s1-s3` | DQN round 6b: Double DQN, dueling network, prioritised replay, 15 actions, action repeat 2, idle penalty, warm start from the 21M-step checkpoints of the first `rdqn` run | `DQN/rdqn.py`, `DQN/rdqn2_curriculum.json`, 100M | adopted `rdqn2_s2` as the DQN model: 91.8% on generated car parks against `round6_s1` 92.4% in the same session |
| `ExpFull/full3_s1`, `full3_s2` | full run from scratch (no `--initialize-from`): one 18-lesson curriculum (stages, geometry, occupancy, generated car parks, far starts), Experiment A's PPO settings, bay-size and near-ray inputs from step 0. Third attempt after `full_s1-s3` and `full2_s1-s3` stalled in stage 3. `full3_s3` was stopped in stage 4 and is not in Assets | `parking_ppo_full.yaml`, 200M | adopted `full3_s1`: 94.8% on generated car parks, better than `round6_s1` on generated, training car park and original sets, level from far starts. `full3_s2` 90.8% on generated, worse from far starts on generated car parks (74.5%) |
| `Vision/occupancy_cnn.onnx` | the occupancy CNN (not a parking policy): input `[batch, 3, 64, 64]`, output `logits [batch, 2]` | `Vision/finetune_occupancy_unity.py` | `10-Occupancy-CNN.md` |

Notes on the numbers:
- All are deterministic evaluations on fixed layouts (`08-Evaluation.md`). The same model can differ
  between sessions (e.g. `finetune_occ_s3` on the original set: 92.4% in the round 2 session, 94.2% in the later
  sessions of rounds 3 and 5). `round6_s1` evaluated twice on 2026-10-15 differed in whether it parked in 25 to 74
  of 1,200 episodes per set, 0.6 to 1.8 points. Compare only models evaluated in the same session.
- Each comparison used the same seeds and layouts for every model. The reference model was `finetune_occ_s3` up to
  round 5, `round5_s1` for round 6, `round6_s1` (evaluated again in the same session) for the round 6b DQN and for the
  full run (with `rdqn2_s2` in the same session).

## Experiment names (A to C against the code and configs)

| Experiment | Configs, folders, figure files |
|---|---|
| Experiment A: curriculum vs no curriculum | same (`ExpA`, `expA_*`) |
| Experiment B: shaped vs sparse reward | same (`expB_*` runs, no models kept in Assets) |
| Experiment C: PPO vs SAC vs DQN | PPO vs SAC is "Experiment D" in the configs and files (`ExpD`, `expD_*`, `Figures/expD_*`). DQN was added later as "Experiment C" (`ExpC` only). The round 6b DQN (`ExpR6/rdqn2_*`) is a separate, later model |
