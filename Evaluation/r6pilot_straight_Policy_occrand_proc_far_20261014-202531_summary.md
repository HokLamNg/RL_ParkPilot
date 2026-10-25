# Evaluation: r6pilot_straight (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 70.3% | 17.5% | 12.0% | 0.3% | 0.0% | 0.0% | 9.2 | 3.78 | 12.5 |
| 6 | 400 | 71.8% | 18.0% | 9.8% | 0.5% | 0.0% | 0.0% | 9.3 | 3.86 | 11.5 |
| 10 | 400 | 69.8% | 18.8% | 11.3% | 0.3% | 0.0% | 0.0% | 9.6 | 4.15 | 12.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 70.3% (n=400)
- Stage 6: Inner 71.8% (n=400)
- Stage 10: Inner 69.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
