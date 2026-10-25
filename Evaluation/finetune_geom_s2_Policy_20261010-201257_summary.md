# Evaluation: finetune_geom_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.0% | 4.3% | 3.8% | 0.0% | 0.0% | 0.0% | 5.2 | 0.57 | 3.0 |
| 6 | 400 | 89.8% | 6.3% | 4.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.64 | 3.9 |
| 10 | 400 | 88.3% | 7.3% | 4.5% | 0.0% | 0.0% | 0.0% | 6.7 | 0.85 | 5.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.0% (n=400)
- Stage 6: Inner 91.8% (n=219), Outer 87.3% (n=181)
- Stage 10: Corner 84.8% (n=66), Inner 91.8% (n=147), Outer 84.2% (n=101), Sideways 89.5% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
