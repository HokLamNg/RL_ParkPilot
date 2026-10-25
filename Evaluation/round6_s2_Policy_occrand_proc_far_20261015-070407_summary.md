# Evaluation: round6_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 79.0% | 10.8% | 10.0% | 0.3% | 0.0% | 0.0% | 9.5 | 2.78 | 8.5 |
| 6 | 400 | 81.5% | 12.8% | 5.8% | 0.0% | 0.0% | 0.0% | 9.6 | 2.79 | 7.0 |
| 10 | 400 | 77.5% | 13.0% | 8.8% | 0.8% | 0.0% | 0.0% | 9.8 | 3.13 | 8.4 |

**Success by bay group** (per stage):

- Stage 4: Inner 79.0% (n=400)
- Stage 6: Inner 81.5% (n=400)
- Stage 10: Inner 77.5% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
