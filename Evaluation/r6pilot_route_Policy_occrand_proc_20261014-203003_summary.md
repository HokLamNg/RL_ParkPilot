# Evaluation: r6pilot_route (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 91.3% | 6.3% | 2.3% | 0.3% | 0.0% | 0.0% | 5.2 | 0.69 | 3.1 |
| 6 | 400 | 89.8% | 7.5% | 2.8% | 0.0% | 0.0% | 0.0% | 5.3 | 0.63 | 3.5 |
| 10 | 400 | 89.8% | 7.5% | 2.8% | 0.0% | 0.0% | 0.0% | 6.4 | 0.71 | 4.1 |

**Success by bay group** (per stage):

- Stage 4: Inner 91.3% (n=400)
- Stage 6: Inner 89.8% (n=400)
- Stage 10: Inner 89.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
