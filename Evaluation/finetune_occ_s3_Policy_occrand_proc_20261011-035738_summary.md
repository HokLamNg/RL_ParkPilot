# Evaluation: finetune_occ_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 68.3% | 23.3% | 6.8% | 1.8% | 0.0% | 0.0% | 5.3 | 1.70 | 9.9 |
| 6 | 400 | 65.5% | 26.3% | 5.8% | 2.5% | 0.0% | 0.0% | 5.4 | 1.73 | 10.8 |
| 10 | 400 | 66.8% | 25.5% | 6.5% | 1.3% | 0.0% | 0.0% | 6.7 | 1.74 | 10.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 68.3% (n=400)
- Stage 6: Inner 65.5% (n=400)
- Stage 10: Inner 66.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
