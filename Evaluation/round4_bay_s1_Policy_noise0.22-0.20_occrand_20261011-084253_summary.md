# Evaluation: round4_bay_s1 (Policy, deterministic)

Action noise (sampled like training): std 0.22 steering, 0.20 throttle, redrawn each decision.

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 75.0% | 13.8% | 10.8% | 0.5% | 0.0% | 0.0% | 5.4 | 1.20 | 7.0 |
| 6 | 400 | 76.8% | 14.0% | 8.8% | 0.5% | 0.0% | 0.0% | 5.2 | 1.18 | 7.4 |
| 10 | 400 | 74.0% | 16.5% | 8.8% | 0.8% | 0.0% | 0.0% | 7.0 | 1.59 | 10.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 75.0% (n=400)
- Stage 6: Inner 75.5% (n=237), Outer 78.5% (n=163)
- Stage 10: Corner 65.6% (n=61), Inner 75.3% (n=154), Outer 76.2% (n=105), Sideways 75.0% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
