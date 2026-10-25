# Evaluation: full3_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 95.0% | 2.0% | 3.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.39 | 2.6 |
| 6 | 400 | 95.0% | 2.3% | 2.8% | 0.0% | 0.0% | 0.0% | 4.8 | 0.44 | 2.4 |
| 10 | 400 | 94.0% | 3.5% | 2.3% | 0.3% | 0.0% | 0.0% | 6.2 | 0.59 | 3.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 95.0% (n=400)
- Stage 6: Inner 94.9% (n=237), Outer 95.1% (n=163)
- Stage 10: Corner 82.0% (n=61), Inner 96.1% (n=154), Outer 95.2% (n=105), Sideways 97.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
