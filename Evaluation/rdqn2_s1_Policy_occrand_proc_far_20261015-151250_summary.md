# Evaluation: rdqn2_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 85.3% | 5.5% | 6.0% | 3.3% | 0.0% | 0.0% | 9.8 | 1.87 | 7.1 |
| 6 | 400 | 84.0% | 4.5% | 8.5% | 3.0% | 0.0% | 0.0% | 10.0 | 1.96 | 6.4 |
| 10 | 400 | 81.3% | 4.5% | 10.3% | 4.0% | 0.0% | 0.0% | 10.1 | 2.43 | 7.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 85.3% (n=400)
- Stage 6: Inner 84.0% (n=400)
- Stage 10: Inner 81.3% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
