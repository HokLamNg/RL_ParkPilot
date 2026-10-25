# Evaluation: round5_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 95.3% | 2.8% | 2.0% | 0.0% | 0.0% | 0.0% | 4.8 | 0.38 | 1.6 |
| 6 | 400 | 94.5% | 3.3% | 2.3% | 0.0% | 0.0% | 0.0% | 4.8 | 0.45 | 2.1 |
| 10 | 400 | 93.0% | 3.5% | 3.5% | 0.0% | 0.0% | 0.0% | 6.2 | 0.55 | 3.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 95.3% (n=400)
- Stage 6: Inner 94.1% (n=219), Outer 95.0% (n=181)
- Stage 10: Corner 89.4% (n=66), Inner 92.5% (n=147), Outer 93.1% (n=101), Sideways 96.5% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
