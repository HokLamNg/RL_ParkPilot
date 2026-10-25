# Evaluation: finetune_occ_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 94.0% | 3.3% | 2.8% | 0.0% | 0.0% | 0.0% | 5.0 | 0.47 | 2.5 |
| 6 | 400 | 91.5% | 5.0% | 3.5% | 0.0% | 0.0% | 0.0% | 4.9 | 0.57 | 3.5 |
| 10 | 400 | 91.8% | 4.3% | 4.0% | 0.0% | 0.0% | 0.0% | 6.4 | 0.69 | 4.4 |

**Success by bay group** (per stage):

- Stage 4: Inner 94.0% (n=400)
- Stage 6: Inner 90.9% (n=219), Outer 92.3% (n=181)
- Stage 10: Corner 86.4% (n=66), Inner 92.5% (n=147), Outer 95.0% (n=101), Sideways 90.7% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
