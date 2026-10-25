# Evaluation: round4_bay_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 69.8% | 16.3% | 14.0% | 0.0% | 0.0% | 0.0% | 4.9 | 1.59 | 7.9 |
| 6 | 400 | 67.3% | 20.8% | 11.8% | 0.3% | 0.0% | 0.0% | 5.0 | 1.72 | 9.2 |
| 10 | 400 | 64.3% | 24.5% | 10.3% | 0.8% | 0.3% | 0.0% | 6.6 | 1.86 | 10.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 69.8% (n=400)
- Stage 6: Inner 67.3% (n=400)
- Stage 10: Inner 64.3% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
