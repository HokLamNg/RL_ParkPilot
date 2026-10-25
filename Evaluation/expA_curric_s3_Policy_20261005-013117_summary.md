# Evaluation: expA_curric_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 90.3% | 5.5% | 4.0% | 0.3% | 0.0% | 0.0% | 5.1 | 0.62 | 2.9 |
| 6 | 400 | 87.5% | 7.8% | 4.8% | 0.0% | 0.0% | 0.0% | 5.0 | 0.74 | 4.3 |
| 10 | 400 | 87.8% | 9.3% | 3.0% | 0.0% | 0.0% | 0.0% | 6.8 | 0.92 | 5.4 |

**Success by bay group** (per stage):

- Stage 4: Inner 90.3% (n=400)
- Stage 6: Inner 88.6% (n=219), Outer 86.2% (n=181)
- Stage 10: Corner 78.8% (n=66), Inner 89.8% (n=147), Outer 87.1% (n=101), Sideways 91.9% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
