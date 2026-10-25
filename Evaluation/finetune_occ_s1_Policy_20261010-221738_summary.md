# Evaluation: finetune_occ_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 95.3% | 1.8% | 3.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.37 | 1.8 |
| 6 | 400 | 91.8% | 4.8% | 3.5% | 0.0% | 0.0% | 0.0% | 4.9 | 0.53 | 2.9 |
| 10 | 400 | 92.3% | 3.8% | 4.0% | 0.0% | 0.0% | 0.0% | 6.4 | 0.62 | 4.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 95.3% (n=400)
- Stage 6: Inner 90.9% (n=219), Outer 92.8% (n=181)
- Stage 10: Corner 90.9% (n=66), Inner 91.8% (n=147), Outer 94.1% (n=101), Sideways 91.9% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
