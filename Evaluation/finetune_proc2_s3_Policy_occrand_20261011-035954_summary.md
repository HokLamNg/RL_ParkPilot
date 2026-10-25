# Evaluation: finetune_proc2_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 73.0% | 14.8% | 11.8% | 0.5% | 0.0% | 0.0% | 5.1 | 1.36 | 7.6 |
| 6 | 400 | 77.0% | 12.5% | 10.3% | 0.3% | 0.0% | 0.0% | 5.0 | 1.28 | 8.1 |
| 10 | 400 | 76.5% | 12.8% | 8.5% | 2.3% | 0.0% | 0.0% | 6.6 | 1.59 | 10.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 73.0% (n=400)
- Stage 6: Inner 73.0% (n=237), Outer 82.8% (n=163)
- Stage 10: Corner 68.9% (n=61), Inner 81.2% (n=154), Outer 75.2% (n=105), Sideways 75.0% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
