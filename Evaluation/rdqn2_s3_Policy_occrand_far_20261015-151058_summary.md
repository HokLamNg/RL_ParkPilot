# Evaluation: rdqn2_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 94.8% | 0.5% | 4.5% | 0.3% | 0.0% | 0.0% | 10.1 | 1.15 | 3.7 |
| 6 | 400 | 95.0% | 2.5% | 2.3% | 0.3% | 0.0% | 0.0% | 10.2 | 0.87 | 3.4 |
| 10 | 400 | 95.3% | 1.3% | 2.5% | 1.0% | 0.0% | 0.0% | 10.4 | 0.81 | 2.9 |

**Success by bay group** (per stage):

- Stage 4: Inner 94.8% (n=400)
- Stage 6: Inner 93.6% (n=250), Outer 97.3% (n=150)
- Stage 10: Corner 96.9% (n=65), Inner 94.4% (n=160), Outer 96.8% (n=95), Sideways 93.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
