# Evaluation: round6_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 90.8% | 7.5% | 1.8% | 0.0% | 0.0% | 0.0% | 10.0 | 1.93 | 4.6 |
| 6 | 400 | 91.3% | 6.5% | 2.0% | 0.3% | 0.0% | 0.0% | 10.2 | 1.79 | 4.1 |
| 10 | 400 | 90.3% | 7.0% | 2.5% | 0.3% | 0.0% | 0.0% | 10.3 | 1.88 | 5.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 90.8% (n=400)
- Stage 6: Inner 90.4% (n=250), Outer 92.7% (n=150)
- Stage 10: Corner 86.2% (n=65), Inner 90.0% (n=160), Outer 93.7% (n=95), Sideways 90.0% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
