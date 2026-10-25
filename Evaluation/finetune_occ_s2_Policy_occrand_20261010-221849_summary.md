# Evaluation: finetune_occ_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 87.8% | 9.8% | 2.5% | 0.0% | 0.0% | 0.0% | 5.2 | 0.83 | 4.1 |
| 6 | 400 | 85.3% | 11.0% | 3.8% | 0.0% | 0.0% | 0.0% | 5.0 | 0.88 | 5.4 |
| 10 | 400 | 84.8% | 11.5% | 3.5% | 0.3% | 0.0% | 0.0% | 6.2 | 1.36 | 7.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 87.8% (n=400)
- Stage 6: Inner 85.7% (n=237), Outer 84.7% (n=163)
- Stage 10: Corner 73.8% (n=61), Inner 89.0% (n=154), Outer 83.8% (n=105), Sideways 86.3% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
