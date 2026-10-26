# 13 DQN (Experiment C baseline and the round 6 full-task model)

Sources: `DQN/train_dqn.py` (151 lines), `DQN/unity_vec_env.py` (153), `DQN/export_onnx.py` (78),
`DQN/diagnose_collapse.py` (73), `DQN/rdqn.py` (730), `DQN/diagnose_rdqn.py` (189), `DQN/rdqn_curriculum.json` (26),
`DQN/rdqn2_curriculum.json` (185), `DQN/requirements.txt`, `ParkingAgent.OnActionReceived`. All checked on 2026-10-17, the round 6b exploration on 2026-10-23,
the action-gap estimate on 2026-10-25.

## Purpose

The project has two DQN implementations with different jobs:

- Experiment C (`train_dqn.py`, Stable-Baselines3): Experiment C compares the three main families of deep RL on the
  easy task (stage 0): PPO (on-policy policy gradient), SAC (off-policy actor-critic, both from ML-Agents) and DQN
  (value-based Q-learning, from Stable-Baselines3). This is Experiment C. In the configs and files PPO vs
  SAC is called Experiment D, DQN is `ExpC` (`09-Models.md`). It is a stage-0 comparison with vanilla DQN, 9 vector
  inputs and untuned hyperparameters.
- Round 6 DQN (`rdqn.py`, plain PyTorch): DQN with the standard fixes, trained on the full task (the
  whole curriculum, real and generated car parks, far starts) with the same inputs as the adopted PPO model
  (68 + 11 + 124). Its best seed, `ExpR6/rdqn2_s2.onnx`, is the showcase's second parking model (`12-Showcase.md`). Its
  own changes (warm start, action repeat, idle penalty) mean it is not a controlled comparison with PPO.

## How it connects to Unity (`unity_vec_env.py`)

ML-Agents' own Gym wrapper supports only one agent, so `UnityParkingVecEnv` wraps the unchanged training build
(`Builds/ParkPilot/ParkPilot.exe`) with the ML-Agents low-level API (`mlagents_envs` 1.1.0) and exposes each of the 40
agents as one sub-environment of an SB3 `VecEnv`. Both trainers use it. `rdqn.py` uses only this class from the SB3
package (its base class `VecEnv`), not SB3's DQN.

- Engine: time scale 20, no frame cap, no graphics (as `mlagents-learn`). Environment parameters through the side
  channel (`env_params` at start, `set_params()` later, read by Unity at each episode reset, used for `rdqn.py`'s
  curriculum). `train_dqn.py` sets `curriculum_stage` 0, `sparse_reward` 0 (shaped).
- Parameters: `action_table` (rows of (steer, throttle) sent to Unity, default `ACTION_TABLE`, the 15 actions
  below) and `additional_args` (command-line flags for the Unity player, `rdqn.py` passes `--bay-size-obs` and
  `--near-rays` for the round 4/5/6 inputs).
- Observations: the sensors concatenated in the order of an ML-Agents model's inputs. Experiment C:
  `[obs_0 rays 68, obs_1 vector 9]` = 77 values (it predates the bay-size and near-ray inputs). `rdqn.py`:
  `[68, 11, 124]` = 203 values.
- Step: every agent gets its action, then Unity is stepped until every agent has asked for its next decision
  (decision period 5 physics steps). Rewards are summed over that time.
- Episode ends: an agent that terminates reports done. A max-step timeout is marked
  `TimeLimit.truncated`, so DQN bootstraps through it instead of treating it as a real end. After a done, the stored
  observation is the new episode's first one, the terminal one goes into `info["terminal_observation"]`.
- Unity's `Parking/...` statistics are collected from the stats side channel for logging.

### Discrete actions (and a corrected labelling error)

DQN needs a discrete action space. `ACTION_TABLE` holds 15 fixed pairs, sent to Unity as the usual 2 continuous
actions. Until 2026-10-11 the code built it as `(t, s) for t in (-1, -0.5, 0, 0.5, 1) for s in (-1, 0, 1)` and called the
first value throttle and the second steering. It is now written as `(s, t) for s in STEER_LEVELS for t in
THROTTLE_LEVELS`, with exactly the same values and order (checked).

Unity reads the first value as steering and the second as throttle (`ParkingAgent.OnActionReceived`:
`steer = a[0], throttle = a[1]`, the order every ML-Agents model in the project uses). So the action set the DQN
actually trained and was evaluated with is:

| | Levels |
|---|---|
| Steering | -1, -0.5, 0, +0.5, +1 (5 levels) |
| Throttle | -1, 0, +1 (full reverse, none, full forward: 3 levels) |

Training and the ONNX export use the same table in the same order, so the experiment and its numbers are valid. Only
the descriptions were wrong. Corrected on 2026-10-11: the comments and names in `unity_vec_env.py` and
`export_onnx.py` and the "top choices" labels of `diagnose_collapse.py`.
Two earlier interpretations change with it:
- "DQN's discrete actions avoid the deadzone: throttle levels are always 0, +/-0.5 or +/-1": still true in effect, with
  the levels corrected. The throttle is always 0 or +/-1. A throttle of 0 lies inside the 0.15 deadzone and brakes on
  purpose, but whenever DQN drives, the command is +/-1, far outside it (corrected 2026-10-20).
- "Slightly more heading error, probably because it can only steer full left, straight or full right": wrong as
  stated. DQN has 5 steering levels. Its coarse control is the throttle (only full or none).

`rdqn.py` has its own tables (`ACTION_SETS`): 25 (steering x throttle, each -1, -0.5, 0, 0.5, 1, the first round 6
run) and 15 (the same 15 pairs as `ACTION_TABLE`, the default since round 6b).

## Experiment C training (`train_dqn.py`)

```
C:\python-envs\sb3\Scripts\python.exe DQN/train_dqn.py --run-id expC_dqn_lr1e4_s1 --seed 1 --lr 1e-4
```

Build the training player first (`07-Training.md`). Options: `--seed`, `--env`, `--base-port` (5005), `--steps`
(500,000 agent steps), `--stage` (0), `--lr` (default 3e-4), `--force` (overwrite the run folder).

| Setting | Value | Note |
|---|---|---|
| Algorithm | SB3 `DQN`, `MlpPolicy`, vanilla (no double DQN), Huber loss | SB3 2.4.1 (`DQN/requirements.txt`: `<2.5`) |
| Network | 2 x 256 | as PPO and SAC |
| Discount | 0.99 | as PPO and SAC |
| Learning rate | 3e-4 (same as PPO, SAC) or 1e-4 (SB3 default) | both run, 3 seeds each |
| Replay buffer | 500,000 | |
| Batch | 256 | |
| Learning starts | 10,000 | |
| Updates | `train_freq` 1 vector step (= 40 agent steps), 4 gradient steps | one gradient step per 10 agent steps, as SAC |
| Target network | hard update every 10,000 agent steps | |
| Exploration | epsilon 1.0 -> 0.05 over the first 20% (100k steps) | |
| Budget | 500k agent steps, stage 0, shaped reward | as the PPO and SAC runs (`parking_ppo_stage1.yaml`, `parking_sac_stage1.yaml`) |

Hyperparameters are close to SB3 defaults and were not tuned.

Output in `results/<run-id>/`: `ParkingAgent/` with TensorBoard events (the same `Parking/...` tags as ML-Agents, plus
`Environment/Cumulative Reward`, `Environment/Episode Length`, `Policy/Epsilon`, `Losses/TD Loss`, every 10,000 steps),
checkpoints `ParkingAgent-<step>.zip` every 100,000 steps, the final `ParkingAgent.zip`, and `ParkingAgent.onnx`.
Speed: about 1,300 steps per second with 3 runs in parallel.

## Experiment C export to Unity (`export_onnx.py`)

The Q-network is wrapped so that Unity can run it like any ML-Agents model (`EvaluationRunner`, inference only):

- inputs `obs_0 [batch, 68]`, `obs_1 [batch, 9]` (opset 9, as the project's ML-Agents models)
- the graph computes argmax Q, looks the pair up in `ACTION_TABLE` and returns it as both `continuous_actions` and
  `deterministic_continuous_actions`, plus `version_number` 3, `memory_size` 0, `continuous_action_output_shape` 2
- every export is checked against PyTorch with onnxruntime on 32 random inputs

Re-export a saved model:
`C:\python-envs\sb3\Scripts\python.exe DQN/export_onnx.py results/<run>/ParkingAgent/ParkingAgent.zip <out>.onnx`

DQN is therefore always greedy in Unity. The models are in `Assets/Project/Models/ExpC/` (`09-Models.md`).

## Experiment C results

Deterministic evaluation, stage 0, the same 400 layouts as PPO and SAC (40 areas x 10, seed base 100000):

| Algorithm | Per seed | Mean +/- SD | Parked episodes: time / distance / heading |
|---|---|---|---|
| PPO | 98.2 / 100.0 / 99.0 | 99.1% +/- 0.9 | 4.3 s / 0.34 m / 1.1 degrees |
| SAC | 23.0 / 71.5 / 40.2 | 44.9% +/- 24.6 | 4.8 s / 0.33 m / 1.3 degrees |
| DQN, lr 1e-4 | 100.0 / 99.8 / 83.5 | 94.4% +/- 9.5 | 4.3 s / 0.17 m / 2.2 degrees |
| DQN, lr 3e-4, final | 0 / 0 / 0 | 0.0% (collapsed) | |
| DQN, lr 3e-4, 200k checkpoint | 100.0 / 96.8 / 100.0 | 98.9% +/- 1.9 | 3.2 s / 0.23 m / 1.7 degrees |

The 200k checkpoints were chosen after seeing the curves (post hoc), so they illustrate the collapse and are not a
fair comparison.

Paired comparison on the same layouts, DQN (1e-4) vs PPO: s1 DQN better (7 vs 0), s2 equal, s3 PPO better
(66 vs 4). DQN vs SAC: DQN better for every seed.

### SAC dead-zone check

SAC's stuck endings were explained by its mean throttle landing in the throttle dead zone. A re-evaluation of the same
models tested this: stage 0, deterministic, the same 400 layouts, PPO as a control, once with the dead zone at 0.15 and
once switched off (`EvaluationRunner.throttleDeadzoneOverride`, `08-Evaluation.md`).

| Model | Dead zone 0.15 | Dead zone 0 |
|---|---|---|
| SAC s1 / s2 / s3 | 23.0 / 71.5 / 40.2% | 100.0 / 38.0 / 91.5% |
| PPO s1 / s2 / s3 | 98.0 / 100.0 / 99.0% | 98.5 / 100.0 / 100.0% |

At 0.15 the SAC results match the table above exactly (PPO s1, first in the queue, differs in 15 episodes). In SAC's
stuck episodes the car does not stand still. Over the last 3 s it crawls at about 0.06 m/s, 0.4 to 0.5 m from the bay
centre (seeds 1 and 3), with a throttle of about 0.18 to 0.21 at the edge of the dead zone, where the car brakes or
barely accelerates. Without the dead zone, seeds 1 and 3 park and SAC averages 76.5% +/- 33.6, close to its 74.2% in
training. Seed 2 gets worse because its throttle in those situations drops to about 0.04. PPO does not change.

## The collapse at lr 3e-4 (`diagnose_collapse.py`)

```
C:\python-envs\sb3\Scripts\python.exe DQN/diagnose_collapse.py expC_dqn_s1
```

It drives the good 200k checkpoint greedily for 300 vector steps (about 11k transitions from complete episodes),
computes the discounted return actually obtained, then asks every checkpoint (100k to 500k) for its Q-values on exactly
those states: Q of the action taken vs the true return, max Q, and how often it would pick the same action.

Finding: the Q-values kept rising past the true return (overestimation) while the greedy action flipped on
58-88% of the good policy's states by 300k. Per-step shaping rewards are small (about 0.01), so small value errors
change the argmax. This is the known instability of Q-learning with function approximation (bootstrapping,
off-policy data, a neural network), not an environment fault: PPO on the same build is stable, and DQN at lr 1e-4
mostly holds. Experiment C did not try the standard fixes (double DQN, soft target updates, a lower or decaying
learning rate, early stopping). The round 6 DQN below uses them.

## Round 6 DQN on the full task (`rdqn.py`)

Plain PyTorch (no SB3 algorithm code), on the GPU if available. Two sub-commands:

```
C:\python-envs\sb3\Scripts\python.exe DQN/rdqn.py train --run-id rdqn_s1 --seed 1 --base-port 6005
C:\python-envs\sb3\Scripts\python.exe DQN/rdqn.py train --run-id rdqn_s1 --seed 1 --base-port 6005 --resume
C:\python-envs\sb3\Scripts\python.exe DQN/rdqn.py export results/rdqn_s1/ParkingAgent/checkpoint.pt results/rdqn_s1/ParkingAgent.onnx
```

Build the training player first (`07-Training.md`). The round 6b runs (`rdqn2_s1-s3`) were started with 3 Unity
instances, 100M steps, `--init-from` the first run's 21M checkpoint of the same seed and `--start-phase 11` (the start phase is confirmed by the phase stored in their first checkpoints). They ran without the extra early exploration
(`--explore-steps 0`, see Exploration and the curriculum below).

### Learning

- Network (`DuelingQ`, `--net mlp`): a fully connected trunk of 3 x 512 (ReLU) on the 203 normalised inputs, a value
  head 512 -> 256 -> 1 and an advantage head 512 -> 256 -> number of actions, combined as `Q = V + A - mean(A)`.
  (`--net raycnn` is an optional CNN over the ray angles, course Week 9 Part A, implemented but not used.)
- Targets: n-step returns (`--nstep`), Double DQN: the online network picks the next action, the target network
  values it. A real episode end stops bootstrapping, a timeout (`TimeLimit.truncated`) does not.
- Target network: soft update after every gradient step, `target = (1 - tau) target + tau online`, tau 0.005.
- Prioritised replay (`Replay`, `SumTree`): proportional, alpha 0.6, priority `|TD error| + 0.001`, new transitions
  get the highest priority so far, stratified sampling. Importance weights with beta annealed linearly from 0.4 to 1
  over `--steps`. Observations are stored raw (float16) and normalised when sampled.
- Input normalisation (`RunningNorm`): running mean and variance (batched Welford) of all inputs, `(x - mean) / std`
  clipped to +-5, like ML-Agents' `normalize: true`.
- Loss and optimiser: Huber loss (`smooth_l1`) weighted by the importance weights, Adam (eps 1.5e-4), gradient norm
  clipped to 10. The learning rate decays linearly to 10% of `--lr` over `--lr-decay-steps` (0 = over `--steps`).
- Update rate: learning starts once the buffer holds `--learning-starts` transitions, then `--replay-ratio`
  sampled transitions per new transition (16, Experiment C's SB3 DQN: 25.6), in batches of `--batch`.

### Exploration and the curriculum

- Per-agent epsilon (Ape-X): agent i of N uses `eps_i = 0.4 ** (1 + 7 i / (N - 1))`, from 0.4 down to about
  0.0007. During the first `--explore-steps` (150k) every agent explores at least `1 - steps / explore-steps`.
  The round 6b runs did not use this extra exploration. Their `Policy/Epsilon (mean)` is 0.0634 (the Ape-X rates
  alone) from the first summary at 50k steps, against 0.667 at 50k and 0.335 at 100k in the first run. A warm start
  counts steps from 0, so they were started with `--explore-steps 0` (read from the logs, the command line was not
  recorded).
- Near-greedy agents (`eps_i <= 0.02`, a little over half of them) drive the curriculum: a phase ends when their
  success rate over the last `--success-window` (2,000) episodes reaches the phase's `success` after its `min_steps`, or
  at its `max_steps`. Success counts as a real ending whose last reward is above 1 (the +2 parked reward). Until half
  the window is filled the rate counts as 0.
- The curriculum file is re-read at every phase change, so edits to later phases apply without a restart.
  `--start-phase` starts in a later phase.

### Environment, steps and the round 6b changes

- Several Unity instances (`MultiUnity`, `--envs`, 40 agents each, seeds `seed * 100 + i`, worker ids 0 to n-1)
  are stepped in parallel threads, since the ML-Agents calls wait on sockets. Each runs with `--bay-size-obs` and
  `--near-rays`.
- Action repeat (`--action-repeat`, default 2): each DQN choice is held for that many decisions (0.2 s at 2), the
  frame skip of the original DQN. Rewards are summed. An agent whose episode ends during the repeat keeps that ending.
  Steps are counted as N agents x repeat (Unity decisions), comparable with every other run.
- Warm start (`--init-from`): tensors of the same shape are copied from the checkpoint, the advantage output layer
  is copied row by row for every action that exists in the old table (the 15 actions are a subset of the old 25), the
  normaliser is copied, and the target network starts as a copy. The network type must match.
- Idle penalty (`--idle-penalty` 0.02 per decision): when the car stands still (`--idle-speed`, < 0.1 m/s) more than
  `--idle-dist` (1.2 m) from the bay centre, read from the next observation (vector slot 6 = speed / 5, slots 1-2 = bay
  position / 30). It is subtracted only from the reward stored in the replay buffer. Unity's reward is unchanged
  (and so PPO's), and the logged episode reward does not include it.

### Defaults (`rdqn.py train`)

| Option | Default | Note |
|---|---|---|
| `--run-id` | (required) | output in `results/<run-id>/` |
| `--seed` | 1 | |
| `--env` | `Builds/ParkPilot/ParkPilot.exe` | |
| `--envs` | 4 | Unity instances, 40 agents each (round 6 runs used 3) |
| `--base-port` | 6005 | |
| `--curriculum` | `DQN/rdqn2_curriculum.json` | first run: `rdqn_curriculum.json` |
| `--steps` | 150,000,000 | agent decisions, as ML-Agents counts steps (round 6b: 100M) |
| `--lr` | 1e-4 | decays linearly to 10% |
| `--lr-decay-steps` | 0 | 0 = over `--steps` |
| `--gamma` | 0.99 | first run 0.995 |
| `--nstep` | 3 | first run 5 |
| `--net` | `mlp` | or `raycnn` |
| `--init-from` | (none) | checkpoint file for a warm start |
| `--start-phase` | 0 | 11 = real car parks |
| `--action-repeat` | 2 | first run 1 |
| `--actions` | 15 | 15 or 25 (first run 25) |
| `--idle-penalty` | 0.02 | 0 = off (first run: none) |
| `--idle-speed` | 0.1 | m/s |
| `--idle-dist` | 1.2 | m |
| `--batch` | 512 | |
| `--buffer` | 1,000,000 | first run 2M |
| `--learning-starts` | 50,000 | transitions in the buffer |
| `--replay-ratio` | 16 | |
| `--tau` | 0.005 | |
| `--explore-steps` | 150,000 | round 6b runs: 0 |
| `--success-window` | 2,000 | near-greedy episodes |
| `--summary-freq` | 50,000 | steps |
| `--checkpoint-every` | 1,000,000 | steps |
| `--device` | `cuda` | falls back to the CPU if CUDA is missing |
| `--resume`, `--force` | off | continue from `checkpoint.pt`, or start over |

The "first run" values are the first round 6 run (`rdqn_s1-s3`), before the round 6b defaults.

### Logging and checkpoints

Output in `results/<run-id>/ParkingAgent/`: TensorBoard events every `--summary-freq` steps with Unity's `Parking/...`
tags, `Environment/Cumulative Reward`, `Environment/Episode
Length`, `Policy/Epsilon (mean)`, `Curriculum/Phase`, `Curriculum/NearGreedySuccess`, `Losses/TD Loss`,
`Policy/Q (taken action)`, `Policy/Action gap (median)` (best minus second-best Q, median per batch over the first 50
batches of each period) and `Policy/Idle share (standing still away from the bay)`.

`checkpoint.pt` (the latest, for `--resume`) and `ParkingAgent-<steps>.pt` every `--checkpoint-every` steps, and
`checkpoint.pt` again when training stops. A checkpoint holds the online and target networks, the optimiser, the
normaliser, the step count, the curriculum phase and its start, the input sizes, the network type and the action table.
`--resume` restores all of it, the replay buffer refills. Without `--resume` or `--force` a run with a checkpoint is not
overwritten. At the end of training the model is exported to `results/<run-id>/ParkingAgent.onnx`.

### Export (`rdqn.py export`)

`ForUnity` makes an ML-Agents-compatible graph (opset 11): three inputs `obs_0 [batch, 68]`, `obs_1 [batch, 11]`,
`obs_2 [batch, 124]`, concatenated, normalised inside the graph (the checkpoint's mean and std, clipped to +-5),
argmax Q, looked up in the checkpoint's action table and returned as `continuous_actions` and
`deterministic_continuous_actions`, plus `version_number` 3, `memory_size` 0 and `continuous_action_output_shape` 2.
Every export is checked against PyTorch with onnxruntime on 32 random inputs. The round 6b models are in
`Assets/Project/Models/ExpR6/rdqn2_s1-s3.onnx` (`09-Models.md`), and the showcase loads `rdqn2_s2` as its
`alternativeModel`.

## Curricula (`rdqn_curriculum.json`, `rdqn2_curriculum.json`)

Both have 16 phases that follow PPO's path, with `base` parameters `sparse_reward` 0 and `early_endings` 1:

| Phase | Name | Conditions | Success / min / max steps |
|---|---|---|---|
| 0-1 | `stage0`, `stage1` | `curriculum_stage` 0, 1 | 0.90 / 300k / 5M |
| 2-3 | `stage2`, `stage3` | stages 2, 3 | 0.88 / 500k / 6M |
| 4-9 | `stage4` to `stage9` | stages 4-9 | 0.88 / 1M / 8M |
| 10 | `stage10` | stage 10 | 0.88 / 2M / 10M |
| 11 | `real_car_parks` | stage 10, geometry randomisation (share 0.5), occupancy randomisation, `stuck_steps_near_bay` 400 | 0.85 / 4M / 12M |
| 12 | `generated_L0` | as 11, generated car parks difficulty 0, hard cases 0.25 | 0.85 / 4M / 10M |
| 13 | `generated_L1` | difficulty 1, hard cases 0.35 | 0.83 / 6M / 12M |
| 14 | `generated_L2` | difficulty 2, hard cases 0.5, generated share 0.5 | 0.83 / 10M / 25M |
| 15 | `far_starts` | as 14, `far_start_share` 0.4, `route_distance` 1, `route_clearance` 0.3, `empty_bay_cost` 3 | last phase, runs to the end |

The only difference is the share of generated car parks (`procedural_layout`) in phases 12 and 13: 0.1 and 0.25 in
`rdqn_curriculum.json` (the first run), 0.25 and 0.4 in `rdqn2_curriculum.json` (round 6b, the first run's weak
spot). The round 6b runs started in phase 11 (`real_car_parks`) and ended in phase 15.

## Diagnosis of the first round 6 run (`diagnose_rdqn.py`)

```
C:\python-envs\sb3\Scripts\python.exe DQN/diagnose_rdqn.py rdqn_s1 13000080 21000000 31000080 41000040 --episodes 15
```

This is the command used for the analysis of the first run. Since 2026-10-16 only the 21M checkpoints of `rdqn_s1-s3` and each run's
`checkpoint.pt` are kept (intermediate checkpoints deleted to save space, `07-Training.md`), so the other step numbers no
longer exist. For round 6b, train a run with `--checkpoint-every` and pick its checkpoints.

Arguments: the run, one or more checkpoint step numbers (`ParkingAgent-<step>.pt`), `--conditions` (`stage10`,
`real_car_parks`, `generated`, default all three), `--episodes` per agent (15, 40 agents, the first episode of each agent
is discarded), `--base-port` (6305), `--gamma` for the obtained return (default now 0.99, round 6b's value. The
analysis of the first run used that run's 0.995) and `--action-repeat` (default 0 = auto: 2 for 15-action checkpoints,
1 for 25-action ones). It runs each checkpoint greedily in one Unity instance with the
round 6 inputs and reports per checkpoint and condition: success, collision, stuck and no-progress rates (Unity's
statistics), throttle and steering use, the standing-still share (all decisions and the last 3 s before a stuck ending),
direction changes before a stuck ending, the median action gap (all decisions and before stuck), and Q at the start of
an episode against the discounted return actually obtained (overestimation).

Unity decodes the chosen action numbers with each checkpoint's own action table (25 actions in the first run, 15 in
round 6b), and every choice is held for the action repeat. "Decision" in its output means one DQN choice, and the
windows "last 3 s" and "last 1 s" are scaled by the repeat. Until 2026-10-15 the script always used the 25-action table
without repeat, which is right for the first run's checkpoints, the only ones it was used on.

## Round 6 results

First run (`rdqn_s1-s3`, 25 actions, gamma 0.995, 5-step, stopped at 41M steps): stages 0-10 learned well
(stage 10 93-94% greedy, no forgetting), but real and generated car parks learned slowly (generated 15% to 39% over
20M steps) with stuck as the main failure. Cause: the action gap (median best minus second-best Q, 0.004-0.02)
was roughly 10-50 times smaller than the Q error implied by the TD loss, so the argmax between waiting, forward and reverse was
noise. Round 6b targets this: warm start from the 21M checkpoints, 15 actions, action repeat 2, gamma 0.99,
3-step returns and the idle penalty.

Round 6b training (`rdqn2_s1-s3`, 100M steps each, about 8.5 hours): near-greedy training statistics,
seed mean, first to last 8M steps: generated car parks 64% to 86%, training car park 84% to 95%, stuck 7% to 2%,
collisions 5% throughout. The action-gap problem did not return.

Round 6b deterministic evaluation: five test sets of 1,200 episodes, `round6_s1` (PPO) run again in the
same session for a paired comparison. Success in %:

| Test set | rdqn2_s1 | rdqn2_s2 | rdqn2_s3 | round6_s1 (same session) |
|---|---|---|---|---|
| Generated car parks (95% target) | 90.7 | 91.8 | 89.3 | 92.4 |
| Far starts, training car park | 95.5 | 94.8 | 95.0 | 91.6 |
| Far starts, generated | 83.5 | 84.6 | 83.7 | 80.7 |
| Training car park + occupancy | 94.4 | 94.2 | 94.0 | 94.5 |
| Original (close hand-over) | 98.0 | 98.0 | 97.1 | 97.3 |

DQN reaches PPO's level: on generated car parks `rdqn2_s2` and `round6_s1` are level (67 episodes won, 74 lost), on the training
car park and the original set no seed differs clearly from `round6_s1`, and on far starts every DQN seed is better. DQN collides far less but ends stuck or without progress more often. Adopted: `rdqn2_s2`, the showcase's
second model. Showcase batches: `rdqn2_s2` 30/30 from the car park centre and 28/30 from the aisle (two
stops 0.37-0.39 m short of the bay centre, ended as stuck).

Against the adopted PPO model: in the 2026-10-17 session `rdqn2_s2` scored the same as before on
generated car parks (91.8%) against 94.8% for `full3_s1`. Paired comparison on that session: `full3_s1` is
better on generated car parks (72 episodes won, 37 lost), the training car park (57 to 26) and the original set (22 to 8), `rdqn2_s2` on both far-start sets (training car park 75 to 42, generated 113 to 75). It stays the showcase's second model.

## Limits

- Experiment C: stage 0 only (fixed bay, near spawn, 500k steps), vanilla DQN, untuned hyperparameters, 3 seeds
  per condition, 9-input observations (not comparable with the round 4/5/6 models on their inputs).
- Round 6 DQN: 3 seeds. Its own changes (warm start from the first run, action repeat, a trainer-side idle penalty,
  its own curriculum logic) mean DQN vs PPO on the full task compares two tuned pipelines, not two algorithms under
  identical conditions. Neither reaches 95% on generated car parks. Its typical failure is stopping just short in the
  bay (stuck).
- Evaluation numbers differ between sessions by a few episodes, so only models evaluated in the same session
  are compared.
