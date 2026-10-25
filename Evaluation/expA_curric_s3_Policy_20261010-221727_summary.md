# Evaluation: expA_curric_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 90.3% | 5.8% | 3.8% | 0.3% | 0.0% | 0.0% | 5.2 | 0.58 | 3.0 |
| 6 | 400 | 87.8% | 8.5% | 3.8% | 0.0% | 0.0% | 0.0% | 5.1 | 0.73 | 4.8 |
| 10 | 400 | 89.5% | 8.8% | 1.8% | 0.0% | 0.0% | 0.0% | 6.9 | 0.89 | 5.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 90.3% (n=400)
- Stage 6: Inner 86.8% (n=219), Outer 89.0% (n=181)
- Stage 10: Corner 81.8% (n=66), Inner 90.5% (n=147), Outer 88.1% (n=101), Sideways 95.3% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
