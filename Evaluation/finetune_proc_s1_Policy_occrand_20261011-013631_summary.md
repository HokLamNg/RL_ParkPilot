# Evaluation: finetune_proc_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 71.3% | 13.0% | 14.8% | 1.0% | 0.0% | 0.0% | 5.2 | 1.51 | 7.4 |
| 6 | 400 | 71.3% | 9.0% | 19.0% | 0.8% | 0.0% | 0.0% | 5.3 | 1.17 | 7.1 |
| 10 | 400 | 64.0% | 13.5% | 22.5% | 0.0% | 0.0% | 0.0% | 6.5 | 1.80 | 10.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 71.3% (n=400)
- Stage 6: Inner 69.2% (n=237), Outer 74.2% (n=163)
- Stage 10: Corner 49.2% (n=61), Inner 70.1% (n=154), Outer 60.0% (n=105), Sideways 68.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
