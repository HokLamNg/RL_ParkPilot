# Evaluation: round5_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 89.5% | 8.5% | 2.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.75 | 3.3 |
| 6 | 400 | 89.8% | 6.5% | 3.8% | 0.0% | 0.0% | 0.0% | 4.9 | 0.74 | 3.7 |
| 10 | 400 | 90.8% | 7.5% | 1.5% | 0.3% | 0.0% | 0.0% | 6.1 | 0.90 | 4.9 |

**Success by bay group** (per stage):

- Stage 4: Inner 89.5% (n=400)
- Stage 6: Inner 89.0% (n=237), Outer 90.8% (n=163)
- Stage 10: Corner 82.0% (n=61), Inner 92.2% (n=154), Outer 91.4% (n=105), Sideways 93.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
