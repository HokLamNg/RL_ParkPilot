"""Why did DQN (lr 3e-4) collapse? Compare each checkpoint's Q-values with real returns on the same states.

Drives the good 200k checkpoint greedily, records the visited states and the discounted returns actually
obtained, then asks every checkpoint of the run: what is Q of the action actually taken (vs the true return),
what is max Q, and which action would it pick instead?

    C:\\python-envs\\sb3\\Scripts\\python.exe DQN/diagnose_collapse.py expC_dqn_s1
"""
import os
import sys

import numpy as np
import torch
from stable_baselines3 import DQN

from unity_vec_env import ACTION_TABLE, UnityParkingVecEnv

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAMMA = 0.99


def main(run, vec_steps=300):
    ck_dir = os.path.join(ROOT, "results", run, "ParkingAgent")
    ref = DQN.load(os.path.join(
        ck_dir, "ParkingAgent-200000.zip"), device="cpu")
    env = UnityParkingVecEnv(os.path.join(ROOT, "Builds", "ParkPilot", "ParkPilot.exe"),
                             env_params={"curriculum_stage": 0, "sparse_reward": 0}, seed=123)
    obs = env.reset()
    O, A, R, D = [], [], [], []
    for _ in range(vec_steps):
        act, _ = ref.predict(obs, deterministic=True)
        nxt, rew, done, _ = env.step(act)
        O.append(obs)
        A.append(act)
        R.append(rew)
        D.append(done)
        obs = nxt
    env.close()
    O, A, R, D = map(np.array, (O, A, R, D))  # [T, n_envs, ...]

    # Discounted Monte-Carlo return per transition; drop each env's unfinished last episode.
    T, n = R.shape
    G = np.full((T, n), np.nan)
    for e in range(n):
        g, valid = 0.0, False
        for t in reversed(range(T)):
            if D[t, e]:
                g, valid = 0.0, True
            g = R[t, e] + GAMMA * g
            if valid:
                G[t, e] = g
    keep = ~np.isnan(G)
    O, A, G = O[keep], A[keep], G[keep]
    print(f"{len(G)} transitions from complete episodes, mean true return {G.mean():.2f}")

    x = torch.tensor(O, dtype=torch.float32)
    print(f"{'checkpoint':>10} {'Q(taken)':>9} {'true G':>7} {'over-est':>9} {'max Q':>7} {'same action':>12} {'top choices'}")
    for step in (100_000, 200_000, 300_000, 400_000, 500_000):
        m = DQN.load(os.path.join(
            ck_dir, f"ParkingAgent-{step}.zip"), device="cpu")
        with torch.no_grad():
            q = m.policy.q_net(x).numpy()
        q_taken = q[np.arange(len(A)), A]
        greedy = q.argmax(1)
        counts = np.bincount(greedy, minlength=len(ACTION_TABLE)) / len(greedy)
        top = ", ".join(f"steer {ACTION_TABLE[i][0]:+.1f}/thr {ACTION_TABLE[i][1]:+.0f}: {100 * counts[i]:.0f}%"
                        for i in counts.argsort()[::-1][:2])
        print(f"{step:>10} {q_taken.mean():9.2f} {G.mean():7.2f} {q_taken.mean() - G.mean():+9.2f} "
              f"{q.max(1).mean():7.2f} {100 * (greedy == A).mean():11.0f}% {top}")


if __name__ == "__main__":
    main(sys.argv[1])
