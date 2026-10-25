# Evaluation: finetune_occ_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 83.8% | 10.5% | 5.8% | 0.0% | 0.0% | 0.0% | 5.1 | 1.00 | 4.6 |
| 6 | 400 | 86.3% | 10.3% | 3.5% | 0.0% | 0.0% | 0.0% | 4.9 | 0.89 | 4.7 |
| 10 | 400 | 84.0% | 11.3% | 4.8% | 0.0% | 0.0% | 0.0% | 6.2 | 1.36 | 6.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 83.8% (n=400)
- Stage 6: Inner 84.8% (n=237), Outer 88.3% (n=163)
- Stage 10: Corner 75.4% (n=61), Inner 87.0% (n=154), Outer 82.9% (n=105), Sideways 86.3% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
