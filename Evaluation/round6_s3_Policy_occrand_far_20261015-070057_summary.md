# Evaluation: round6_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.8% | 6.3% | 1.0% | 0.0% | 0.0% | 0.0% | 10.0 | 1.58 | 3.7 |
| 6 | 400 | 93.0% | 5.3% | 1.8% | 0.0% | 0.0% | 0.0% | 10.2 | 1.47 | 4.1 |
| 10 | 400 | 91.5% | 6.0% | 2.5% | 0.0% | 0.0% | 0.0% | 10.4 | 1.58 | 4.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.8% (n=400)
- Stage 6: Inner 92.8% (n=250), Outer 93.3% (n=150)
- Stage 10: Corner 89.2% (n=65), Inner 91.9% (n=160), Outer 92.6% (n=95), Sideways 91.3% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
