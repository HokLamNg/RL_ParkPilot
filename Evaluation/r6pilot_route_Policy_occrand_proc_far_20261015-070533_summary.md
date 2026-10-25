# Evaluation: r6pilot_route (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 72.8% | 15.5% | 11.5% | 0.3% | 0.0% | 0.0% | 9.2 | 3.63 | 11.8 |
| 6 | 400 | 70.5% | 17.5% | 11.5% | 0.5% | 0.0% | 0.0% | 9.3 | 3.78 | 11.4 |
| 10 | 400 | 67.8% | 18.0% | 14.0% | 0.3% | 0.0% | 0.0% | 9.6 | 4.11 | 11.8 |

**Success by bay group** (per stage):

- Stage 4: Inner 72.8% (n=400)
- Stage 6: Inner 70.5% (n=400)
- Stage 10: Inner 67.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
