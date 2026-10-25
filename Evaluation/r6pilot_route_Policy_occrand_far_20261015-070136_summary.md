# Evaluation: r6pilot_route (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 82.3% | 9.8% | 8.0% | 0.0% | 0.0% | 0.0% | 9.9 | 2.98 | 8.4 |
| 6 | 400 | 85.3% | 9.0% | 5.3% | 0.5% | 0.0% | 0.0% | 10.2 | 2.58 | 6.5 |
| 10 | 400 | 82.8% | 10.0% | 6.3% | 1.0% | 0.0% | 0.0% | 10.2 | 3.01 | 7.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 82.3% (n=400)
- Stage 6: Inner 82.8% (n=250), Outer 89.3% (n=150)
- Stage 10: Corner 80.0% (n=65), Inner 86.9% (n=160), Outer 85.3% (n=95), Sideways 73.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
