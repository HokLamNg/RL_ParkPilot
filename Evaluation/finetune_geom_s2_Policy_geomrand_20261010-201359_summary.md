# Evaluation: finetune_geom_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Geometry randomisation ON (target bay size, parked cars moved / turned / resized).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.3% | 3.3% | 4.5% | 0.0% | 0.0% | 0.0% | 5.3 | 0.56 | 2.7 |
| 6 | 400 | 93.8% | 4.0% | 2.3% | 0.0% | 0.0% | 0.0% | 5.1 | 0.44 | 2.9 |
| 10 | 400 | 88.3% | 8.0% | 3.8% | 0.0% | 0.0% | 0.0% | 6.6 | 0.88 | 5.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.3% (n=400)
- Stage 6: Inner 94.7% (n=244), Outer 92.3% (n=156)
- Stage 10: Corner 85.4% (n=48), Inner 89.0% (n=145), Outer 90.1% (n=121), Sideways 86.0% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
