# Evaluation: full3_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 82.5% | 12.0% | 5.0% | 0.5% | 0.0% | 0.0% | 9.8 | 2.41 | 8.6 |
| 6 | 400 | 82.3% | 9.3% | 7.0% | 1.5% | 0.0% | 0.0% | 9.8 | 2.41 | 7.0 |
| 10 | 400 | 79.5% | 11.8% | 7.3% | 1.5% | 0.0% | 0.0% | 9.9 | 2.96 | 8.8 |

**Success by bay group** (per stage):

- Stage 4: Inner 82.5% (n=400)
- Stage 6: Inner 82.3% (n=400)
- Stage 10: Inner 79.5% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
