# Evaluation: full3_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.8% | 4.3% | 2.3% | 0.8% | 0.0% | 0.0% | 10.2 | 1.48 | 4.6 |
| 6 | 400 | 91.5% | 5.8% | 1.5% | 1.3% | 0.0% | 0.0% | 10.3 | 1.69 | 4.9 |
| 10 | 400 | 91.5% | 6.5% | 1.0% | 1.0% | 0.0% | 0.0% | 10.6 | 1.81 | 5.9 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.8% (n=400)
- Stage 6: Inner 91.6% (n=250), Outer 91.3% (n=150)
- Stage 10: Corner 90.8% (n=65), Inner 93.1% (n=160), Outer 91.6% (n=95), Sideways 88.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
