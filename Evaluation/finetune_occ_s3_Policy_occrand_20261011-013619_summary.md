# Evaluation: finetune_occ_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 88.3% | 8.8% | 2.8% | 0.3% | 0.0% | 0.0% | 5.4 | 0.77 | 3.4 |
| 6 | 400 | 85.8% | 9.8% | 4.5% | 0.0% | 0.0% | 0.0% | 5.3 | 0.81 | 4.9 |
| 10 | 400 | 88.3% | 10.3% | 1.5% | 0.0% | 0.0% | 0.0% | 6.4 | 0.95 | 5.8 |

**Success by bay group** (per stage):

- Stage 4: Inner 88.3% (n=400)
- Stage 6: Inner 85.2% (n=237), Outer 86.5% (n=163)
- Stage 10: Corner 80.3% (n=61), Inner 90.3% (n=154), Outer 89.5% (n=105), Sideways 88.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
