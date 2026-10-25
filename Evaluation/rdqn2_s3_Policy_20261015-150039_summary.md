# Evaluation: rdqn2_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 96.3% | 0.5% | 3.3% | 0.0% | 0.0% | 0.0% | 4.8 | 0.41 | 2.0 |
| 6 | 400 | 97.5% | 0.5% | 2.0% | 0.0% | 0.0% | 0.0% | 4.8 | 0.40 | 2.0 |
| 10 | 400 | 97.5% | 0.3% | 2.3% | 0.0% | 0.0% | 0.0% | 6.2 | 0.34 | 2.1 |

**Success by bay group** (per stage):

- Stage 4: Inner 96.3% (n=400)
- Stage 6: Inner 96.8% (n=219), Outer 98.3% (n=181)
- Stage 10: Corner 100.0% (n=66), Inner 94.6% (n=147), Outer 98.0% (n=101), Sideways 100.0% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
