# Evaluation: round6_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 93.5% | 3.3% | 2.5% | 0.8% | 0.0% | 0.0% | 5.2 | 0.56 | 2.3 |
| 6 | 400 | 93.8% | 3.3% | 3.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.50 | 2.6 |
| 10 | 400 | 96.3% | 3.3% | 0.3% | 0.3% | 0.0% | 0.0% | 6.3 | 0.45 | 2.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 93.5% (n=400)
- Stage 6: Inner 94.1% (n=237), Outer 93.3% (n=163)
- Stage 10: Corner 91.8% (n=61), Inner 98.1% (n=154), Outer 95.2% (n=105), Sideways 97.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
