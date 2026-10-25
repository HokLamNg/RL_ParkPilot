# Evaluation: finetune_proc_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 64.8% | 16.0% | 17.3% | 1.8% | 0.3% | 0.0% | 5.1 | 1.78 | 9.8 |
| 6 | 400 | 63.5% | 18.0% | 14.8% | 3.8% | 0.0% | 0.0% | 5.3 | 1.80 | 10.6 |
| 10 | 400 | 62.3% | 20.8% | 15.5% | 1.5% | 0.0% | 0.0% | 6.6 | 1.92 | 11.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 64.8% (n=400)
- Stage 6: Inner 63.5% (n=400)
- Stage 10: Inner 62.3% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
