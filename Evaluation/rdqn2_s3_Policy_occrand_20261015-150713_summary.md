# Evaluation: rdqn2_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 93.3% | 1.5% | 4.5% | 0.8% | 0.0% | 0.0% | 4.9 | 0.64 | 3.3 |
| 6 | 400 | 92.3% | 0.8% | 7.0% | 0.0% | 0.0% | 0.0% | 4.8 | 0.59 | 2.9 |
| 10 | 400 | 96.5% | 1.0% | 2.5% | 0.0% | 0.0% | 0.0% | 6.2 | 0.53 | 2.9 |

**Success by bay group** (per stage):

- Stage 4: Inner 93.3% (n=400)
- Stage 6: Inner 92.0% (n=237), Outer 92.6% (n=163)
- Stage 10: Corner 96.7% (n=61), Inner 96.1% (n=154), Outer 96.2% (n=105), Sideways 97.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
