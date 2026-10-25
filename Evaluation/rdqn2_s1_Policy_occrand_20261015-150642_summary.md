# Evaluation: rdqn2_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 95.3% | 1.0% | 3.8% | 0.0% | 0.0% | 0.0% | 5.0 | 0.53 | 2.9 |
| 6 | 400 | 93.0% | 1.5% | 5.3% | 0.3% | 0.0% | 0.0% | 4.7 | 0.66 | 3.4 |
| 10 | 400 | 95.0% | 1.8% | 3.3% | 0.0% | 0.0% | 0.0% | 6.2 | 0.65 | 3.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 95.3% (n=400)
- Stage 6: Inner 92.4% (n=237), Outer 93.9% (n=163)
- Stage 10: Corner 88.5% (n=61), Inner 96.8% (n=154), Outer 94.3% (n=105), Sideways 97.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
