# Evaluation: expC_dqn_s2_200k (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 400 | 96.8% | 0.0% | 3.3% | 0.0% | 0.0% | 0.0% | 3.2 | 0.21 | 2.5 |

**Success by bay group** (per stage):

- Stage 0: Inner 96.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
