# Evaluation: expD_ppo_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 400 | 99.0% | 0.0% | 1.0% | 0.0% | 0.0% | 0.0% | 4.3 | 0.38 | 1.2 |

**Success by bay group** (per stage):

- Stage 0: Inner 99.0% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
