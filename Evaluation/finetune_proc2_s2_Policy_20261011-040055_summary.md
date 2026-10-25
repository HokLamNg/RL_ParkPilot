# Evaluation: finetune_proc2_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.5% | 2.5% | 4.8% | 0.3% | 0.0% | 0.0% | 5.1 | 0.55 | 3.6 |
| 6 | 400 | 93.5% | 3.5% | 3.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.48 | 2.9 |
| 10 | 400 | 91.3% | 3.5% | 5.3% | 0.0% | 0.0% | 0.0% | 6.6 | 0.72 | 4.9 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.5% (n=400)
- Stage 6: Inner 92.7% (n=219), Outer 94.5% (n=181)
- Stage 10: Corner 89.4% (n=66), Inner 88.4% (n=147), Outer 93.1% (n=101), Sideways 95.3% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
