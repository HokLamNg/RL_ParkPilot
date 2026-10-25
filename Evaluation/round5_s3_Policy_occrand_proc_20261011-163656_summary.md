# Evaluation: round5_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 85.0% | 8.0% | 7.0% | 0.0% | 0.0% | 0.0% | 4.8 | 0.94 | 4.0 |
| 6 | 400 | 84.3% | 8.5% | 7.0% | 0.3% | 0.0% | 0.0% | 4.9 | 0.92 | 4.7 |
| 10 | 400 | 86.0% | 9.3% | 4.8% | 0.0% | 0.0% | 0.0% | 6.1 | 0.94 | 5.1 |

**Success by bay group** (per stage):

- Stage 4: Inner 85.0% (n=400)
- Stage 6: Inner 84.3% (n=400)
- Stage 10: Inner 86.0% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
