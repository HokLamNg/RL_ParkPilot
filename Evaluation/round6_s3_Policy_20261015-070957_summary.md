# Evaluation: round6_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 98.5% | 0.0% | 1.5% | 0.0% | 0.0% | 0.0% | 5.1 | 0.24 | 1.3 |
| 6 | 400 | 97.3% | 1.5% | 1.3% | 0.0% | 0.0% | 0.0% | 5.0 | 0.30 | 1.4 |
| 10 | 400 | 97.8% | 1.5% | 0.8% | 0.0% | 0.0% | 0.0% | 6.4 | 0.31 | 1.8 |

**Success by bay group** (per stage):

- Stage 4: Inner 98.5% (n=400)
- Stage 6: Inner 97.7% (n=219), Outer 96.7% (n=181)
- Stage 10: Corner 95.5% (n=66), Inner 98.6% (n=147), Outer 99.0% (n=101), Sideways 96.5% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
