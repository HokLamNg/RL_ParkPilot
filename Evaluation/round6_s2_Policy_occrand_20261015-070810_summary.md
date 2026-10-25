# Evaluation: round6_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.8% | 3.5% | 3.0% | 0.8% | 0.0% | 0.0% | 5.2 | 0.55 | 2.3 |
| 6 | 400 | 93.5% | 3.3% | 2.8% | 0.5% | 0.0% | 0.0% | 5.1 | 0.56 | 2.8 |
| 10 | 400 | 96.0% | 3.3% | 0.3% | 0.5% | 0.0% | 0.0% | 6.4 | 0.44 | 2.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.8% (n=400)
- Stage 6: Inner 93.7% (n=237), Outer 93.3% (n=163)
- Stage 10: Corner 91.8% (n=61), Inner 98.1% (n=154), Outer 94.3% (n=105), Sideways 97.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
