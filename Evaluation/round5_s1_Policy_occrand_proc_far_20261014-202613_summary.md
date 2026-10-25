# Evaluation: round5_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 66.3% | 17.8% | 15.8% | 0.3% | 0.0% | 0.0% | 9.0 | 4.29 | 14.8 |
| 6 | 400 | 63.3% | 20.3% | 16.3% | 0.3% | 0.0% | 0.0% | 9.0 | 4.51 | 15.5 |
| 10 | 400 | 63.3% | 20.3% | 15.5% | 1.0% | 0.0% | 0.0% | 9.5 | 4.63 | 15.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 66.3% (n=400)
- Stage 6: Inner 63.3% (n=400)
- Stage 10: Inner 63.3% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
