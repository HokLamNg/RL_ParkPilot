# Evaluation: round5_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 89.8% | 8.0% | 2.3% | 0.0% | 0.0% | 0.0% | 5.0 | 0.68 | 3.0 |
| 6 | 400 | 88.8% | 7.3% | 4.0% | 0.0% | 0.0% | 0.0% | 4.9 | 0.81 | 3.8 |
| 10 | 400 | 90.5% | 7.5% | 1.8% | 0.3% | 0.0% | 0.0% | 6.2 | 0.92 | 4.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 89.8% (n=400)
- Stage 6: Inner 89.0% (n=237), Outer 88.3% (n=163)
- Stage 10: Corner 85.2% (n=61), Inner 92.2% (n=154), Outer 90.5% (n=105), Sideways 91.3% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
