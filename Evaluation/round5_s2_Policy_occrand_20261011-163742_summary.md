# Evaluation: round5_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 89.8% | 7.0% | 3.0% | 0.3% | 0.0% | 0.0% | 5.0 | 0.77 | 3.2 |
| 6 | 400 | 92.3% | 5.0% | 2.8% | 0.0% | 0.0% | 0.0% | 4.9 | 0.59 | 3.5 |
| 10 | 400 | 92.0% | 6.3% | 1.5% | 0.3% | 0.0% | 0.0% | 6.2 | 0.79 | 3.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 89.8% (n=400)
- Stage 6: Inner 93.2% (n=237), Outer 90.8% (n=163)
- Stage 10: Corner 83.6% (n=61), Inner 94.2% (n=154), Outer 93.3% (n=105), Sideways 92.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
