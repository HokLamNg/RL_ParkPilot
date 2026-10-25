# Evaluation: finetune_proc2_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 78.5% | 2.5% | 19.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.68 | 3.6 |
| 6 | 400 | 79.0% | 3.5% | 17.5% | 0.0% | 0.0% | 0.0% | 4.9 | 0.75 | 4.6 |
| 10 | 400 | 81.5% | 3.3% | 14.8% | 0.5% | 0.0% | 0.0% | 6.5 | 0.83 | 5.8 |

**Success by bay group** (per stage):

- Stage 4: Inner 78.5% (n=400)
- Stage 6: Inner 74.9% (n=219), Outer 84.0% (n=181)
- Stage 10: Corner 89.4% (n=66), Inner 76.9% (n=147), Outer 85.1% (n=101), Sideways 79.1% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
