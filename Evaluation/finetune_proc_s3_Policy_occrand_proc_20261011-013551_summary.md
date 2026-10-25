# Evaluation: finetune_proc_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 60.8% | 19.0% | 19.5% | 0.8% | 0.0% | 0.0% | 4.9 | 1.66 | 8.9 |
| 6 | 400 | 57.3% | 23.8% | 18.3% | 0.8% | 0.0% | 0.0% | 5.3 | 1.74 | 8.9 |
| 10 | 400 | 55.8% | 25.0% | 18.0% | 1.3% | 0.0% | 0.0% | 6.6 | 2.09 | 11.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 60.8% (n=400)
- Stage 6: Inner 57.3% (n=400)
- Stage 10: Inner 55.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
