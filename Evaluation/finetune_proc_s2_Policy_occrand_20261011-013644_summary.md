# Evaluation: finetune_proc_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 57.0% | 13.5% | 28.5% | 1.0% | 0.0% | 0.0% | 5.4 | 1.50 | 8.3 |
| 6 | 400 | 66.3% | 9.8% | 22.3% | 1.8% | 0.0% | 0.0% | 5.2 | 1.41 | 8.0 |
| 10 | 400 | 59.0% | 11.8% | 24.8% | 4.3% | 0.3% | 0.0% | 6.9 | 2.00 | 12.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 57.0% (n=400)
- Stage 6: Inner 58.6% (n=237), Outer 77.3% (n=163)
- Stage 10: Corner 57.4% (n=61), Inner 56.5% (n=154), Outer 67.6% (n=105), Sideways 53.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
