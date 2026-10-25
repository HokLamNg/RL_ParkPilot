# Evaluation: r6pilot_route (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 82.3% | 10.8% | 6.5% | 0.5% | 0.0% | 0.0% | 10.0 | 3.05 | 9.1 |
| 6 | 400 | 85.3% | 10.0% | 4.5% | 0.3% | 0.0% | 0.0% | 10.3 | 2.61 | 6.9 |
| 10 | 400 | 82.5% | 11.8% | 4.3% | 1.5% | 0.0% | 0.0% | 10.4 | 3.06 | 8.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 82.3% (n=400)
- Stage 6: Inner 83.2% (n=250), Outer 88.7% (n=150)
- Stage 10: Corner 73.8% (n=65), Inner 86.3% (n=160), Outer 87.4% (n=95), Sideways 76.3% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
