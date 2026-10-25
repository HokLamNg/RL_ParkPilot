# Evaluation: expA_curric_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 74.0% | 17.5% | 7.3% | 1.3% | 0.0% | 0.0% | 5.5 | 1.57 | 8.0 |
| 6 | 400 | 74.8% | 18.0% | 6.3% | 1.0% | 0.0% | 0.0% | 5.1 | 1.41 | 8.7 |
| 10 | 400 | 73.3% | 19.5% | 6.0% | 1.3% | 0.0% | 0.0% | 6.9 | 2.02 | 11.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 74.0% (n=400)
- Stage 6: Inner 75.9% (n=237), Outer 73.0% (n=163)
- Stage 10: Corner 52.5% (n=61), Inner 81.2% (n=154), Outer 70.5% (n=105), Sideways 77.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
