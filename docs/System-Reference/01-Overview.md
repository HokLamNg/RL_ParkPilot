# 01 Overview (start here)

ParkPilot is a car-parking assistant built for UTS 43008 Reinforcement Learning, Assignment 3. A reinforcement-learning
agent learns to park a car in a chosen bay of a car park using distance sensors. Around it sit an occupancy CNN that
reads which bays are free from a camera photo, a research app that turns photos of real car parks into simulations,
and a driver-facing showcase in Unity.

This folder (`docs/System-Reference/`) describes how every part is implemented, as checked against the code on
2026-10-17.

## The parts

```
                          TRAINING (Unity + ML-Agents PPO)
  CarController (02) <- ParkingAgent (06) <- ParkingEnvironment (04) <- bays (03), generated car parks (05)
        |                      |                                        x 40 areas in Training.unity
        |                 configs, build, warm starts (07)  ->  results/<run>/  ->  Assets/Project/Models (09)
        |                                                                               |
        |                      EvaluationRunner + analysis scripts (08)  <--------------+
        |
        |                 USING A TRAINED POLICY
        |   Showcase (12):  CCTV photo -> occupancy CNN (10) -> driver picks a bay -> scripted drive-in -> RL parks
        |                   (the PPO model, or the DQN chosen in Settings)
        |   Assist (11):    real photo -> bays + CNN (10) -> homography -> scenario.json -> ScenarioRunner -> RL parks
        |
  DQN (13): DQN/rdqn.py (plain PyTorch) on the full task, the showcase's second model.
            Experiment C's Stable-Baselines3 DQN (stage 0) was the earlier comparison
```

| Doc | Part | One line |
|---|---|---|
| `02-Car.md` | `CarController` | Kinematic bicycle model on a Rigidbody, 3 m/s, 35 degree steering, 50 Hz physics |
| `03-Bays.md` | `ParkingSpot`, bay layout | 30 bays of 3.2 x 4.88 m in the training car park, names and groups |
| `04-Environment.md` | `ParkingEnvironment`, `DrivableMap` | Episode reset, curriculum stages 0-10, randomisation (geometry, occupancy, generated car parks), far starts, route map, scenario mode |
| `05-Generated-Car-Parks.md` | `ProceduralLot` | Random car parks: aisle widths, bay sizes, pillars, walls, difficulty levels |
| `06-Agent.md` | `ParkingAgent` | Observations, 2 continuous actions, shaped reward (straight-line or route distance), parked check, endings, statistics |
| `07-Training.md` | configs, build, monitor | How to train, every config, curricula, model lineage, warm starts with new inputs, training charts |
| `08-Evaluation.md` | `EvaluationRunner`, analysis | Fixed seeded test layouts, CSV columns, statistics, figure scripts |
| `09-Models.md` | `Assets/Project/Models` | Which models are in use, input signatures, every model with its result |
| `10-Occupancy-CNN.md` | `Vision/` CNN | Free or taken per bay from a photo, training, Unity fine-tune, inference |
| `11-Assist-App.md` | `Vision/assist.py`, `ScenarioRunner` | Photo of a real car park to a recommended manoeuvre |
| `12-Showcase.md` | `ShowcaseController`, `ShowcaseUI` | The driver demo: menus, capture, drive-in, RL parking, logs, batch test |
| `13-DQN-Baseline.md` | `DQN/` | Experiment C's SB3 DQN (real action set, collapse) and the full-task DQN `rdqn.py` |

## The current state in a few lines

- Adopted parking policy: `Assets/Project/Models/ExpFull/full3_s1.onnx` (adopted 2026-10-17), trained
  from scratch with one config (`Config/parking_ppo_full.yaml`, 200M steps, 18-lesson curriculum): 99.2% on the
  original test set, 96.8% on the training car park with random occupancy, 94.8% on unseen generated car parks, 91.9%
  and 81.4% from far starts (training car park, generated) (deterministic, 1,200 episodes each, one session). It
  replaces `round6_s1` (round 6, adopted 2026-10-15) and is better than it on generated car
  parks, the training car park and the original set, level on both far-start sets.
- Occupancy CNN: `Assets/Project/Models/Vision/occupancy_cnn.onnx` (fine-tuned on Unity renders): 99.6% on held-out
  Unity car parks, 98.88% on real photos.
- Showcase: uses both, with `ExpR6/rdqn2_s2` (DQN) as a second model the driver can choose in Settings.
  Batch tests: `full3_s1` 30/30 parked with the centre hand-over and 30/30 with the aisle hand-over, the
  previous PPO model `round6_s1` 30/30 centre, `rdqn2_s2` 30/30 centre and 28/30 aisle.
- Assist: works end to end on a real photo (24/24 with an earlier model on the user-marked camera-6 layout). It
  now uses `full3_s1`, not yet tested on camera 6.
- DQN: `DQN/rdqn.py` (plain PyTorch, not SB3) trained on the full task. `rdqn2_s2` reached the level of `round6_s1`
  (91.8% on generated car parks against 92.4% in the same session). Against `full3_s1` (2026-10-17) it is
  worse on the three near-start sets (91.8% against 94.8% on generated car parks) and better from far starts.
- Full run from scratch: `full3_s1-s3`, the third attempt after two stalled in stage 2 or 3. `full3_s1`
  is the adopted model, `full3_s2` is worse from far starts on generated car parks, `full3_s3` was stopped
  in stage 4. The recipe works but is not reliable across seeds.
- Known issues: the bugs and wrong descriptions found in the 2026-10-11 check are fixed. Still to know: Assist's simulation is mirrored, and `full3_s1` is untested in Assist.

## Folders

| Folder | Contents | In git |
|---|---|---|
| `Assets/Project/Scripts/` | all project C#: `CarController`, `ParkingSpot`, `ParkingEnvironment`, `ProceduralLot`, `ParkingAgent`, `EvaluationRunner`, `ScenarioRunner`, `ShowcaseController` + `ShowcaseUI` + `ShowcaseUIView`, `DrivableMap` (round 6 route map) | yes |
| `Assets/Project/Scenes/` | `Training.unity` (40 areas, training, evaluation, Assist), `Showcase.unity` (the driver demo) | yes |
| `Assets/Project/UI/` | `ShowcaseUI.prefab` (the showcase interface, editable in the scene) and its sprites | yes |
| `Assets/Project/Prefabs/` | `TrainingArea` (car park, bays, agent), `ObstacleCar` (parked car) | yes |
| `Assets/Project/Models/` | trained policies by experiment folder, `Vision/occupancy_cnn.onnx` | yes |
| `Assets/Parking Env/` | the third-party car park and town asset (`SampleScene` is the showcase's town) | yes |
| `Config/` | ML-Agents training configs (23 YAML files), including `parking_ppo_full.yaml` (the full run) and the three stall-test configs `parking_ppo_fp_*.yaml` | yes |
| `DQN/` | Experiment C's SB3 DQN (`train_dqn.py`, `unity_vec_env.py`, `export_onnx.py`, `diagnose_collapse.py`), the full-task DQN `rdqn.py` with `diagnose_rdqn.py` and its curricula `rdqn_curriculum.json`, `rdqn2_curriculum.json` | yes |
| `Vision/` | occupancy CNN, its training and export, the Assist app, geometry, bay layouts, `models/` (CNN weights and metrics) | yes |
| `Evaluation/` | evaluation CSVs and summaries | yes |
| `Figures/` | charts of the experiments and training runs | yes |
| `docs/` | `Training-Monitor.md`, this folder (`System-Reference/`) | yes |
| `third_party/` | source snapshots of ML-Agents 1.1.0 and SB3 2.4.1 with a README | yes |
| `results/` | ML-Agents and DQN training output: TensorBoard logs (all training graphs), final models, `checkpoint.pt` for resuming. Intermediate checkpoints of finished runs were deleted on 2026-10-16 (see `07-Training.md`). Only on this PC, back it up to keep the graphs | no |
| `Builds/` | the headless training player `Builds/ParkPilot/ParkPilot.exe` | no |
| `Data/` | Unity CNN patches (`unity_patches/`). The CNRPark-EXT patches and full frames (`cnrpark/`) were deleted on 2026-10-16 to save space and must be downloaded again to retrain the CNN | no |
| `Scenarios/` | Assist files and results, showcase run logs. Only the showcase batch logs `showcase/batch_*.txt` and the tables `showcase/results*.csv` are in git | partly |

## Environments and tools

| What | Where |
|---|---|
| Unity | 6000.5.8f1, ML-Agents package 4.1.0, Inference Engine for ONNX models |
| ML-Agents trainer | venv `C:\python-envs\mlagents` (Python 3.10.11, mlagents 1.1.0, torch 2.2.2+cu121), trains on the GPU |
| DQN, CNN, Assist | venv `C:\python-envs\sb3` (SB3 2.4.1, torch 2.2.2+cu121, torchvision, opencv, scipy), `rdqn.py` trains on the GPU |

## Conventions

- Units: metres, seconds, degrees. Physics at 50 Hz (0.02 s per step). The agent decides every 5 physics steps
  (0.1 s). An episode lasts at most 1500 steps (30 s), a far-start episode at most 3000 steps (60 s).
- Frames: Unity, y up. A bay's forward (+z of its transform) points into the bay from its open end. Yaw 0 = a
  car parked nose-in faces +z, positive yaw turns towards +x. Area-local coordinates are relative to a training area's
  centre. The training car park's walls are at +/-20 m.
- Bay names: `Bay_<row>_<side><n>`, e.g. `Bay_N2_W3` (row N1, N2, S2, S1, middle column `MidN_1-3`, `MidS_1-3`).
  The showcase shows them as `N2-W3`. Assist layout bays are `Bay_L00`, `Bay_L01`, ...
- Tags the agent's rays detect: `Wall` (walls, pillars, barriers) and `ObstacleCar` (parked cars).
- Model names: `<experiment>_<variant>_s<seed>`, three seeds per condition. Folder `ExpX` per experiment
  (`09-Models.md` maps the experiment names A to C to folder names: Experiment C is `ExpD` (PPO vs SAC) plus `ExpC` (DQN)).
  Later names do not follow this pattern: `round6_s1` (round and seed), `rdqn2_s2` (second `rdqn.py` run, seed 2),
  `r6pilot_route` (round 6 pilot with the route distance, one seed) and `full3_s1` (the full run from scratch, seed 1).
  The round 6b DQN models are in `ExpR6` next to the round 6 PPO models. The full run's models are in `ExpFull`.
- Evaluation file names: `<model>_<mode>[_occrand][_proc]..._<date>-<time>.csv` (`08-Evaluation.md`).
- Deterministic evaluation decides everything. Training statistics are noisy and only guide.
- Writing style for all project documents: no em-dashes and no semicolons.

## Glossary

| Term | Meaning |
|---|---|
| Area | one training car park with its own agent. `Training.unity` has 40, the showcase 1 |
| Stage | curriculum level 0-10: which bays can be targets and how far and how turned the car starts (`04-Environment.md`) |
| Lesson | one step of an ML-Agents curriculum (stage curriculum in Experiment A, layout curriculum in rounds 4-5) |
| Full-run curriculum | the 18 lessons of `parking_ppo_full.yaml`: stages 0-10, geometry, occupancy, four generated-car-park steps, far starts |
| Original test set | stage 4, 6 and 10 layouts with no randomisation, 1,200 episodes |
| `_occrand` | test set with occupancy randomisation: the training car park with 0-27 of its other bays occupied |
| `_occrand_proc` | generated car parks at full difficulty, 0 to 90% of their bays occupied |
| `_occrand_far`, `_occrand_proc_far` | the two test sets above with far starts (round 6) |
| Far start | an episode that starts anywhere in the aisles and corridors, 10-40 m route distance from the bay, any heading, 60 s limit (`04-Environment.md`) |
| Route distance | the shortest drivable distance to the bay around parked cars, pillars and walls (`DrivableMap`). The reward's distance term from round 6 on, instead of the straight line |
| Generated car park | a random layout from `ProceduralLot` (aisle 6-11 m, bay size, pillars, walls) |
| Geometry randomisation | per-episode changes to the target bay's size and the parked cars' positions |
| Hard case | round 5, generated car parks only: the target is a bay with a pillar beside it, else one on an aisle under 8 m, and half of the time the start is at most 7 m away (`04-Environment.md`) |
| Near rays | the second, dense short-range ray sensor added in round 5 (124 inputs) |
| Bay-size inputs | 2 vector inputs (bay width and length) added in round 4 |
| Widening | adding zero-weight input columns to a checkpoint so a model with more inputs starts exactly like its parent |
| Hand-over | the moment the showcase's scripted drive-in gives control to the RL agent |
| Scenario | a fixed situation (target, start pose, occupied bays) that every reset rebuilds, used by the showcase and Assist |
| Soft contact | a light touch on stages 3-8 that is penalised but does not end the episode |
| Stuck / no progress | early endings when the car stops moving or stops getting closer (`06-Agent.md`) |
| Photo detection confidence | the CNN's softmax for its free or taken call, shown on hover. Not a parking success rate |

## Other documents

- `docs/Training-Monitor.md`: dated snapshots of the training statistics (rounds 4 to 6, the DQN and the full runs).
- `Vision/README.md`: Assist usage and setup (the full description is `11-Assist-App.md`).
