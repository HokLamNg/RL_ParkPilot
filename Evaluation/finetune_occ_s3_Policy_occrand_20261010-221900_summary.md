# Evaluation: finetune_occ_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 86.3% | 9.0% | 4.8% | 0.0% | 0.0% | 0.0% | 5.2 | 0.86 | 3.6 |
| 6 | 400 | 87.8% | 7.8% | 4.5% | 0.0% | 0.0% | 0.0% | 5.1 | 0.79 | 4.5 |
| 10 | 400 | 86.5% | 9.5% | 3.8% | 0.3% | 0.0% | 0.0% | 6.3 | 1.06 | 5.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 86.3% (n=400)
- Stage 6: Inner 88.6% (n=237), Outer 86.5% (n=163)
- Stage 10: Corner 78.7% (n=61), Inner 89.0% (n=154), Outer 88.6% (n=105), Sideways 85.0% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
