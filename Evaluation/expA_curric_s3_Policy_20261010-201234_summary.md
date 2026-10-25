# Evaluation: expA_curric_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 91.8% | 5.8% | 2.5% | 0.0% | 0.0% | 0.0% | 5.2 | 0.53 | 2.7 |
| 6 | 400 | 88.5% | 8.3% | 3.3% | 0.0% | 0.0% | 0.0% | 5.1 | 0.73 | 4.9 |
| 10 | 400 | 88.8% | 9.0% | 2.3% | 0.0% | 0.0% | 0.0% | 6.9 | 0.91 | 5.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 91.8% (n=400)
- Stage 6: Inner 88.6% (n=219), Outer 88.4% (n=181)
- Stage 10: Corner 81.8% (n=66), Inner 90.5% (n=147), Outer 88.1% (n=101), Sideways 91.9% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
