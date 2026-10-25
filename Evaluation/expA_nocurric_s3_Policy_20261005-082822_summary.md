# Evaluation: expA_nocurric_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 83.3% | 4.8% | 9.5% | 2.5% | 0.0% | 0.0% | 9.2 | 0.90 | 6.6 |
| 6 | 400 | 79.3% | 8.3% | 10.8% | 1.8% | 0.0% | 0.0% | 9.3 | 1.27 | 8.3 |
| 10 | 400 | 83.0% | 9.3% | 6.3% | 1.5% | 0.0% | 0.0% | 7.4 | 1.23 | 7.1 |

**Success by bay group** (per stage):

- Stage 4: Inner 83.3% (n=400)
- Stage 6: Inner 74.0% (n=219), Outer 85.6% (n=181)
- Stage 10: Corner 78.8% (n=66), Inner 81.0% (n=147), Outer 90.1% (n=101), Sideways 81.4% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
