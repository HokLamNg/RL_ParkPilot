# Evaluation: round4_bay_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 68.5% | 12.0% | 19.0% | 0.5% | 0.0% | 0.0% | 4.9 | 1.33 | 5.7 |
| 6 | 400 | 73.0% | 12.8% | 14.0% | 0.3% | 0.0% | 0.0% | 4.8 | 1.30 | 7.5 |
| 10 | 400 | 71.5% | 14.0% | 14.3% | 0.3% | 0.0% | 0.0% | 6.5 | 1.54 | 9.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 68.5% (n=400)
- Stage 6: Inner 68.4% (n=237), Outer 79.8% (n=163)
- Stage 10: Corner 73.8% (n=61), Inner 70.1% (n=154), Outer 76.2% (n=105), Sideways 66.3% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
