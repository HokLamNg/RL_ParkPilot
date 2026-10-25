# Evaluation: expD_ppo_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 400 | 98.3% | 0.0% | 1.8% | 0.0% | 0.0% | 0.0% | 3.9 | 0.26 | 1.2 |

**Success by bay group** (per stage):

- Stage 0: Inner 98.3% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
