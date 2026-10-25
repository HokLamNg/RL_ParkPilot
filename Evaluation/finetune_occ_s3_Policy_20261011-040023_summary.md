# Evaluation: finetune_occ_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 94.3% | 3.3% | 2.5% | 0.0% | 0.0% | 0.0% | 5.3 | 0.43 | 2.5 |
| 6 | 400 | 94.0% | 4.8% | 1.3% | 0.0% | 0.0% | 0.0% | 5.2 | 0.48 | 3.2 |
| 10 | 400 | 94.3% | 4.8% | 1.0% | 0.0% | 0.0% | 0.0% | 6.7 | 0.55 | 3.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 94.3% (n=400)
- Stage 6: Inner 93.2% (n=219), Outer 95.0% (n=181)
- Stage 10: Corner 87.9% (n=66), Inner 93.2% (n=147), Outer 98.0% (n=101), Sideways 96.5% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
