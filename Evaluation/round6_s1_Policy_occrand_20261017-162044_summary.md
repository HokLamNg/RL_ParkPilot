# Evaluation: round6_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 93.8% | 2.8% | 3.0% | 0.5% | 0.0% | 0.0% | 5.2 | 0.53 | 2.3 |
| 6 | 400 | 94.0% | 4.5% | 1.5% | 0.0% | 0.0% | 0.0% | 5.1 | 0.53 | 2.4 |
| 10 | 400 | 95.8% | 3.3% | 0.8% | 0.3% | 0.0% | 0.0% | 6.3 | 0.50 | 2.4 |

**Success by bay group** (per stage):

- Stage 4: Inner 93.8% (n=400)
- Stage 6: Inner 94.9% (n=237), Outer 92.6% (n=163)
- Stage 10: Corner 91.8% (n=61), Inner 97.4% (n=154), Outer 94.3% (n=105), Sideways 97.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
