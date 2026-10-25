# Evaluation: round4_bay_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 88.8% | 2.0% | 9.3% | 0.0% | 0.0% | 0.0% | 5.0 | 0.59 | 2.5 |
| 6 | 400 | 88.3% | 4.0% | 7.8% | 0.0% | 0.0% | 0.0% | 4.9 | 0.74 | 3.6 |
| 10 | 400 | 85.0% | 4.8% | 9.8% | 0.5% | 0.0% | 0.0% | 6.3 | 0.95 | 5.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 88.8% (n=400)
- Stage 6: Inner 84.5% (n=219), Outer 92.8% (n=181)
- Stage 10: Corner 83.3% (n=66), Inner 83.0% (n=147), Outer 88.1% (n=101), Sideways 86.0% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
