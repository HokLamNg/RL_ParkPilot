# Evaluation: round5_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 90.5% | 8.8% | 0.8% | 0.0% | 0.0% | 0.0% | 5.2 | 0.68 | 3.1 |
| 6 | 400 | 89.3% | 6.8% | 4.0% | 0.0% | 0.0% | 0.0% | 5.1 | 0.72 | 3.8 |
| 10 | 400 | 92.0% | 6.5% | 1.3% | 0.3% | 0.0% | 0.0% | 6.3 | 0.74 | 4.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 90.5% (n=400)
- Stage 6: Inner 90.7% (n=237), Outer 87.1% (n=163)
- Stage 10: Corner 85.2% (n=61), Inner 92.9% (n=154), Outer 93.3% (n=105), Sideways 93.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
