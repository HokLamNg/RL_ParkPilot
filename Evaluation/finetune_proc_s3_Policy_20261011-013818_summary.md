# Evaluation: finetune_proc_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 87.0% | 4.8% | 8.0% | 0.3% | 0.0% | 0.0% | 5.1 | 0.75 | 3.6 |
| 6 | 400 | 87.5% | 4.0% | 8.0% | 0.5% | 0.0% | 0.0% | 5.0 | 0.66 | 4.0 |
| 10 | 400 | 87.0% | 5.8% | 7.3% | 0.0% | 0.0% | 0.0% | 6.6 | 0.90 | 5.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 87.0% (n=400)
- Stage 6: Inner 84.9% (n=219), Outer 90.6% (n=181)
- Stage 10: Corner 74.2% (n=66), Inner 90.5% (n=147), Outer 91.1% (n=101), Sideways 86.0% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
