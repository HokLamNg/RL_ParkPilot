# Evaluation: round6_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 94.8% | 4.3% | 1.0% | 0.0% | 0.0% | 0.0% | 5.4 | 0.49 | 2.0 |
| 6 | 400 | 94.3% | 4.8% | 1.0% | 0.0% | 0.0% | 0.0% | 5.3 | 0.53 | 2.8 |
| 10 | 400 | 93.8% | 4.3% | 1.8% | 0.3% | 0.0% | 0.0% | 6.5 | 0.57 | 3.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 94.8% (n=400)
- Stage 6: Inner 94.3% (n=400)
- Stage 10: Inner 93.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
