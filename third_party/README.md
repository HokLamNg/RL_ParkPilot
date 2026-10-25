# Third-party algorithm source (reference copy, not used at runtime)

These are unmodified copies of the key algorithm files from the two RL libraries this project uses, so the team can read how PPO, SAC and DQN are implemented without opening the Python environments.

Training does not use these files. `mlagents-learn` and `DQN/train_dqn.py` import the libraries installed with pip (`mlagents` 1.1.0, `stable-baselines3` 2.4.1). Editing a file here changes nothing. To change an algorithm's behaviour, change its settings in `Config/*.yaml` (PPO, SAC) or `DQN/train_dqn.py` (Experiment C's DQN). The current DQN model (round 6, `DQN/rdqn.py`) does not use SB3's DQN. It is written in plain PyTorch, and only its Unity connection (`DQN/unity_vec_env.py`) uses SB3's `VecEnv` base class.

| Library | Version | Licence | Upstream |
|---|---|---|---|
| Unity ML-Agents (`mlagents`) | 1.1.0 | Apache 2.0 (`ml-agents-1.1.0/LICENSE`) | https://github.com/Unity-Technologies/ml-agents |
| Stable-Baselines3 | 2.4.1 | MIT (`stable-baselines3-2.4.1/LICENSE`, `NOTICE`) | https://github.com/DLR-RM/stable-baselines3 |

## Where each algorithm's key steps are

### PPO (ML-Agents): main algorithm, Experiments A and B, PPO side of C

| Step | File | Line |
|---|---|---|
| Trainer: collects experience from all agents, fills the buffer, triggers updates | `ml-agents-1.1.0/trainers/ppo/trainer.py` | `PPOTrainer` |
| Advantage estimate (GAE, γ = 0.99, λ = 0.95) | `ml-agents-1.1.0/trainers/trainer/trainer_utils.py` | 20 `get_gae` |
| One PPO update (epochs over mini-batches) | `ml-agents-1.1.0/trainers/ppo/optimizer_torch.py` | 100 `update` |
| Clipped surrogate objective `clamp(ratio, 1 − ε, 1 + ε) × advantage` | `ml-agents-1.1.0/trainers/torch_entities/utils.py` | 431 `trust_region_policy_loss` |
| Clipped value loss | `ml-agents-1.1.0/trainers/torch_entities/utils.py` | 400 `trust_region_value_loss` |
| Actor and critic networks (2 × 256) | `ml-agents-1.1.0/trainers/torch_entities/networks.py` | 571 `SimpleActor`, 453 `ValueNetwork` |

### SAC (ML-Agents): Experiment C

| Step | File | Line |
|---|---|---|
| Trainer with replay buffer | `ml-agents-1.1.0/trainers/sac/trainer.py` | `SACTrainer` |
| One SAC update | `ml-agents-1.1.0/trainers/sac/optimizer_torch.py` | 481 `update` |
| Twin Q-function loss | same file | 243 `sac_q_loss` |
| Value loss | same file | 277 `sac_value_loss` |
| Policy loss (maximise Q + entropy) | same file | 369 `sac_policy_loss` |
| Automatic entropy-coefficient tuning | same file | 415 `sac_entropy_loss` |
| Where the size-1 discrete branch crashed | same file | 459 `_condense_q_streams` |

### DQN (Stable-Baselines3): Experiment C

| Step | File | Line |
|---|---|---|
| Q-network (one output per discrete action) and greedy action | `stable-baselines3-2.4.1/dqn/policies.py` | 18 `QNetwork`, 68 `_predict` |
| Training step: target `r + γ · max_a' Q_target(s', a')`, Huber loss | `stable-baselines3-2.4.1/dqn/dqn.py` | 184 `train` (max at 199, loss at 212) |
| Target-network update and ε-greedy schedule | same file | 168 `_on_step` |
| Replay buffer (uniform sampling) | `stable-baselines3-2.4.1/common/buffers.py` | 157 `ReplayBuffer`, 284 `sample` |
| Collect experience / train loop shared by off-policy algorithms | `stable-baselines3-2.4.1/common/off_policy_algorithm.py` | `collect_rollouts`, `learn` |

The `max` at `dqn.py` line 199 is the source of the overestimation that caused the DQN collapse at learning rate 3e-4.

The current DQN model (round 6, `DQN/rdqn.py`, plain PyTorch) uses Double DQN targets instead: the online network picks the next action and the target network values it, `r + γⁿ · Q_target(s', argmax_a' Q(s', a'))` (`rdqn.py` lines 495-497), which reduces this overestimation. Details in `docs/System-Reference/13-DQN-Baseline.md`.

## Our code that uses them

- `Config/*.yaml`: PPO and SAC hyperparameters and curriculum, read by `mlagents-learn`.
- `DQN/unity_vec_env.py`: connects SB3 to the Unity build (40 agents as a vectorised environment, 15 discrete actions by default, the table is configurable with `action_table`).
- `DQN/train_dqn.py`: Experiment C's DQN hyperparameters and training. `DQN/export_onnx.py`: exports its Q-network for Unity. `DQN/diagnose_collapse.py`: analyses its collapse.
- `DQN/rdqn.py`: the round 6 DQN in plain PyTorch (Double DQN, dueling network, prioritised replay, training and ONNX export). `DQN/diagnose_rdqn.py`: greedy diagnosis of its checkpoints.
- `Assets/Project/Scripts/`: the environment (car, agent, rewards, curriculum, evaluation).
