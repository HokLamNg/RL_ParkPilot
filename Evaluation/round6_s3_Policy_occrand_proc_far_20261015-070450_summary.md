# Evaluation: round6_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 82.5% | 10.5% | 7.0% | 0.0% | 0.0% | 0.0% | 9.5 | 2.44 | 7.8 |
| 6 | 400 | 81.0% | 12.5% | 6.5% | 0.0% | 0.0% | 0.0% | 9.7 | 2.78 | 6.6 |
| 10 | 400 | 78.8% | 13.0% | 8.3% | 0.0% | 0.0% | 0.0% | 9.9 | 2.86 | 7.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 82.5% (n=400)
- Stage 6: Inner 81.0% (n=400)
- Stage 10: Inner 78.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
