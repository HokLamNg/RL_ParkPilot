# Evaluation: round5_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 94.3% | 2.5% | 3.0% | 0.3% | 0.0% | 0.0% | 4.8 | 0.45 | 2.2 |
| 6 | 400 | 93.3% | 3.5% | 3.3% | 0.0% | 0.0% | 0.0% | 4.7 | 0.49 | 2.5 |
| 10 | 400 | 95.8% | 2.0% | 2.3% | 0.0% | 0.0% | 0.0% | 6.2 | 0.49 | 2.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 94.3% (n=400)
- Stage 6: Inner 93.6% (n=219), Outer 92.8% (n=181)
- Stage 10: Corner 92.4% (n=66), Inner 94.6% (n=147), Outer 99.0% (n=101), Sideways 96.5% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
