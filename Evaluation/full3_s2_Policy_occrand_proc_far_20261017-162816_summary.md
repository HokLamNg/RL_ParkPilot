# Evaluation: full3_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 76.3% | 9.8% | 13.3% | 0.8% | 0.0% | 0.0% | 9.3 | 3.08 | 10.3 |
| 6 | 400 | 74.3% | 11.0% | 14.0% | 0.8% | 0.0% | 0.0% | 9.5 | 3.39 | 9.6 |
| 10 | 400 | 73.0% | 11.5% | 13.8% | 1.8% | 0.0% | 0.0% | 9.7 | 3.77 | 11.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 76.3% (n=400)
- Stage 6: Inner 74.3% (n=400)
- Stage 10: Inner 73.0% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
