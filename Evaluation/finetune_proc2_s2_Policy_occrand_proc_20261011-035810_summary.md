# Evaluation: finetune_proc2_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 71.5% | 14.8% | 12.3% | 1.0% | 0.5% | 0.0% | 5.3 | 1.52 | 8.3 |
| 6 | 400 | 69.5% | 15.5% | 11.3% | 3.8% | 0.0% | 0.0% | 5.3 | 1.59 | 9.6 |
| 10 | 400 | 61.8% | 21.3% | 13.0% | 3.8% | 0.3% | 0.0% | 6.6 | 2.00 | 12.1 |

**Success by bay group** (per stage):

- Stage 4: Inner 71.5% (n=400)
- Stage 6: Inner 69.5% (n=400)
- Stage 10: Inner 61.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
