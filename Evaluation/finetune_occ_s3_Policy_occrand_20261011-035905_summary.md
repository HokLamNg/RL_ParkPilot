# Evaluation: finetune_occ_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 87.8% | 8.5% | 3.5% | 0.3% | 0.0% | 0.0% | 5.4 | 0.80 | 3.8 |
| 6 | 400 | 86.0% | 9.8% | 4.3% | 0.0% | 0.0% | 0.0% | 5.3 | 0.83 | 4.7 |
| 10 | 400 | 86.3% | 10.3% | 3.5% | 0.0% | 0.0% | 0.0% | 6.4 | 1.08 | 6.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 87.8% (n=400)
- Stage 6: Inner 85.7% (n=237), Outer 86.5% (n=163)
- Stage 10: Corner 78.7% (n=61), Inner 87.7% (n=154), Outer 88.6% (n=105), Sideways 86.3% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
