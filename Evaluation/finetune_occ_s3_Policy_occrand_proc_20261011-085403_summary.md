# Evaluation: finetune_occ_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 67.5% | 24.3% | 7.3% | 1.0% | 0.0% | 0.0% | 5.2 | 1.64 | 9.3 |
| 6 | 400 | 67.3% | 26.5% | 4.8% | 1.5% | 0.0% | 0.0% | 5.3 | 1.73 | 9.9 |
| 10 | 400 | 67.8% | 26.0% | 5.3% | 1.0% | 0.0% | 0.0% | 6.7 | 1.73 | 10.4 |

**Success by bay group** (per stage):

- Stage 4: Inner 67.5% (n=400)
- Stage 6: Inner 67.3% (n=400)
- Stage 10: Inner 67.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
