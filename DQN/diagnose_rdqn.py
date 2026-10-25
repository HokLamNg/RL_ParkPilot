"""Why did the round 6 DQN stop improving? Runs saved checkpoints GREEDILY (no exploration) in the Unity training build
under fixed conditions and records, decision by decision, what the network does.

    C:\\python-envs\\sb3\\Scripts\\python.exe DQN/diagnose_rdqn.py rdqn_s1 13000080 21000000 31000080 41000040 --episodes 15

For each checkpoint and condition (stage 10 plain / real car parks / generated car parks) it reports:
  outcomes (Unity's own statistics), action usage (throttle and steering levels), how often the greedy action stands
  still or reverses direction, the action gap (best minus second-best Q) in normal driving and in the decisions before
  a stuck ending, and Q at the start of an episode against the discounted return actually obtained (overestimation).

Unity gets each checkpoint's own action table (25 actions in the first run, 15 in round 6b) and every choice is held
for the action repeat the model was trained with (--action-repeat, default auto: 2 for 15-action checkpoints, 1 for
25-action ones). "Decision" below means one DQN choice.
"""
import argparse
import os
from collections import Counter, defaultdict

import numpy as np
import torch

import rdqn

CONDITIONS = {
    "stage10": {"curriculum_stage": 10},
    "real_car_parks": {"curriculum_stage": 10, "geometry_randomization": 1, "geometry_share": 0.5,
                       "occupancy_randomization": 1},
    "generated": {"curriculum_stage": 10, "geometry_randomization": 1, "occupancy_randomization": 1,
                  "procedural_layout": 1.0, "procedural_difficulty": 2},
}
ZERO = {"geometry_randomization": 0, "geometry_share": 1, "occupancy_randomization": 0, "procedural_layout": 0,
        "procedural_difficulty": 2, "hard_case_share": 0, "far_start_share": 0, "stuck_steps_near_bay": 0,
        "sparse_reward": 0, "early_endings": 1}


def load_net(run, step, obs_dim, device):
    path = os.path.join(rdqn.ROOT, "results", run,
                        "ParkingAgent", f"ParkingAgent-{step}.pt")
    ck = torch.load(path, map_location="cpu")
    net = rdqn.make_net(ck.get("net_kind", "mlp"),
                        ck["obs_sizes"], len(ck["actions"])).to(device)
    net.load_state_dict(ck["net"])
    net.eval()
    mean = torch.tensor(ck["norm"]["mean"], dtype=torch.float32, device=device)
    std = torch.tensor(np.sqrt(ck["norm"]["var"] + 1e-8),
                       dtype=torch.float32, device=device)
    return net, mean, std, np.asarray(ck["actions"], np.float32)


def run_condition(env, net, mean, std, episodes, gamma, device, repeat=1):
    N = env.num_agents
    o = env.reset()
    # finished episodes per agent (the first one is discarded)
    seen = np.zeros(N, int)
    # per agent: (q vector, action, reward, speed)
    traj = [[] for _ in range(N)]
    stats = defaultdict(list)
    eps = []
    env.drain_stats()
    while seen.min() < episodes + 1:
        with torch.no_grad():
            q = net(((torch.as_tensor(o, dtype=torch.float32,
                    device=device) - mean) / std).clamp(-5, 5)).cpu().numpy()
        act = q.argmax(1)
        no, rew, done, infos = env.step(act, repeat)
        # vector observation slot 6: forward speed / 5 m/s
        speed = o[:, 68 + 5] * 5.0
        for i in range(N):
            traj[i].append((q[i], act[i], rew[i], speed[i]))
            if done[i]:
                if seen[i] >= 1 and seen[i] <= episodes:
                    trunc = bool(infos[i].get("TimeLimit.truncated", False))
                    eps.append((traj[i], rew[i], trunc))
                seen[i] += 1
                traj[i] = []
        o = no
        for k, v in env.drain_stats().items():
            if seen.min() >= 1:
                stats[k].extend(v)
    return eps, stats


def summarise(eps, stats, gamma, actions, repeat=1):
    res = {}
    for k in ("Parking/SuccessRate", "Parking/CollisionRate", "Parking/StuckRate", "Parking/NoProgressRate", "Parking/TimeoutRate"):
        res[k.split("/")[1]] = float(np.mean(stats[k])
                                     ) if stats.get(k) else float("nan")
    acts = Counter()
    gaps_all, gaps_stuck, starts_q, starts_g = [], [], [], []
    still_stuck, flips_stuck, still_all = [], [], []
    for tr, last_r, trunc in eps:
        a = np.array([t[1] for t in tr])
        thr = actions[a, 1]
        acts.update(a.tolist())
        q = np.array([t[0] for t in tr])
        top2 = np.sort(q, 1)[:, -2:]
        gap = top2[:, 1] - top2[:, 0]
        gaps_all.extend(gap.tolist())
        still_all.append(np.mean(thr == 0))
        # discounted return actually obtained from the first decision, against Q there
        rews = np.array([t[2] for t in tr])
        g = float(np.sum(rews * gamma ** np.arange(len(rews))))
        starts_q.append(float(q[0].max()))
        starts_g.append(g)
        speeds = np.array([t[3] for t in tr])
        stuck = (not trunc) and last_r < - \
            0.5 and np.mean(np.abs(speeds[-max(1, 10 // repeat):])) < 0.3   # last 1 s
        if stuck:
            tail = slice(max(0, len(tr) - 30 // repeat), len(tr))      # the last 3 s (30 decisions of 0.1 s)
            gaps_stuck.extend(gap[tail].tolist())
            still_stuck.append(np.mean(thr[tail] == 0))
            t2 = thr[tail]
            nz = t2[t2 != 0]
            flips_stuck.append(
                np.mean(np.sign(nz[1:]) != np.sign(nz[:-1])) if len(nz) > 1 else 0.0)
    tot = sum(acts.values())
    thr_use = Counter()
    steer_use = Counter()
    for a, n in acts.items():
        thr_use[actions[a, 1]] += n
        steer_use[actions[a, 0]] += n
    res["episodes"] = len(eps)
    res["throttle use"] = {
        f"{k:+.1f}": f"{100 * v / tot:.0f}%" for k, v in sorted(thr_use.items())}
    res["steer use"] = {
        f"{k:+.1f}": f"{100 * v / tot:.0f}%" for k, v in sorted(steer_use.items())}
    res["action gap median (all decisions)"] = float(
        np.median(gaps_all)) if gaps_all else float("nan")
    res["action gap median (last 3 s before stuck)"] = float(
        np.median(gaps_stuck)) if gaps_stuck else float("nan")
    res["standing still share (all)"] = float(
        np.mean(still_all)) if still_all else float("nan")
    res["standing still share (last 3 s before stuck)"] = float(
        np.mean(still_stuck)) if still_stuck else float("nan")
    res["direction flips (last 3 s before stuck)"] = float(
        np.mean(flips_stuck)) if flips_stuck else float("nan")
    res["stuck episodes seen"] = len(still_stuck)
    res["Q at start (mean)"] = float(np.mean(starts_q))
    res["return actually obtained (mean)"] = float(np.mean(starts_g))
    return res


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("run")
    ap.add_argument("steps", nargs="+", type=int,
                    help="checkpoint step numbers (ParkingAgent-<step>.pt)")
    ap.add_argument("--conditions", nargs="+", default=list(CONDITIONS))
    ap.add_argument("--episodes", type=int, default=15,
                    help="episodes per agent (40 agents)")
    ap.add_argument("--base-port", type=int, default=6305)
    ap.add_argument("--gamma", type=float, default=0.99)   # rdqn round 6b trains with 0.99
    ap.add_argument("--action-repeat", type=int, default=0,
                    help="decisions each choice is held for, 0 = auto (2 for 15-action checkpoints, 1 for 25-action ones)")
    a = ap.parse_args()
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    first = torch.load(os.path.join(rdqn.ROOT, "results", a.run, "ParkingAgent", f"ParkingAgent-{a.steps[0]}.pt"),
                       map_location="cpu")
    env = rdqn.MultiUnity(1, os.path.join(rdqn.ROOT, "Builds", "ParkPilot", "ParkPilot.exe"), ZERO, 7, a.base_port,
                          ["--bay-size-obs", "--near-rays"], action_table=np.asarray(first["actions"], np.float32))
    obs_dim = sum(env.obs_sizes)
    try:
        for step in a.steps:
            net, mean, std, actions = load_net(a.run, step, obs_dim, device)
            for e in env.envs:                      # Unity must decode the indices with this checkpoint's table
                e.action_table = actions
            repeat = a.action_repeat or (2 if len(actions) == 15 else 1)
            for c in a.conditions:
                params = dict(ZERO)
                params.update(CONDITIONS[c])
                env.set_params(params)
                eps, stats = run_condition(
                    env, net, mean, std, a.episodes, a.gamma, device, repeat)
                r = summarise(eps, stats, a.gamma, actions, repeat)
                print(f"\n=== {a.run} @ {step / 1e6:.1f}M, {c}: success {100 * r['SuccessRate']:.1f}%, collision "
                      f"{100 * r['CollisionRate']:.1f}%, stuck {100 * r['StuckRate']:.1f}%, no progress {100 * r['NoProgressRate']:.1f}% "
                      f"({r['episodes']} episodes)", flush=True)
                for k in ("throttle use", "steer use", "standing still share (all)", "standing still share (last 3 s before stuck)",
                          "direction flips (last 3 s before stuck)", "stuck episodes seen", "action gap median (all decisions)",
                          "action gap median (last 3 s before stuck)", "Q at start (mean)", "return actually obtained (mean)"):
                    v = r[k]
                    print(
                        f"    {k}: {v if isinstance(v, (dict, int)) else f'{v:.4f}'}", flush=True)
    finally:
        env.close()


if __name__ == "__main__":
    main()
