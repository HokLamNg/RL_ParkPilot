# Evaluation: rdqn2_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 89.5% | 1.3% | 8.3% | 1.0% | 0.0% | 0.0% | 5.1 | 0.71 | 3.5 |
| 6 | 400 | 91.5% | 1.5% | 7.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.58 | 2.7 |
| 10 | 400 | 87.0% | 2.0% | 10.0% | 0.8% | 0.3% | 0.0% | 6.3 | 0.74 | 3.9 |

**Success by bay group** (per stage):

- Stage 4: Inner 89.5% (n=400)
- Stage 6: Inner 91.5% (n=400)
- Stage 10: Inner 87.0% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
