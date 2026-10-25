# Evaluation: finetune_proc2_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 55.0% | 18.8% | 24.5% | 1.8% | 0.0% | 0.0% | 4.8 | 1.89 | 11.5 |
| 6 | 400 | 61.3% | 15.5% | 21.3% | 2.0% | 0.0% | 0.0% | 5.1 | 1.89 | 13.2 |
| 10 | 400 | 48.8% | 27.3% | 22.3% | 1.8% | 0.0% | 0.0% | 6.6 | 2.38 | 14.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 55.0% (n=400)
- Stage 6: Inner 61.3% (n=400)
- Stage 10: Inner 48.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
