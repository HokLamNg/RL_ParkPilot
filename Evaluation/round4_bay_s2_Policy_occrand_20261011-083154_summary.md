# Evaluation: round4_bay_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 78.5% | 12.3% | 8.8% | 0.5% | 0.0% | 0.0% | 5.2 | 1.19 | 6.3 |
| 6 | 400 | 81.0% | 10.3% | 8.5% | 0.3% | 0.0% | 0.0% | 5.1 | 1.02 | 6.3 |
| 10 | 400 | 75.0% | 11.5% | 12.5% | 1.0% | 0.0% | 0.0% | 6.4 | 1.59 | 9.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 78.5% (n=400)
- Stage 6: Inner 81.0% (n=237), Outer 81.0% (n=163)
- Stage 10: Corner 72.1% (n=61), Inner 79.2% (n=154), Outer 73.3% (n=105), Sideways 71.3% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
