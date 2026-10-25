# Evaluation: rdqn2_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 93.0% | 1.5% | 5.0% | 0.5% | 0.0% | 0.0% | 5.0 | 0.56 | 2.9 |
| 6 | 400 | 89.3% | 3.0% | 7.3% | 0.5% | 0.0% | 0.0% | 5.0 | 0.76 | 4.0 |
| 10 | 400 | 89.8% | 2.0% | 7.5% | 0.5% | 0.3% | 0.0% | 6.3 | 0.68 | 3.8 |

**Success by bay group** (per stage):

- Stage 4: Inner 93.0% (n=400)
- Stage 6: Inner 89.3% (n=400)
- Stage 10: Inner 89.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
