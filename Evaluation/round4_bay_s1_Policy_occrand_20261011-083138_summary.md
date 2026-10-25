# Evaluation: round4_bay_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 79.3% | 10.3% | 10.5% | 0.0% | 0.0% | 0.0% | 5.1 | 1.02 | 5.3 |
| 6 | 400 | 79.8% | 11.3% | 9.0% | 0.0% | 0.0% | 0.0% | 5.0 | 1.04 | 6.4 |
| 10 | 400 | 78.3% | 13.5% | 8.0% | 0.3% | 0.0% | 0.0% | 6.4 | 1.46 | 9.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 79.3% (n=400)
- Stage 6: Inner 77.6% (n=237), Outer 82.8% (n=163)
- Stage 10: Corner 73.8% (n=61), Inner 79.2% (n=154), Outer 79.0% (n=105), Sideways 78.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
