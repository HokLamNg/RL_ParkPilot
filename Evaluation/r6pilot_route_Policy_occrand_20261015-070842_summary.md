# Evaluation: r6pilot_route (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 90.0% | 6.3% | 3.5% | 0.3% | 0.0% | 0.0% | 5.1 | 0.67 | 2.8 |
| 6 | 400 | 90.3% | 6.0% | 3.8% | 0.0% | 0.0% | 0.0% | 5.0 | 0.65 | 3.3 |
| 10 | 400 | 93.0% | 5.5% | 1.5% | 0.0% | 0.0% | 0.0% | 6.3 | 0.69 | 3.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 90.0% (n=400)
- Stage 6: Inner 91.1% (n=237), Outer 89.0% (n=163)
- Stage 10: Corner 86.9% (n=61), Inner 96.8% (n=154), Outer 90.5% (n=105), Sideways 93.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
