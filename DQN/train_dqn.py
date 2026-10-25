"""Train DQN (Stable-Baselines3) on the ParkPilot build: Experiment C's third algorithm.

Same task as the PPO and SAC runs of Experiment C (Config/parking_ppo_stage1.yaml, parking_sac_stage1.yaml):
stage 0 (fixed bay, near spawn), shaped reward, 500k agent steps, 40 parallel areas, gamma 0.99,
2 x 256 network. DQN hyperparameters are close to SB3 defaults, with the update ratio matched to SAC
(one gradient step per 10 agent steps), and were not tuned for this task.

Run from the repo root with the sb3 venv (build the training player first):
    C:\\python-envs\\sb3\\Scripts\\python.exe DQN/train_dqn.py --run-id expC_dqn_s1 --seed 1

Writes results/<run-id>/ParkingAgent/ (TensorBoard events with the same Parking/... tags as ML-Agents),
checkpoints, the final SB3 model (.zip) and an ML-Agents-compatible .onnx for the EvaluationRunner.
"""
import argparse
import os
import time
from collections import defaultdict

import numpy as np
import torch
from stable_baselines3 import DQN
from stable_baselines3.common.callbacks import BaseCallback
from torch.utils.tensorboard import SummaryWriter

from export_onnx import export_onnx
from unity_vec_env import UnityParkingVecEnv

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


class ParkingLogger(BaseCallback):
    """Writes ML-Agents-style summaries every `freq` agent steps and saves checkpoints."""

    def __init__(self, out_dir, freq=10_000, checkpoint_every=100_000):
        super().__init__()
        self.out_dir, self.freq, self.checkpoint_every = out_dir, freq, checkpoint_every
        self.writer = SummaryWriter(out_dir)
        self.next_summary, self.next_checkpoint = freq, checkpoint_every
        self.ep_return = None
        self.finished_returns, self.finished_lengths = [], []
        self.ep_len = None
        self.t0 = time.time()

    def _on_training_start(self):
        n = self.training_env.num_envs
        self.ep_return, self.ep_len = np.zeros(n), np.zeros(n, dtype=int)

    def _on_step(self):
        rewards, dones = self.locals["rewards"], self.locals["dones"]
        self.ep_return += rewards
        self.ep_len += 1
        for i in np.flatnonzero(dones):
            self.finished_returns.append(self.ep_return[i])
            self.finished_lengths.append(self.ep_len[i])
            self.ep_return[i], self.ep_len[i] = 0.0, 0

        if self.num_timesteps >= self.next_summary:
            self.next_summary += self.freq
            env = self.training_env
            step = self.num_timesteps
            stats = env.unity_stats
            for key, values in stats.items():
                if values:
                    self.writer.add_scalar(key, float(np.mean(values)), step)
            if self.finished_returns:
                self.writer.add_scalar(
                    "Environment/Cumulative Reward", float(np.mean(self.finished_returns)), step)
                self.writer.add_scalar(
                    "Environment/Episode Length", float(np.mean(self.finished_lengths)), step)
            self.writer.add_scalar(
                "Policy/Epsilon", self.model.exploration_rate, step)
            loss = self.model.logger.name_to_value.get("train/loss")
            if loss is not None:
                self.writer.add_scalar("Losses/TD Loss", loss, step)
            sr = np.mean(stats["Parking/SuccessRate"]
                         ) if stats.get("Parking/SuccessRate") else float("nan")
            rate = step / max(1e-9, time.time() - self.t0)
            print(f"Step: {step}. Success: {sr:.3f}. Mean reward: "
                  f"{np.mean(self.finished_returns) if self.finished_returns else float('nan'):.3f}. "
                  f"Epsilon: {self.model.exploration_rate:.3f}. {rate:.0f} steps/s", flush=True)
            stats.clear()
            self.finished_returns, self.finished_lengths = [], []
            self.writer.flush()

        if self.num_timesteps >= self.next_checkpoint:
            self.next_checkpoint += self.checkpoint_every
            self.model.save(os.path.join(
                self.out_dir, f"ParkingAgent-{self.num_timesteps}"))
        return True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run-id", required=True)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--env", default=os.path.join(ROOT,
                    "Builds", "ParkPilot", "ParkPilot.exe"))
    ap.add_argument("--base-port", type=int, default=5005)
    ap.add_argument("--steps", type=int, default=500_000)
    ap.add_argument("--stage", type=float, default=0.0)
    ap.add_argument("--lr", type=float, default=3e-4)
    ap.add_argument("--force", action="store_true",
                    help="overwrite an existing run folder")
    args = ap.parse_args()

    out_dir = os.path.join(ROOT, "results", args.run_id, "ParkingAgent")
    if os.path.exists(out_dir) and os.listdir(out_dir) and not args.force:
        raise SystemExit(
            f"{out_dir} already exists; use --force to overwrite.")
    os.makedirs(out_dir, exist_ok=True)

    torch.manual_seed(args.seed)
    np.random.seed(args.seed)
    env = UnityParkingVecEnv(args.env, env_params={"curriculum_stage": args.stage, "sparse_reward": 0.0},
                             seed=args.seed, base_port=args.base_port)
    print(f"Connected: {env.num_envs} agents, obs {env.observation_space.shape}, "
          f"{env.action_space.n} discrete actions", flush=True)

    model = DQN(
        "MlpPolicy", env,
        learning_rate=args.lr,
        buffer_size=500_000,
        learning_starts=10_000,
        batch_size=256,
        gamma=0.99,
        train_freq=1,                  # every vector step = 40 agent steps
        # -> one gradient step per 10 agent steps (as SAC)
        gradient_steps=4,
        target_update_interval=10_000,  # agent steps
        exploration_fraction=0.2,      # epsilon 1.0 -> 0.05 over the first 100k steps
        exploration_initial_eps=1.0,
        exploration_final_eps=0.05,
        policy_kwargs=dict(net_arch=[256, 256]),
        seed=args.seed,
        verbose=0,
        device="cpu",
    )
    logger = ParkingLogger(out_dir)
    try:
        model.learn(total_timesteps=args.steps, callback=logger)
    finally:
        model.save(os.path.join(out_dir, "ParkingAgent"))
        env.close()
        logger.writer.close()
    onnx_path = os.path.join(ROOT, "results", args.run_id, "ParkingAgent.onnx")
    export_onnx(model, env.obs_sizes, onnx_path)
    print("Saved", onnx_path)


if __name__ == "__main__":
    main()
