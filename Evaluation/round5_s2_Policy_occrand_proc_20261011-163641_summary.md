# Evaluation: round5_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 89.0% | 8.0% | 3.0% | 0.0% | 0.0% | 0.0% | 4.8 | 0.84 | 4.0 |
| 6 | 400 | 86.5% | 9.0% | 4.5% | 0.0% | 0.0% | 0.0% | 4.8 | 0.99 | 5.1 |
| 10 | 400 | 87.8% | 8.8% | 3.5% | 0.0% | 0.0% | 0.0% | 6.1 | 0.83 | 5.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 89.0% (n=400)
- Stage 6: Inner 86.5% (n=400)
- Stage 10: Inner 87.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
