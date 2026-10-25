# Evaluation: round6_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 93.0% | 3.8% | 3.0% | 0.3% | 0.0% | 0.0% | 5.0 | 0.60 | 2.6 |
| 6 | 400 | 91.8% | 5.5% | 2.8% | 0.0% | 0.0% | 0.0% | 5.1 | 0.64 | 3.0 |
| 10 | 400 | 90.0% | 5.8% | 4.3% | 0.0% | 0.0% | 0.0% | 6.3 | 0.62 | 3.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 93.0% (n=400)
- Stage 6: Inner 91.8% (n=400)
- Stage 10: Inner 90.0% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
