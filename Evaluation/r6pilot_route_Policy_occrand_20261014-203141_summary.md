# Evaluation: r6pilot_route (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.0% | 6.8% | 1.3% | 0.0% | 0.0% | 0.0% | 5.4 | 0.61 | 2.5 |
| 6 | 400 | 92.3% | 6.0% | 1.8% | 0.0% | 0.0% | 0.0% | 5.2 | 0.59 | 3.2 |
| 10 | 400 | 92.8% | 6.5% | 0.8% | 0.0% | 0.0% | 0.0% | 6.4 | 0.74 | 4.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.0% (n=400)
- Stage 6: Inner 92.4% (n=237), Outer 92.0% (n=163)
- Stage 10: Corner 85.2% (n=61), Inner 96.1% (n=154), Outer 90.5% (n=105), Sideways 95.0% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
