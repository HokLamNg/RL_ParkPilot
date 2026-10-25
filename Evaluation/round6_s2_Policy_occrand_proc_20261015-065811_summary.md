# Evaluation: round6_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 91.5% | 4.0% | 4.3% | 0.3% | 0.0% | 0.0% | 5.0 | 0.70 | 2.8 |
| 6 | 400 | 90.8% | 5.0% | 3.8% | 0.5% | 0.0% | 0.0% | 5.1 | 0.63 | 3.1 |
| 10 | 400 | 92.8% | 5.3% | 2.0% | 0.0% | 0.0% | 0.0% | 6.2 | 0.55 | 2.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 91.5% (n=400)
- Stage 6: Inner 90.8% (n=400)
- Stage 10: Inner 92.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
