# Evaluation: finetune_proc_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 90.3% | 1.8% | 8.0% | 0.0% | 0.0% | 0.0% | 5.2 | 0.53 | 3.5 |
| 6 | 400 | 90.3% | 3.5% | 6.3% | 0.0% | 0.0% | 0.0% | 5.1 | 0.56 | 4.0 |
| 10 | 400 | 86.3% | 4.0% | 9.8% | 0.0% | 0.0% | 0.0% | 6.7 | 0.76 | 5.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 90.3% (n=400)
- Stage 6: Inner 88.1% (n=219), Outer 92.8% (n=181)
- Stage 10: Corner 87.9% (n=66), Inner 87.8% (n=147), Outer 89.1% (n=101), Sideways 79.1% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
