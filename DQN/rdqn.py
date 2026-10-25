"""Improved DQN for the full ParkPilot task (round 6): trained from scratch, on the GPU.

Experiment C's DQN (train_dqn.py, Stable-Baselines3) was vanilla DQN on stage 0 only, collapsed at PPO's learning rate
(Q-value overestimation) and had no input normalisation. This version adds the standard fixes (the first
run's values, changed for round 6b below):
  * Double DQN targets (the online network picks the next action, the target network values it): less overestimation
  * dueling network (state value + action advantages)
  * n-step returns, 5 in the first run (faster credit assignment over the 10 decisions per second), now --nstep 3
  * prioritised replay (proportional, alpha 0.6, importance weights annealed to 1): learns more from rare hard cases
  * soft target updates (tau 0.005 per gradient step) instead of a hard copy every 10k steps
  * running input normalisation (like ML-Agents' normalize: true), clipped to +-5
  * 25 actions in the first run: steering x throttle, each in {-1, -0.5, 0, 0.5, 1}, now --actions 15
  * per-agent exploration rates (Ape-X): agent i of N uses eps_i = 0.4 ** (1 + 7 i / (N - 1)), so some agents keep
    exploring while others act almost greedily; during the first `--explore-steps` (150k) all agents explore more
  * a curriculum read from a JSON file (DQN/rdqn_curriculum.json in the first run, now DQN/rdqn2_curriculum.json): stages 0-10, then the fine-tuning conditions PPO
    got over rounds 2-6 (geometry and occupancy randomisation, generated car parks, hard cases, far starts). A phase
    ends when the near-greedy agents' success rate reaches its threshold after its minimum number of steps.
It uses the same inputs as the adopted PPO model (68 main rays + 11 vector + 124 near rays), so the exported model can
replace PPO in the showcase. Training statistics go to results/<run-id>/ParkingAgent with the same Parking/... tags as
ML-Agents, so the same TensorBoard views work for both.

Round 6b, after the 41M-step analysis (the action gap, 0.004-0.02, was roughly 10-50x smaller than
the Q error, so the argmax between wait / forward / reverse was noise and the agent got stuck):
  * --init-from: warm start from a checkpoint of the first run (the 21M checkpoints: stage 10 94%, real car parks 81%);
    the 15 actions are a subset of the old 25, so their output rows are copied unchanged
  * --action-repeat 2 (default): each DQN choice is held for 2 decisions (0.2 s), the frame skip of the original DQN:
    each choice moves the reward twice as much, so the gaps between actions grow, and the horizon in choices halves
  * --net mlp (default; raycnn = an optional CNN over ray angle, not used)
  * --actions 15 (default): steering {-1, -0.5, 0, 0.5, 1} x throttle {-1, 0, 1} (half throttle was used 5-10%)
  * --gamma 0.99, --nstep 3 (smaller values, smaller errors; course Week 9 Part B uses a short horizon too)
  * --idle-penalty 0.02 per decision when the car stands still (< 0.1 m/s) more than 1.2 m from the bay centre: a
    clear signal against waiting (as the course's -10 for an invalid move), applied in this trainer only (Unity's reward,
    and so PPO, unchanged)
  * Double DQN, dueling head, prioritised replay kept (they learnt stages 0-10 to 94%)

Train (build the training player first; venv sb3 with CUDA PyTorch):
    C:\\python-envs\\sb3\\Scripts\\python.exe DQN/rdqn.py train --run-id rdqn_s1 --seed 1 --base-port 6005
Resume after a stop (network, optimiser, normaliser and curriculum position are restored; the replay buffer refills):
    ... DQN/rdqn.py train --run-id rdqn_s1 --seed 1 --base-port 6005 --resume
Export a checkpoint to an ML-Agents-compatible .onnx (also done at the end of training):
    ... DQN/rdqn.py export results/rdqn_s1/ParkingAgent/checkpoint.pt results/rdqn_s1/ParkingAgent.onnx
"""
import argparse
import json
import math
import os
import time
from collections import defaultdict, deque
from concurrent.futures import ThreadPoolExecutor

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEVELS = (-1.0, -0.5, 0.0, 0.5, 1.0)
# (continuous action 0, continuous action 1) = (steer, throttle), the order ParkingAgent.OnActionReceived reads.
ACTIONS = np.array([(s, t) for s in LEVELS for t in LEVELS], dtype=np.float32)
ACTION_SETS = {
    25: ACTIONS,
    # no half throttle
    15: np.array([(s, t) for s in LEVELS for t in (-1.0, 0.0, 1.0)], dtype=np.float32),
}


# ---------------------------------------------------------------------------------------------- environment

class MultiUnity:
    """Several Unity instances (40 agents each) stepped in parallel threads (the ML-Agents calls wait on sockets)."""

    def __init__(self, n_envs, file_name, params, seed, base_port, additional_args, action_table=ACTIONS):
        from unity_vec_env import UnityParkingVecEnv
        self.envs = [UnityParkingVecEnv(file_name, env_params=params, seed=seed * 100 + i, base_port=base_port, worker_id=i,
                                        action_table=action_table, additional_args=additional_args) for i in range(n_envs)]
        self.sizes = [e.num_envs for e in self.envs]
        self.num_agents = sum(self.sizes)
        self.obs_sizes = self.envs[0].obs_sizes
        self.pool = ThreadPoolExecutor(max_workers=n_envs)

    def reset(self):
        return np.concatenate([e.reset() for e in self.envs])

    def set_params(self, params):
        for e in self.envs:
            e.set_params(params)

    def step(self, actions, repeat=1):
        """Apply the actions for `repeat` decisions (action repeat). Rewards are summed; an agent whose episode ends during
        the repeat keeps that ending (its later rewards belong to the next episode and are dropped)."""
        obs, rew, done, infos = self._step_once(actions)
        for _ in range(repeat - 1):
            o2, r2, d2, i2 = self._step_once(actions)
            live = ~done
            rew = rew + np.where(live, r2, 0.0)
            for i in np.flatnonzero(live & d2):
                infos[i] = i2[i]
            done = done | d2
            obs = o2
        return obs, rew, done, infos

    def _step_once(self, actions):
        parts, start = [], 0
        for e, n in zip(self.envs, self.sizes):
            parts.append(actions[start:start + n])
            start += n

        def run(e, a):
            e.step_async(a)
            return e.step_wait()

        results = list(self.pool.map(run, self.envs, parts))
        obs = np.concatenate([r[0] for r in results])
        rew = np.concatenate([r[1] for r in results])
        done = np.concatenate([r[2] for r in results])
        infos = [i for r in results for i in r[3]]
        return obs, rew, done, infos

    def drain_stats(self):
        out = defaultdict(list)
        for e in self.envs:
            for k, v in e.unity_stats.items():
                out[k].extend(v)
            e.unity_stats.clear()
        return out

    def close(self):
        for e in self.envs:
            e.close()


# ---------------------------------------------------------------------------------------------- learning parts

class RunningNorm:
    """Running mean / variance of the inputs (batched Welford), normalised and clipped like ML-Agents."""

    def __init__(self, dim):
        self.mean = np.zeros(dim, np.float64)
        self.var = np.ones(dim, np.float64)
        self.count = 1e-4

    def update(self, x):
        bm, bv, bn = x.mean(0), x.var(0), x.shape[0]
        delta, tot = bm - self.mean, self.count + bn
        self.mean = self.mean + delta * bn / tot
        self.var = (self.var * self.count + bv * bn +
                    delta ** 2 * self.count * bn / tot) / tot
        self.count = tot

    def torch_params(self, device):
        return (torch.tensor(self.mean, dtype=torch.float32, device=device),
                torch.tensor(np.sqrt(self.var + 1e-8), dtype=torch.float32, device=device))

    def state(self):
        return {"mean": self.mean, "var": self.var, "count": self.count}

    def load(self, s):
        self.mean, self.var, self.count = s["mean"], s["var"], s["count"]


class DuelingQ(nn.Module):
    def __init__(self, obs_dim, n_actions, hidden=512):
        super().__init__()
        self.trunk = nn.Sequential(nn.Linear(obs_dim, hidden), nn.ReLU(), nn.Linear(hidden, hidden), nn.ReLU(),
                                   nn.Linear(hidden, hidden), nn.ReLU())
        self.value = nn.Sequential(
            nn.Linear(hidden, 256), nn.ReLU(), nn.Linear(256, 1))
        self.adv = nn.Sequential(
            nn.Linear(hidden, 256), nn.ReLU(), nn.Linear(256, n_actions))

    def forward(self, x):
        h = self.trunk(x)
        a = self.adv(h)
        return self.value(h) + a - a.mean(dim=1, keepdim=True)


def ray_order(n):
    """Indices that put a ML-Agents ray sensor's rays in angle order. The sensor stores them alternating
    (centre, then -1, +1, -2, +2, ... steps); angle order is -n..+n steps."""
    return [2 * (-k) - 1 if k < 0 else (0 if k == 0 else 2 * k) for k in range(-n, n + 1)]


class RayCNN(nn.Module):
    """Q-network with a CNN over the ray sensors (course Week 9 Part A, conv + ReLU + max-pool, applied along ray angle).

    Input: the normalised observation [main rays 68 | vector 11 | near rays 124]. Each ray has 4 channels (2 tag flags,
    missed flag, distance). Main sensor: 17 rays over the full circle (circular padding). Near sensor: 31 rays over
    +-150 deg (zero padding). Convolutions are Conv2d with 1 x k kernels on (batch, 4, 1, rays), the most widely supported
    form for Unity's Inference Engine. Then fully connected layers and a dueling head (state value + action advantages).
    """

    def __init__(self, sizes, n_actions, main_dirs=8, near_dirs=15, ch=4, hidden=512):
        super().__init__()
        assert len(sizes) == 3 and sizes[0] == (
            2 * main_dirs + 1) * ch and sizes[2] == (2 * near_dirs + 1) * ch, sizes
        self.sizes, self.ch = list(sizes), ch
        self.n0, self.n2 = 2 * main_dirs + 1, 2 * near_dirs + 1
        self.register_buffer("ord0", torch.tensor(
            ray_order(main_dirs), dtype=torch.long))
        self.register_buffer("ord2", torch.tensor(
            ray_order(near_dirs), dtype=torch.long))
        self.c0a, self.c0b = nn.Conv2d(
            ch, 32, (1, 3)), nn.Conv2d(32, 64, (1, 3))
        self.c2a, self.c2b = nn.Conv2d(ch, 32, (1, 5), padding=(
            0, 2)), nn.Conv2d(32, 64, (1, 5), padding=(0, 2))
        self.pool = nn.MaxPool2d((1, 2))
        self.vec = nn.Sequential(nn.Linear(sizes[1], 64), nn.ReLU())
        flat = 64 * (self.n0 // 2) + 64 * (self.n2 // 2) + 64
        self.trunk = nn.Sequential(
            nn.Linear(flat, hidden), nn.ReLU(), nn.Linear(hidden, hidden), nn.ReLU())
        self.value = nn.Sequential(
            nn.Linear(hidden, 256), nn.ReLU(), nn.Linear(256, 1))
        self.adv = nn.Sequential(
            nn.Linear(hidden, 256), nn.ReLU(), nn.Linear(256, n_actions))

    @staticmethod
    def _wrap(x):
        """Circular padding by one ray on each side (written with cat, so it exports to ONNX everywhere)."""
        return torch.cat([x[:, :, :, -1:], x, x[:, :, :, :1]], dim=3)

    def _rays(self, x, n, order):
        b = x.shape[0]
        r = torch.index_select(x.reshape(b, n, self.ch),
                               1, order)       # rays in angle order
        # (batch, channels, 1, rays)
        return r.permute(0, 2, 1).unsqueeze(2)

    def forward(self, x):
        s0, s1 = self.sizes[0], self.sizes[1]
        m = self._rays(x[:, :s0], self.n0, self.ord0)
        m = F.relu(self.c0a(self._wrap(m)))
        m = self.pool(F.relu(self.c0b(self._wrap(m))))
        n = self._rays(x[:, s0 + s1:], self.n2, self.ord2)
        n = self.pool(F.relu(self.c2b(F.relu(self.c2a(n)))))
        h = torch.cat([m.flatten(1), n.flatten(
            1), self.vec(x[:, s0:s0 + s1])], dim=1)
        h = self.trunk(h)
        a = self.adv(h)
        return self.value(h) + a - a.mean(dim=1, keepdim=True)


def make_net(kind, sizes, n_actions):
    return RayCNN(sizes, n_actions) if kind == "raycnn" else DuelingQ(sum(sizes), n_actions)


class SumTree:
    """Proportional priorities; batched (vectorised) sampling and updates."""

    def __init__(self, capacity):
        self.size = 1 << math.ceil(math.log2(capacity))
        self.tree = np.zeros(2 * self.size, np.float64)

    @property
    def total(self):
        return self.tree[1]

    def update(self, idx, prio):
        pos = np.asarray(idx) + self.size
        self.tree[pos] = prio
        pos = np.unique(pos // 2)
        while pos[0] >= 1:
            self.tree[pos] = self.tree[2 * pos] + self.tree[2 * pos + 1]
            if pos[0] == 1:
                break
            pos = np.unique(pos // 2)

    def find(self, u):
        idx = np.ones(len(u), np.int64)
        u = u.copy()
        while idx[0] < self.size:
            left = 2 * idx
            right = u >= self.tree[left]
            u = np.where(right, u - self.tree[left], u)
            idx = np.where(right, left + 1, left)
        return idx - self.size


class Replay:
    """n-step transitions with prioritised sampling. Observations are stored raw (float16) and normalised when sampled."""

    def __init__(self, capacity, obs_dim, alpha=0.6):
        self.cap, self.alpha, self.n, self.pos = capacity, alpha, 0, 0
        self.obs = np.zeros((capacity, obs_dim), np.float16)
        self.nobs = np.zeros((capacity, obs_dim), np.float16)
        self.act = np.zeros(capacity, np.int16)
        self.ret = np.zeros(capacity, np.float32)
        # gamma^k, or 0 after a real ending (no bootstrap)
        self.disc = np.zeros(capacity, np.float32)
        self.tree = SumTree(capacity)
        self.max_prio = 1.0

    def add_batch(self, items):
        """items: list of (obs, action, n-step return, next obs, discount). One priority-tree update per batch."""
        if not items:
            return
        k = len(items)
        idx = (self.pos + np.arange(k)) % self.cap
        o, a, r, no, d = zip(*items)
        self.obs[idx], self.act[idx], self.ret[idx] = np.stack(o), a, r
        self.nobs[idx], self.disc[idx] = np.stack(no), d
        self.tree.update(idx, np.full(k, self.max_prio ** self.alpha))
        self.pos = (self.pos + k) % self.cap
        self.n = min(self.n + k, self.cap)

    def sample(self, batch, beta, rng):
        total = self.tree.total
        u = (np.arange(batch) + rng.random(batch)) * \
            (total / batch)            # stratified
        idx = np.clip(self.tree.find(np.minimum(
            u, total * (1 - 1e-9))), 0, self.n - 1)
        p = self.tree.tree[idx + self.tree.size] / total
        w = (self.n * np.maximum(p, 1e-12)) ** (-beta)
        return idx, (w / w.max()).astype(np.float32)

    def update(self, idx, td):
        prio = np.abs(td) + 1e-3
        self.max_prio = max(self.max_prio, float(prio.max()))
        self.tree.update(idx, prio ** self.alpha)


class NStep:
    """Per-agent n-step return builder. Finished transitions collect in .out (added to the replay once per step)."""

    def __init__(self, n_agents, n, gamma):
        self.n, self.gamma = n, gamma
        self.q = [deque() for _ in range(n_agents)]
        # finished transitions, collected by the caller
        self.out = []

    def _emit(self, q, next_obs, terminal):
        o, a, _ = q[0]
        ret, g = 0.0, 1.0
        for (_, _, r) in q:
            ret += g * r
            g *= self.gamma
        self.out.append((o, a, ret, next_obs, 0.0 if terminal else g))
        q.popleft()

    def push(self, i, obs, act, rew, next_obs, done, truncated, terminal_obs):
        q = self.q[i]
        q.append((obs, act, rew))
        if done:
            last = terminal_obs if terminal_obs is not None else next_obs
            while q:
                self._emit(q, last, terminal=not truncated)
        elif len(q) == self.n:
            self._emit(q, next_obs, terminal=False)


# ---------------------------------------------------------------------------------------------- training

def load_curriculum(path):
    with open(path) as f:
        c = json.load(f)
    base = c.get("base", {})
    phases = []
    for p in c["phases"]:
        params = dict(base)
        params.update(p.get("params", {}))
        phases.append({"name": p["name"], "params": params, "success": p.get("success", 1.01),
                       "min_steps": int(p.get("min_steps", 0)), "max_steps": int(p.get("max_steps", 10 ** 12))})
    return phases


def train(a):
    from torch.utils.tensorboard import SummaryWriter
    torch.manual_seed(a.seed)
    rng = np.random.default_rng(a.seed)
    device = torch.device("cuda" if torch.cuda.is_available()
                          and a.device != "cpu" else "cpu")
    out = os.path.join(ROOT, "results", a.run_id, "ParkingAgent")
    ck_path = os.path.join(out, "checkpoint.pt")
    if os.path.exists(ck_path) and not (a.resume or a.force):
        raise SystemExit(
            f"{out} has a checkpoint: use --resume to continue or --force to start over.")
    os.makedirs(out, exist_ok=True)

    phases = load_curriculum(a.curriculum)
    phase, phase_start, steps, ck = a.start_phase, 0, 0, None
    if a.resume and os.path.exists(ck_path):
        ck = torch.load(ck_path, map_location="cpu")
        phase, phase_start, steps = ck["phase"], ck["phase_start"], ck["steps"]

    actions = ACTION_SETS[a.actions]
    env = MultiUnity(a.envs, a.env, phases[phase]["params"], a.seed, a.base_port, [
                     "--bay-size-obs", "--near-rays"], actions)
    obs_dim, N, A = sum(env.obs_sizes), env.num_agents, len(actions)
    # vector inputs start after the main rays
    s0 = env.obs_sizes[0]
    print(f"Connected: {a.envs} Unity instances, {N} agents, inputs {env.obs_sizes}, {A} actions, network {a.net}, "
          f"device {device}", flush=True)

    net, tgt = make_net(a.net, env.obs_sizes, A).to(
        device), make_net(a.net, env.obs_sizes, A).to(device)
    opt = torch.optim.Adam(net.parameters(), lr=a.lr, eps=1.5e-4)
    norm = RunningNorm(obs_dim)
    if ck:
        net.load_state_dict(ck["net"])
        tgt.load_state_dict(ck["tgt"])
        opt.load_state_dict(ck["opt"])
        norm.load(ck["norm"])
        print(
            f"Resumed at {steps} steps, phase {phases[phase]['name']}", flush=True)
    else:
        if a.init_from:
            warm_start(net, norm, a.init_from, actions, a.net)
        tgt.load_state_dict(net.state_dict())
    replay = Replay(a.buffer, obs_dim)
    nstep = NStep(N, a.nstep, a.gamma)

    apex = 0.4 ** (1 + 7 * np.arange(N) / max(1, N - 1)
                   )       # per-agent exploration rates
    # the agents whose success drives the curriculum
    greedy = apex <= 0.02
    writer = SummaryWriter(out)
    stats, ep_ret, ep_len = defaultdict(list), np.zeros(N), np.zeros(N, int)
    greedy_success = deque(maxlen=a.success_window)
    losses, qs, gaps, idle_flags = [], [], [], []
    update_credit, next_summary, next_ck = 0.0, steps + \
        a.summary_freq, steps + a.checkpoint_every
    t0, steps0 = time.time(), steps

    o = env.reset()
    norm.update(o)
    try:
        while steps < a.steps:
            # ---- act
            explore = max(0.0, 1.0 - steps /
                          a.explore_steps) if a.explore_steps > 0 else 0.0
            eps = np.maximum(apex, explore)
            mean, std = norm.torch_params(device)
            with torch.no_grad():
                x = torch.as_tensor(o, dtype=torch.float32, device=device)
                q = net(((x - mean) / std).clamp(-5, 5))
                act = q.argmax(1).cpu().numpy()
            rand = rng.random(N) < eps
            act[rand] = rng.integers(0, A, rand.sum())
            no, rew, done, infos = env.step(act, a.action_repeat)

            # ---- store
            for i in range(N):
                trunc = bool(infos[i].get(
                    "TimeLimit.truncated", False)) if done[i] else False
                r = rew[i]
                if a.idle_penalty > 0 and not done[i]:
                    # standing still away from the bay: obs slot 6 = speed / 5, slots 1-2 = bay position / 30 (car frame)
                    speed = abs(no[i, s0 + 5]) * 5.0
                    dist = 30.0 * math.hypot(no[i, s0], no[i, s0 + 1])
                    idle = speed < a.idle_speed and dist > a.idle_dist
                    idle_flags.append(idle)
                    if idle:
                        r -= a.idle_penalty
                nstep.push(i, o[i], act[i], r, no[i], bool(
                    done[i]), trunc, infos[i].get("terminal_observation"))
                ep_ret[i] += rew[i]
                ep_len[i] += 1
                if done[i]:
                    stats["Environment/Cumulative Reward"].append(ep_ret[i])
                    stats["Environment/Episode Length"].append(ep_len[i])
                    if greedy[i]:
                        # Parked ends with the +2 success reward; every other real ending is <= 0 at its last step.
                        greedy_success.append(
                            1.0 if (not trunc and rew[i] > 1.0) else 0.0)
                    ep_ret[i], ep_len[i] = 0.0, 0
            replay.add_batch(nstep.out)
            nstep.out.clear()
            norm.update(no)
            o = no
            # Unity decisions, comparable with every other run
            steps += N * a.action_repeat

            # ---- learn
            if replay.n >= a.learning_starts:
                update_credit += N * a.replay_ratio / a.batch          # per stored transition
                beta = min(1.0, 0.4 + 0.6 * steps / a.steps)
                # linear decay to 10%
                lr = a.lr * max(0.1, 1.0 - steps /
                                (a.lr_decay_steps or a.steps))
                for g in opt.param_groups:
                    g["lr"] = lr
                mean, std = norm.torch_params(device)
                while update_credit >= 1.0:
                    update_credit -= 1.0
                    idx, w = replay.sample(a.batch, beta, rng)
                    ob = ((torch.as_tensor(
                        replay.obs[idx], dtype=torch.float32, device=device) - mean) / std).clamp(-5, 5)
                    nb = ((torch.as_tensor(
                        replay.nobs[idx], dtype=torch.float32, device=device) - mean) / std).clamp(-5, 5)
                    ac = torch.as_tensor(
                        replay.act[idx].astype(np.int64), device=device)
                    rt = torch.as_tensor(replay.ret[idx], device=device)
                    dc = torch.as_tensor(replay.disc[idx], device=device)
                    q_all = net(ob)
                    qa = q_all.gather(1, ac[:, None]).squeeze(1)
                    with torch.no_grad():
                        best = net(nb).argmax(1, keepdim=True)
                        y = rt + dc * tgt(nb).gather(1, best).squeeze(1)
                    td = y - qa
                    loss = (torch.as_tensor(w, device=device) *
                            F.smooth_l1_loss(qa, y, reduction="none")).mean()
                    opt.zero_grad(set_to_none=True)
                    loss.backward()
                    nn.utils.clip_grad_norm_(net.parameters(), 10.0)
                    opt.step()
                    with torch.no_grad():
                        torch._foreach_mul_(list(tgt.parameters()), 1 - a.tau)
                        torch._foreach_add_(list(tgt.parameters()), list(
                            net.parameters()), alpha=a.tau)
                    replay.update(idx, td.detach().cpu().numpy())
                    losses.append(loss.item())
                    qs.append(qa.mean().item())
                    # best minus second-best Q (the action gap)
                    if len(gaps) < 50:
                        top2 = torch.topk(q_all.detach(), 2, dim=1).values
                        gaps.append((top2[:, 0] - top2[:, 1]).median().item())

            # ---- curriculum
            p = phases[phase]
            in_phase = steps - phase_start
            gs = float(np.mean(greedy_success)) if len(
                greedy_success) >= a.success_window // 2 else 0.0
            if phase + 1 < len(phases) and in_phase >= p["min_steps"] and (gs >= p["success"] or in_phase >= p["max_steps"]):
                # re-read: edits to later phases apply without a restart
                phases = load_curriculum(a.curriculum)
                phase += 1
                phase_start = steps
                greedy_success.clear()
                env.set_params(phases[phase]["params"])
                print(
                    f"Step {steps}: phase {p['name']} done (near-greedy success {gs:.3f}) -> {phases[phase]['name']}", flush=True)

            # ---- log
            for k, v in env.drain_stats().items():
                stats[k].extend(v)
            if steps >= next_summary:
                next_summary += a.summary_freq
                for k, v in stats.items():
                    if v:
                        writer.add_scalar(k, float(np.mean(v)), steps)
                writer.add_scalar("Policy/Epsilon (mean)",
                                  float(eps.mean()), steps)
                writer.add_scalar("Curriculum/Phase", phase, steps)
                writer.add_scalar("Curriculum/NearGreedySuccess", gs, steps)
                if losses:
                    writer.add_scalar("Losses/TD Loss",
                                      float(np.mean(losses)), steps)
                    writer.add_scalar("Policy/Q (taken action)",
                                      float(np.mean(qs)), steps)
                if gaps:
                    writer.add_scalar(
                        "Policy/Action gap (median)", float(np.mean(gaps)), steps)
                if idle_flags:
                    writer.add_scalar(
                        "Policy/Idle share (standing still away from the bay)", float(np.mean(idle_flags)), steps)
                rate = (steps - steps0) / max(1e-9, time.time() - t0)
                sr = np.mean(
                    stats["Parking/SuccessRate"]) if stats.get("Parking/SuccessRate") else float("nan")
                print(f"Step: {steps}. Phase {phases[phase]['name']}. Success {sr:.3f} (near-greedy {gs:.3f}). "
                      f"Loss {np.mean(losses) if losses else float('nan'):.4f}. {rate:.0f} steps/s", flush=True)
                stats.clear()
                losses.clear()
                qs.clear()
                gaps.clear()
                idle_flags.clear()
                writer.flush()
            if steps >= next_ck:
                next_ck += a.checkpoint_every
                save(ck_path, net, tgt, opt, norm, steps, phase,
                     phase_start, env.obs_sizes, a.net, actions)
                save(os.path.join(out, f"ParkingAgent-{steps}.pt"), net, tgt, opt, norm, steps, phase, phase_start,
                     env.obs_sizes, a.net, actions)
    finally:
        save(ck_path, net, tgt, opt, norm, steps, phase,
             phase_start, env.obs_sizes, a.net, actions)
        env.close()
        writer.close()
    onnx_path = os.path.join(ROOT, "results", a.run_id, "ParkingAgent.onnx")
    export(ck_path, onnx_path)


def warm_start(net, norm, path, actions, net_kind):
    """Copy a checkpoint into a new network. Layers of the same shape are copied; the advantage output layer is copied
    row by row for every action that exists in the old action table (15 of the old 25), the rest stays new."""
    ck = torch.load(path, map_location="cpu")
    assert ck.get(
        "net_kind", "mlp") == net_kind, "warm start needs the same network type"
    old = ck["net"]
    new = net.state_dict()
    old_actions = [tuple(np.round(r, 3)) for r in np.asarray(ck["actions"])]
    rows = [old_actions.index(tuple(np.round(r, 3))) if tuple(
        np.round(r, 3)) in old_actions else -1 for r in actions]
    copied, mapped = 0, 0
    for k, v in new.items():
        if k in old and old[k].shape == v.shape:
            new[k] = old[k].clone()
            copied += 1
        elif k in old and k.startswith("adv.") and old[k].shape[1:] == v.shape[1:]:
            for j, r in enumerate(rows):
                if r >= 0:
                    new[k][j] = old[k][r]
            mapped += 1
    net.load_state_dict(new)
    norm.load(ck["norm"])
    print(f"Warm start from {path}: {copied} tensors copied, {mapped} output tensors mapped "
          f"({sum(r >= 0 for r in rows)}/{len(rows)} actions found), normaliser copied", flush=True)


def save(path, net, tgt, opt, norm, steps, phase, phase_start, obs_sizes, net_kind="mlp", actions=ACTIONS):
    torch.save({"net": net.state_dict(), "tgt": tgt.state_dict(), "opt": opt.state_dict(), "norm": norm.state(),
                "steps": steps, "phase": phase, "phase_start": phase_start, "obs_sizes": list(obs_sizes),
                "net_kind": net_kind, "actions": np.asarray(actions).tolist()}, path)


# ---------------------------------------------------------------------------------------------- export

class ForUnity(nn.Module):
    """ML-Agents-compatible graph: separate sensor inputs, normalisation inside, greedy action looked up in the table."""

    def __init__(self, net, mean, std, actions, n_inputs):
        super().__init__()
        self.net, self.n_inputs = net, n_inputs
        self.mean = nn.Parameter(torch.tensor(
            mean, dtype=torch.float32), requires_grad=False)
        self.std = nn.Parameter(torch.tensor(
            std, dtype=torch.float32), requires_grad=False)
        self.table = nn.Parameter(torch.tensor(
            actions, dtype=torch.float32), requires_grad=False)
        self.version_number = nn.Parameter(
            torch.tensor([3.0]), requires_grad=False)
        self.memory_size = nn.Parameter(
            torch.tensor([0.0]), requires_grad=False)
        self.action_shape = nn.Parameter(
            torch.tensor([2.0]), requires_grad=False)

    def forward(self, *obs):
        x = torch.cat(obs, dim=1)
        q = self.net(torch.clamp((x - self.mean) / self.std, -5.0, 5.0))
        actions = torch.index_select(self.table, 0, torch.argmax(q, dim=1))
        return self.version_number, self.memory_size, actions, self.action_shape, actions


def export(ck_path, onnx_path):
    import onnxruntime as ort
    ck = torch.load(ck_path, map_location="cpu")
    sizes = ck["obs_sizes"]
    net = make_net(ck.get("net_kind", "mlp"), sizes, len(ck["actions"]))
    net.load_state_dict(ck["net"])
    net.eval()
    std = np.sqrt(ck["norm"]["var"] + 1e-8)
    model = ForUnity(net, ck["norm"]["mean"], std, np.asarray(
        ck["actions"], np.float32), len(sizes)).eval()
    names = [f"obs_{i}" for i in range(len(sizes))]
    dummy = tuple(torch.zeros(1, s) for s in sizes)
    torch.onnx.export(model, dummy, onnx_path, opset_version=11, input_names=names,
                      output_names=["version_number", "memory_size", "continuous_actions",
                                    "continuous_action_output_shape", "deterministic_continuous_actions"],
                      dynamic_axes={**{n: {0: "batch"} for n in names},
                                    "continuous_actions": {0: "batch"}, "deterministic_continuous_actions": {0: "batch"}})
    rng = np.random.default_rng(0)
    xs = [rng.random((32, s), dtype=np.float32) for s in sizes]
    got = ort.InferenceSession(onnx_path).run(None, dict(zip(names, xs)))
    ref = model(*[torch.from_numpy(x) for x in xs])[2].detach().numpy()
    assert np.allclose(got[2], ref) and np.allclose(
        got[4], ref), "ONNX export mismatch"
    print(
        f"Saved {onnx_path} (inputs {sizes}, {len(ck['actions'])} actions, {ck['steps']} steps), checked with onnxruntime")


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    t = sub.add_parser("train")
    t.add_argument("--run-id", required=True)
    t.add_argument("--seed", type=int, default=1)
    t.add_argument("--env", default=os.path.join(ROOT,
                   "Builds", "ParkPilot", "ParkPilot.exe"))
    t.add_argument("--envs", type=int, default=4,
                   help="Unity instances (40 agents each)")
    t.add_argument("--base-port", type=int, default=6005)
    t.add_argument("--curriculum", default=os.path.join(ROOT,
                   "DQN", "rdqn2_curriculum.json"))
    t.add_argument("--steps", type=int, default=150_000_000,
                   help="agent decisions (as ML-Agents counts steps)")
    t.add_argument("--lr", type=float, default=1e-4)
    t.add_argument("--gamma", type=float, default=0.99)
    t.add_argument("--nstep", type=int, default=3)
    t.add_argument("--net", default="mlp", choices=["mlp", "raycnn"])
    t.add_argument("--init-from", default="",
                   help="warm start from a checkpoint file (e.g. results/rdqn_s1/ParkingAgent/ParkingAgent-21000000.pt)")
    t.add_argument("--start-phase", type=int, default=0,
                   help="curriculum phase to start in (11 = real car parks)")
    t.add_argument("--action-repeat", type=int, default=2,
                   help="decisions each DQN choice is held for (frame skip)")
    t.add_argument("--actions", type=int, default=15,
                   choices=sorted(ACTION_SETS))
    t.add_argument("--idle-penalty", type=float, default=0.02,
                   help="per decision standing still away from the bay (0 = off)")
    t.add_argument("--idle-speed", type=float, default=0.1,
                   help="m/s below which the car counts as standing still")
    t.add_argument("--idle-dist", type=float, default=1.2,
                   help="m from the bay centre beyond which standing still is idle")
    t.add_argument("--batch", type=int, default=512)
    t.add_argument("--buffer", type=int, default=1_000_000)
    t.add_argument("--learning-starts", type=int, default=50_000)
    t.add_argument("--replay-ratio", type=float, default=16.0,
                   help="sampled transitions per new transition (Experiment C's SB3 DQN: 25.6)")
    t.add_argument("--tau", type=float, default=0.005)
    t.add_argument("--lr-decay-steps", type=int, default=0,
                   help="learning rate decays linearly to 10%% over these steps (0 = over --steps)")
    t.add_argument("--explore-steps", type=int, default=150_000,
                   help="extra exploration for all agents, decaying 1 -> 0 over these steps (then only the per-agent rates)")
    t.add_argument("--success-window", type=int, default=2000,
                   help="near-greedy episodes behind the curriculum decision")
    t.add_argument("--summary-freq", type=int, default=50_000)
    t.add_argument("--checkpoint-every", type=int, default=1_000_000)
    t.add_argument("--device", default="cuda")
    t.add_argument("--resume", action="store_true")
    t.add_argument("--force", action="store_true")
    e = sub.add_parser("export")
    e.add_argument("checkpoint")
    e.add_argument("onnx")
    a = ap.parse_args()
    if a.cmd == "train":
        train(a)
    else:
        export(a.checkpoint, a.onnx)


if __name__ == "__main__":
    main()
