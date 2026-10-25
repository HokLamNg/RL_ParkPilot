# Evaluation: r6pilot_route (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 73.3% | 16.3% | 10.0% | 0.5% | 0.0% | 0.0% | 9.5 | 3.60 | 12.2 |
| 6 | 400 | 71.5% | 18.0% | 10.0% | 0.5% | 0.0% | 0.0% | 9.6 | 3.69 | 12.2 |
| 10 | 400 | 69.5% | 20.0% | 9.5% | 1.0% | 0.0% | 0.0% | 9.7 | 4.27 | 12.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 73.3% (n=400)
- Stage 6: Inner 71.5% (n=400)
- Stage 10: Inner 69.5% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
