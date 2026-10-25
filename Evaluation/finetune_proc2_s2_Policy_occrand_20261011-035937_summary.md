# Evaluation: finetune_proc2_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 73.0% | 9.0% | 14.0% | 4.0% | 0.0% | 0.0% | 5.2 | 1.29 | 7.5 |
| 6 | 400 | 75.5% | 13.0% | 10.5% | 1.0% | 0.0% | 0.0% | 5.0 | 1.25 | 7.6 |
| 10 | 400 | 72.5% | 10.8% | 14.3% | 2.5% | 0.0% | 0.0% | 6.6 | 1.78 | 11.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 73.0% (n=400)
- Stage 6: Inner 73.4% (n=237), Outer 78.5% (n=163)
- Stage 10: Corner 68.9% (n=61), Inner 68.8% (n=154), Outer 79.0% (n=105), Sideways 73.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
