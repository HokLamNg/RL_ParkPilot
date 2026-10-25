# Evaluation: finetune_geom_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Geometry randomisation ON (target bay size, parked cars moved / turned / resized).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 92.0% | 3.5% | 4.5% | 0.0% | 0.0% | 0.0% | 5.0 | 0.51 | 2.5 |
| 6 | 400 | 92.8% | 4.5% | 2.8% | 0.0% | 0.0% | 0.0% | 4.9 | 0.52 | 3.0 |
| 10 | 400 | 90.0% | 6.8% | 3.3% | 0.0% | 0.0% | 0.0% | 6.7 | 0.73 | 3.9 |

**Success by bay group** (per stage):

- Stage 4: Inner 92.0% (n=400)
- Stage 6: Inner 94.3% (n=244), Outer 90.4% (n=156)
- Stage 10: Corner 83.3% (n=48), Inner 91.0% (n=145), Outer 90.1% (n=121), Sideways 91.9% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
