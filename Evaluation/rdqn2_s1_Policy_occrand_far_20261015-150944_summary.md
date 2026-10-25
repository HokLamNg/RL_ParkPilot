# Evaluation: rdqn2_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 94.0% | 2.3% | 3.0% | 0.8% | 0.0% | 0.0% | 10.2 | 1.11 | 3.9 |
| 6 | 400 | 96.8% | 1.0% | 2.3% | 0.0% | 0.0% | 0.0% | 10.3 | 0.90 | 3.0 |
| 10 | 400 | 95.8% | 2.0% | 2.0% | 0.3% | 0.0% | 0.0% | 10.5 | 0.96 | 3.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 94.0% (n=400)
- Stage 6: Inner 96.8% (n=250), Outer 96.7% (n=150)
- Stage 10: Corner 96.9% (n=65), Inner 96.3% (n=160), Outer 97.9% (n=95), Sideways 91.3% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
