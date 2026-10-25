# Evaluation: r6pilot_straight (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 83.0% | 10.3% | 6.5% | 0.3% | 0.0% | 0.0% | 10.0 | 2.83 | 8.0 |
| 6 | 400 | 86.8% | 9.3% | 4.0% | 0.0% | 0.0% | 0.0% | 10.2 | 2.46 | 7.0 |
| 10 | 400 | 82.3% | 12.5% | 5.3% | 0.0% | 0.0% | 0.0% | 10.3 | 3.18 | 8.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 83.0% (n=400)
- Stage 6: Inner 85.2% (n=250), Outer 89.3% (n=150)
- Stage 10: Corner 78.5% (n=65), Inner 84.4% (n=160), Outer 85.3% (n=95), Sideways 77.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
