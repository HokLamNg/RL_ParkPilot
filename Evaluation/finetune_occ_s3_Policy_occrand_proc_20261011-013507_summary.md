# Evaluation: finetune_occ_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 68.0% | 23.0% | 7.0% | 2.0% | 0.0% | 0.0% | 5.2 | 1.63 | 9.5 |
| 6 | 400 | 67.5% | 26.3% | 5.3% | 1.0% | 0.0% | 0.0% | 5.4 | 1.65 | 9.8 |
| 10 | 400 | 68.8% | 25.0% | 5.0% | 1.3% | 0.0% | 0.0% | 6.7 | 1.73 | 10.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 68.0% (n=400)
- Stage 6: Inner 67.5% (n=400)
- Stage 10: Inner 68.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
