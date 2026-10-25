# Evaluation: finetune_proc_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 67.5% | 13.5% | 18.5% | 0.5% | 0.0% | 0.0% | 5.3 | 1.30 | 7.2 |
| 6 | 400 | 64.3% | 12.5% | 22.8% | 0.5% | 0.0% | 0.0% | 5.1 | 1.33 | 7.5 |
| 10 | 400 | 65.5% | 15.5% | 18.8% | 0.3% | 0.0% | 0.0% | 6.6 | 1.83 | 10.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 67.5% (n=400)
- Stage 6: Inner 63.3% (n=237), Outer 65.6% (n=163)
- Stage 10: Corner 49.2% (n=61), Inner 70.1% (n=154), Outer 63.8% (n=105), Sideways 71.3% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
