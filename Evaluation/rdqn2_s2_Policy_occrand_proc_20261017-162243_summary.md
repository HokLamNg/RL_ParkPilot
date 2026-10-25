# Evaluation: rdqn2_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 90.8% | 2.3% | 6.5% | 0.5% | 0.0% | 0.0% | 5.0 | 0.69 | 3.8 |
| 6 | 400 | 92.8% | 1.5% | 5.5% | 0.3% | 0.0% | 0.0% | 5.1 | 0.54 | 3.3 |
| 10 | 400 | 92.0% | 1.3% | 5.5% | 1.3% | 0.0% | 0.0% | 6.2 | 0.65 | 3.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 90.8% (n=400)
- Stage 6: Inner 92.8% (n=400)
- Stage 10: Inner 92.0% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
