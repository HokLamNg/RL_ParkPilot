# Evaluation: r6pilot_route (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 88.0% | 7.3% | 4.8% | 0.0% | 0.0% | 0.0% | 5.0 | 0.83 | 3.3 |
| 6 | 400 | 87.0% | 6.8% | 6.3% | 0.0% | 0.0% | 0.0% | 5.0 | 0.73 | 4.0 |
| 10 | 400 | 86.3% | 8.3% | 5.5% | 0.0% | 0.0% | 0.0% | 6.2 | 0.83 | 4.4 |

**Success by bay group** (per stage):

- Stage 4: Inner 88.0% (n=400)
- Stage 6: Inner 87.0% (n=400)
- Stage 10: Inner 86.3% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
