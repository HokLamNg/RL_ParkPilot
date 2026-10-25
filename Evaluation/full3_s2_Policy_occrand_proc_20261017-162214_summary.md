# Evaluation: full3_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 91.3% | 3.3% | 5.5% | 0.0% | 0.0% | 0.0% | 5.1 | 0.57 | 3.0 |
| 6 | 400 | 91.5% | 4.5% | 4.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.66 | 3.5 |
| 10 | 400 | 89.8% | 3.8% | 6.5% | 0.0% | 0.0% | 0.0% | 6.2 | 0.63 | 3.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 91.3% (n=400)
- Stage 6: Inner 91.5% (n=400)
- Stage 10: Inner 89.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
