# Evaluation: finetune_geom_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.3% | 4.0% | 3.8% | 0.0% | 0.0% | 0.0% | 5.2 | 0.44 | 2.1 |
| 6 | 400 | 90.3% | 5.5% | 4.3% | 0.0% | 0.0% | 0.0% | 5.0 | 0.56 | 2.9 |
| 10 | 400 | 89.5% | 7.5% | 3.0% | 0.0% | 0.0% | 0.0% | 6.6 | 0.77 | 4.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.3% (n=400)
- Stage 6: Inner 90.4% (n=219), Outer 90.1% (n=181)
- Stage 10: Corner 77.3% (n=66), Inner 93.2% (n=147), Outer 90.1% (n=101), Sideways 91.9% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
