# Evaluation: finetune_occ_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 93.5% | 3.5% | 3.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.45 | 2.6 |
| 6 | 400 | 92.3% | 5.0% | 2.8% | 0.0% | 0.0% | 0.0% | 4.9 | 0.49 | 2.5 |
| 10 | 400 | 91.8% | 5.3% | 3.0% | 0.0% | 0.0% | 0.0% | 6.3 | 0.64 | 3.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 93.5% (n=400)
- Stage 6: Inner 92.2% (n=219), Outer 92.3% (n=181)
- Stage 10: Corner 87.9% (n=66), Inner 91.2% (n=147), Outer 93.1% (n=101), Sideways 94.2% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
