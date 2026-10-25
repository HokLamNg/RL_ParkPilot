# Evaluation: r6pilot_straight (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 91.8% | 7.0% | 1.0% | 0.3% | 0.0% | 0.0% | 5.2 | 0.61 | 2.5 |
| 6 | 400 | 91.0% | 5.5% | 3.5% | 0.0% | 0.0% | 0.0% | 5.1 | 0.62 | 3.4 |
| 10 | 400 | 91.5% | 6.8% | 1.5% | 0.3% | 0.0% | 0.0% | 6.3 | 0.84 | 4.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 91.8% (n=400)
- Stage 6: Inner 92.4% (n=237), Outer 89.0% (n=163)
- Stage 10: Corner 85.2% (n=61), Inner 93.5% (n=154), Outer 90.5% (n=105), Sideways 93.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
