# Evaluation: rdqn2_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 93.3% | 1.5% | 5.3% | 0.0% | 0.0% | 0.0% | 5.0 | 0.57 | 3.6 |
| 6 | 400 | 94.0% | 1.3% | 4.8% | 0.0% | 0.0% | 0.0% | 4.8 | 0.59 | 3.6 |
| 10 | 400 | 95.3% | 0.3% | 4.0% | 0.5% | 0.0% | 0.0% | 6.2 | 0.49 | 3.1 |

**Success by bay group** (per stage):

- Stage 4: Inner 93.3% (n=400)
- Stage 6: Inner 94.5% (n=237), Outer 93.3% (n=163)
- Stage 10: Corner 96.7% (n=61), Inner 93.5% (n=154), Outer 97.1% (n=105), Sideways 95.0% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
