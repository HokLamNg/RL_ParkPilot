# Evaluation: finetune_proc2_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 90.5% | 4.5% | 4.8% | 0.0% | 0.3% | 0.0% | 5.1 | 0.65 | 4.2 |
| 6 | 400 | 91.0% | 4.3% | 4.8% | 0.0% | 0.0% | 0.0% | 5.0 | 0.66 | 4.3 |
| 10 | 400 | 91.5% | 4.5% | 3.8% | 0.3% | 0.0% | 0.0% | 6.6 | 0.72 | 5.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 90.5% (n=400)
- Stage 6: Inner 90.0% (n=219), Outer 92.3% (n=181)
- Stage 10: Corner 87.9% (n=66), Inner 94.6% (n=147), Outer 88.1% (n=101), Sideways 93.0% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
