# Evaluation: round5_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 73.8% | 13.5% | 12.8% | 0.0% | 0.0% | 0.0% | 9.9 | 3.68 | 12.4 |
| 6 | 400 | 77.5% | 14.8% | 7.8% | 0.0% | 0.0% | 0.0% | 10.1 | 3.61 | 11.3 |
| 10 | 400 | 73.8% | 18.5% | 7.8% | 0.0% | 0.0% | 0.0% | 10.3 | 4.06 | 13.1 |

**Success by bay group** (per stage):

- Stage 4: Inner 73.8% (n=400)
- Stage 6: Inner 74.4% (n=250), Outer 82.7% (n=150)
- Stage 10: Corner 64.6% (n=65), Inner 81.3% (n=160), Outer 76.8% (n=95), Sideways 62.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
