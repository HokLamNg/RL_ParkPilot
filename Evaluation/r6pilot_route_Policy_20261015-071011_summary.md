# Evaluation: r6pilot_route (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 95.0% | 1.5% | 3.5% | 0.0% | 0.0% | 0.0% | 5.0 | 0.38 | 1.7 |
| 6 | 400 | 94.0% | 2.5% | 3.5% | 0.0% | 0.0% | 0.0% | 4.9 | 0.43 | 2.4 |
| 10 | 400 | 96.8% | 0.8% | 2.5% | 0.0% | 0.0% | 0.0% | 6.3 | 0.34 | 1.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 95.0% (n=400)
- Stage 6: Inner 93.2% (n=219), Outer 95.0% (n=181)
- Stage 10: Corner 93.9% (n=66), Inner 98.0% (n=147), Outer 96.0% (n=101), Sideways 97.7% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
