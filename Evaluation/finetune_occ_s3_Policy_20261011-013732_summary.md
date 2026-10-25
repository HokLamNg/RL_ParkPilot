# Evaluation: finetune_occ_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 94.0% | 3.5% | 2.5% | 0.0% | 0.0% | 0.0% | 5.2 | 0.48 | 2.6 |
| 6 | 400 | 93.8% | 5.0% | 1.3% | 0.0% | 0.0% | 0.0% | 5.1 | 0.49 | 3.1 |
| 10 | 400 | 93.5% | 4.8% | 1.8% | 0.0% | 0.0% | 0.0% | 6.6 | 0.59 | 3.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 94.0% (n=400)
- Stage 6: Inner 93.2% (n=219), Outer 94.5% (n=181)
- Stage 10: Corner 84.8% (n=66), Inner 93.2% (n=147), Outer 98.0% (n=101), Sideways 95.3% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
