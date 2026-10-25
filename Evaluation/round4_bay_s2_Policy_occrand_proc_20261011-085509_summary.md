# Evaluation: round4_bay_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 68.5% | 18.0% | 12.0% | 1.5% | 0.0% | 0.0% | 5.0 | 1.77 | 9.3 |
| 6 | 400 | 65.8% | 21.8% | 11.8% | 0.8% | 0.0% | 0.0% | 5.1 | 1.85 | 10.0 |
| 10 | 400 | 59.8% | 25.5% | 13.0% | 1.8% | 0.0% | 0.0% | 6.4 | 2.14 | 11.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 68.5% (n=400)
- Stage 6: Inner 65.8% (n=400)
- Stage 10: Inner 59.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
