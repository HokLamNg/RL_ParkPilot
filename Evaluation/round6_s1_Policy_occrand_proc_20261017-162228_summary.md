# Evaluation: round6_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.3% | 3.8% | 4.0% | 0.0% | 0.0% | 0.0% | 5.1 | 0.62 | 2.5 |
| 6 | 400 | 92.0% | 4.0% | 4.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.69 | 3.6 |
| 10 | 400 | 93.0% | 5.3% | 1.8% | 0.0% | 0.0% | 0.0% | 6.3 | 0.68 | 3.1 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.3% (n=400)
- Stage 6: Inner 92.0% (n=400)
- Stage 10: Inner 93.0% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
