# Evaluation: finetune_proc2_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 69.5% | 17.5% | 12.5% | 0.5% | 0.0% | 0.0% | 5.1 | 1.55 | 8.6 |
| 6 | 400 | 71.0% | 18.0% | 8.0% | 3.0% | 0.0% | 0.0% | 5.3 | 1.55 | 9.2 |
| 10 | 400 | 64.0% | 24.5% | 8.0% | 3.5% | 0.0% | 0.0% | 6.8 | 1.99 | 12.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 69.5% (n=400)
- Stage 6: Inner 71.0% (n=400)
- Stage 10: Inner 64.0% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
