# Evaluation: expD_sac_s3 (Policy, deterministic)

Throttle dead zone set to 0.00 for this evaluation (trained with 0.15).

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 400 | 91.5% | 0.0% | 6.0% | 2.5% | 0.0% | 0.0% | 4.6 | 0.41 | 2.7 |

**Success by bay group** (per stage):

- Stage 0: Inner 91.5% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
