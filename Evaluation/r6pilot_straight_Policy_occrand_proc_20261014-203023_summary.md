# Evaluation: r6pilot_straight (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 87.8% | 6.8% | 5.5% | 0.0% | 0.0% | 0.0% | 5.0 | 0.85 | 3.6 |
| 6 | 400 | 89.3% | 7.3% | 3.3% | 0.3% | 0.0% | 0.0% | 5.0 | 0.80 | 4.2 |
| 10 | 400 | 87.0% | 9.0% | 4.0% | 0.0% | 0.0% | 0.0% | 6.2 | 0.94 | 4.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 87.8% (n=400)
- Stage 6: Inner 89.3% (n=400)
- Stage 10: Inner 87.0% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
