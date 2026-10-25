# Evaluation: round6_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 96.8% | 2.3% | 0.8% | 0.3% | 0.0% | 0.0% | 5.5 | 0.35 | 1.5 |
| 6 | 400 | 95.3% | 4.3% | 0.5% | 0.0% | 0.0% | 0.0% | 5.3 | 0.43 | 2.2 |
| 10 | 400 | 95.5% | 3.8% | 0.3% | 0.5% | 0.0% | 0.0% | 6.5 | 0.49 | 2.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 96.8% (n=400)
- Stage 6: Inner 96.2% (n=237), Outer 93.9% (n=163)
- Stage 10: Corner 90.2% (n=61), Inner 98.1% (n=154), Outer 94.3% (n=105), Sideways 96.3% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
