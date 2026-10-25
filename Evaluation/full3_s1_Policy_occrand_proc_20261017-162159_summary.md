# Evaluation: full3_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 96.8% | 2.5% | 0.8% | 0.0% | 0.0% | 0.0% | 5.2 | 0.41 | 2.0 |
| 6 | 400 | 93.5% | 5.0% | 1.5% | 0.0% | 0.0% | 0.0% | 5.2 | 0.55 | 3.0 |
| 10 | 400 | 94.0% | 3.8% | 2.3% | 0.0% | 0.0% | 0.0% | 6.5 | 0.54 | 3.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 96.8% (n=400)
- Stage 6: Inner 93.5% (n=400)
- Stage 10: Inner 94.0% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
