# Evaluation: r6pilot_route (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 96.5% | 1.0% | 2.5% | 0.0% | 0.0% | 0.0% | 5.1 | 0.30 | 1.5 |
| 6 | 400 | 96.5% | 2.5% | 1.0% | 0.0% | 0.0% | 0.0% | 5.1 | 0.31 | 1.8 |
| 10 | 400 | 97.0% | 1.8% | 1.0% | 0.3% | 0.0% | 0.0% | 6.5 | 0.31 | 2.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 96.5% (n=400)
- Stage 6: Inner 95.4% (n=219), Outer 97.8% (n=181)
- Stage 10: Corner 92.4% (n=66), Inner 97.3% (n=147), Outer 99.0% (n=101), Sideways 97.7% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
