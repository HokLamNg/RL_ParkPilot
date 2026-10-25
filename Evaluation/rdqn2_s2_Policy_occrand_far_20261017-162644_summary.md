# Evaluation: rdqn2_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 95.3% | 0.8% | 4.0% | 0.0% | 0.0% | 0.0% | 10.0 | 0.72 | 3.5 |
| 6 | 400 | 95.5% | 2.3% | 2.3% | 0.0% | 0.0% | 0.0% | 10.2 | 0.82 | 4.1 |
| 10 | 400 | 93.3% | 3.5% | 3.0% | 0.3% | 0.0% | 0.0% | 10.4 | 0.78 | 4.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 95.3% (n=400)
- Stage 6: Inner 95.2% (n=250), Outer 96.0% (n=150)
- Stage 10: Corner 98.5% (n=65), Inner 91.3% (n=160), Outer 96.8% (n=95), Sideways 88.8% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
