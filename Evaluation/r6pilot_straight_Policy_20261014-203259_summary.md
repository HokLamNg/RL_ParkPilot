# Evaluation: r6pilot_straight (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 95.8% | 1.5% | 2.8% | 0.0% | 0.0% | 0.0% | 5.0 | 0.32 | 1.3 |
| 6 | 400 | 94.3% | 2.5% | 3.3% | 0.0% | 0.0% | 0.0% | 4.8 | 0.41 | 2.6 |
| 10 | 400 | 96.5% | 2.0% | 1.5% | 0.0% | 0.0% | 0.0% | 6.3 | 0.38 | 2.1 |

**Success by bay group** (per stage):

- Stage 4: Inner 95.8% (n=400)
- Stage 6: Inner 93.2% (n=219), Outer 95.6% (n=181)
- Stage 10: Corner 92.4% (n=66), Inner 98.0% (n=147), Outer 98.0% (n=101), Sideways 95.3% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
