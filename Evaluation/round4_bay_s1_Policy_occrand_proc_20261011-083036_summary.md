# Evaluation: round4_bay_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 70.8% | 19.0% | 8.8% | 1.5% | 0.0% | 0.0% | 5.1 | 1.52 | 8.5 |
| 6 | 400 | 71.8% | 17.5% | 9.8% | 1.0% | 0.0% | 0.0% | 5.0 | 1.51 | 8.0 |
| 10 | 400 | 67.5% | 21.8% | 9.8% | 1.0% | 0.0% | 0.0% | 6.6 | 1.77 | 10.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 70.8% (n=400)
- Stage 6: Inner 71.8% (n=400)
- Stage 10: Inner 67.5% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
