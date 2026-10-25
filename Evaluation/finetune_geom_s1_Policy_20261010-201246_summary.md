# Evaluation: finetune_geom_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.0% | 5.3% | 2.8% | 0.0% | 0.0% | 0.0% | 5.1 | 0.55 | 2.5 |
| 6 | 400 | 89.0% | 6.3% | 4.8% | 0.0% | 0.0% | 0.0% | 4.9 | 0.66 | 4.0 |
| 10 | 400 | 89.5% | 7.3% | 3.3% | 0.0% | 0.0% | 0.0% | 6.5 | 0.88 | 5.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.0% (n=400)
- Stage 6: Inner 88.6% (n=219), Outer 89.5% (n=181)
- Stage 10: Corner 83.3% (n=66), Inner 89.8% (n=147), Outer 92.1% (n=101), Sideways 90.7% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
