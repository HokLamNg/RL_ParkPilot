# Evaluation: round6_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.0% | 6.8% | 1.3% | 0.0% | 0.0% | 0.0% | 10.0 | 1.74 | 4.0 |
| 6 | 400 | 92.3% | 6.5% | 1.3% | 0.0% | 0.0% | 0.0% | 10.2 | 1.66 | 4.0 |
| 10 | 400 | 90.5% | 7.0% | 2.5% | 0.0% | 0.0% | 0.0% | 10.3 | 1.81 | 4.9 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.0% (n=400)
- Stage 6: Inner 92.8% (n=250), Outer 91.3% (n=150)
- Stage 10: Corner 89.2% (n=65), Inner 90.0% (n=160), Outer 93.7% (n=95), Sideways 88.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
