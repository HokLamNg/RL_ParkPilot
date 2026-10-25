# Evaluation: finetune_proc_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 62.0% | 19.8% | 15.3% | 3.0% | 0.0% | 0.0% | 5.2 | 1.89 | 10.7 |
| 6 | 400 | 59.0% | 21.0% | 16.3% | 3.8% | 0.0% | 0.0% | 5.2 | 1.89 | 10.9 |
| 10 | 400 | 52.8% | 24.3% | 20.3% | 2.5% | 0.3% | 0.0% | 6.7 | 2.26 | 14.1 |

**Success by bay group** (per stage):

- Stage 4: Inner 62.0% (n=400)
- Stage 6: Inner 59.0% (n=400)
- Stage 10: Inner 52.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
