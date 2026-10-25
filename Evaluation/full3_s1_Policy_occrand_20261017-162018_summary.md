# Evaluation: full3_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 98.0% | 0.8% | 1.3% | 0.0% | 0.0% | 0.0% | 5.1 | 0.28 | 2.0 |
| 6 | 400 | 96.8% | 2.0% | 1.3% | 0.0% | 0.0% | 0.0% | 5.0 | 0.32 | 2.2 |
| 10 | 400 | 95.5% | 3.5% | 1.0% | 0.0% | 0.0% | 0.0% | 6.4 | 0.46 | 2.8 |

**Success by bay group** (per stage):

- Stage 4: Inner 98.0% (n=400)
- Stage 6: Inner 98.3% (n=237), Outer 94.5% (n=163)
- Stage 10: Corner 86.9% (n=61), Inner 97.4% (n=154), Outer 96.2% (n=105), Sideways 97.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
