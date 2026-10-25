"""Stable-Baselines3 VecEnv over the ParkPilot Unity build.

The Training scene holds 40 parking areas, each with one ParkingAgent. ML-Agents' own Gym wrapper only
supports a single agent, so this wrapper exposes every agent as one sub-environment of a VecEnv.

* Observations: the two ML-Agents sensors concatenated as [obs_0 (68 ray values), obs_1 (9 vector values)],
  the same order as the inputs of an ML-Agents .onnx model (see export_onnx.py).
* Actions: DQN needs a discrete action space, so each discrete action maps to one fixed
  (steering, throttle) pair from ACTION_TABLE and is sent to Unity as the usual 2 continuous actions.
  The Unity side is unchanged.
* Episode ends: terminal agents report done=True. Max-step timeouts (interrupted) are flagged with
  info["TimeLimit.truncated"] so DQN bootstraps through them instead of treating them as real endings.
"""
from collections import defaultdict

import numpy as np
from gymnasium import spaces
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.environment_parameters_channel import EnvironmentParametersChannel
from mlagents_envs.side_channel.stats_side_channel import StatsSideChannel
from stable_baselines3.common.vec_env.base_vec_env import VecEnv

STEER_LEVELS = (-1.0, -0.5, 0.0, 0.5, 1.0)
THROTTLE_LEVELS = (-1.0, 0.0, 1.0)
# 15 actions: every steering level combined with full reverse, no throttle and full forward. Each row is
# (continuous action 0, continuous action 1) = (steer, throttle), the order ParkingAgent.OnActionReceived reads.
# Correction 2026-10-11: until then the names and comments here had steering and throttle swapped; the table
# itself (values and row order) is unchanged, so every trained DQN model and result stays valid.
ACTION_TABLE = np.array(
    [(s, t) for s in STEER_LEVELS for t in THROTTLE_LEVELS], dtype=np.float32)


class UnityParkingVecEnv(VecEnv):
    def __init__(self, file_name, env_params=None, seed=0, base_port=5005, worker_id=0,
                 no_graphics=True, time_scale=20.0, action_table=None, additional_args=None):
        # action_table: rows of (steer, throttle) sent to Unity; default ACTION_TABLE (Experiment C's 15 actions).
        # additional_args: command-line flags for the Unity player, e.g. ["--bay-size-obs", "--near-rays"] (round 4/5 inputs).
        self.action_table = ACTION_TABLE if action_table is None else np.asarray(
            action_table, dtype=np.float32)
        self.engine = EngineConfigurationChannel()
        self.params = EnvironmentParametersChannel()
        self.stats = StatsSideChannel()
        self.env = UnityEnvironment(file_name=file_name, worker_id=worker_id, base_port=base_port,
                                    seed=seed, no_graphics=no_graphics, additional_args=additional_args,
                                    side_channels=[self.engine, self.params, self.stats])
        # Same engine settings as mlagents-learn's defaults.
        self.engine.set_configuration_parameters(width=84, height=84, quality_level=5, time_scale=time_scale,
                                                 target_frame_rate=-1, capture_frame_rate=60)
        for k, v in (env_params or {}).items():
            self.params.set_float_parameter(k, float(v))

        self.env.reset()
        self.behavior = list(self.env.behavior_specs)[0]
        spec = self.env.behavior_specs[self.behavior]
        self.obs_sizes = [int(np.prod(o.shape))
                          for o in spec.observation_specs]
        assert spec.action_spec.continuous_size == 2 and spec.action_spec.discrete_size == 0, spec.action_spec

        decision, _ = self.env.get_steps(self.behavior)
        self.agent_ids = sorted(int(a) for a in decision.agent_id)
        self.index = {a: i for i, a in enumerate(self.agent_ids)}
        n = len(self.agent_ids)
        obs_space = spaces.Box(-np.inf, np.inf,
                               shape=(sum(self.obs_sizes),), dtype=np.float32)
        super().__init__(n, obs_space, spaces.Discrete(len(self.action_table)))

        self._obs = np.zeros((n, sum(self.obs_sizes)), dtype=np.float32)
        self._store(decision)
        self._actions = None
        # Parking/... stats from Unity, drained by the logger
        self.unity_stats = defaultdict(list)

    # ---------------------------------------------------------------- helpers
    def _concat(self, steps, row):
        return np.concatenate([o[row].reshape(-1) for o in steps.obs]).astype(np.float32)

    def _store(self, decision):
        for row, aid in enumerate(decision.agent_id):
            self._obs[self.index[int(aid)]] = self._concat(decision, row)

    def _drain_stats(self):
        for key, values in self.stats.get_and_reset_stats().items():
            self.unity_stats[key].extend(v for v, _ in values)

    # ---------------------------------------------------------------- VecEnv API
    def reset(self):
        return self._obs.copy()

    def step_async(self, actions):
        self._actions = np.asarray(actions).reshape(-1)

    def step_wait(self):
        n = self.num_envs
        rewards = np.zeros(n, dtype=np.float32)
        dones = np.zeros(n, dtype=bool)
        infos = [{} for _ in range(n)]
        waiting = set(range(n))  # agents that still need their next decision

        # Act for the agents that are currently waiting for a decision (all of them, every call).
        decision, _ = self.env.get_steps(self.behavior)
        cont = np.zeros((len(decision), 2), dtype=np.float32)
        for row, aid in enumerate(decision.agent_id):
            cont[row] = self.action_table[self._actions[self.index[int(aid)]]]
        self.env.set_actions(self.behavior, ActionTuple(continuous=cont))

        while waiting:
            self.env.step()
            decision, terminal = self.env.get_steps(self.behavior)
            for row, aid in enumerate(terminal.agent_id):
                i = self.index[int(aid)]
                if dones[i]:
                    continue
                rewards[i] += terminal.reward[row]
                dones[i] = True
                infos[i]["terminal_observation"] = self._concat(terminal, row)
                infos[i]["TimeLimit.truncated"] = bool(
                    terminal.interrupted[row])
            for row, aid in enumerate(decision.agent_id):
                i = self.index[int(aid)]
                if not dones[i]:
                    rewards[i] += decision.reward[row]
                # after a done this is the new episode's first obs
                self._obs[i] = self._concat(decision, row)
                waiting.discard(i)
            # Agents that requested no decision this Academy step get no action; keep stepping.
        self._drain_stats()
        return self._obs.copy(), rewards, dones, infos

    def set_params(self, params):
        """Change environment parameters (read by Unity at each episode reset), e.g. for a curriculum."""
        for k, v in params.items():
            self.params.set_float_parameter(k, float(v))

    def close(self):
        self.env.close()

    # SB3 VecEnv boilerplate (not used by DQN).
    def get_attr(self, attr_name, indices=None):
        return [getattr(self, attr_name)] * self.num_envs

    def set_attr(self, attr_name, value, indices=None):
        setattr(self, attr_name, value)

    def env_method(self, method_name, *args, indices=None, **kwargs):
        return [getattr(self, method_name)(*args, **kwargs)] * self.num_envs

    def env_is_wrapped(self, wrapper_class, indices=None):
        return [False] * self.num_envs

    def seed(self, seed=None):
        return [None] * self.num_envs
