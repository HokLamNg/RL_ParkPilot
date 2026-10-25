# Evaluation: finetune_proc_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 90.0% | 3.5% | 6.5% | 0.0% | 0.0% | 0.0% | 5.1 | 0.64 | 3.7 |
| 6 | 400 | 88.5% | 4.3% | 7.3% | 0.0% | 0.0% | 0.0% | 4.9 | 0.78 | 4.5 |
| 10 | 400 | 86.0% | 5.3% | 8.5% | 0.3% | 0.0% | 0.0% | 6.5 | 0.86 | 5.8 |

**Success by bay group** (per stage):

- Stage 4: Inner 90.0% (n=400)
- Stage 6: Inner 87.2% (n=219), Outer 90.1% (n=181)
- Stage 10: Corner 83.3% (n=66), Inner 85.0% (n=147), Outer 86.1% (n=101), Sideways 89.5% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
