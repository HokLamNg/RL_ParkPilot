# Evaluation: finetune_geom_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Geometry randomisation ON (target bay size, parked cars moved / turned / resized).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 93.0% | 3.5% | 3.5% | 0.0% | 0.0% | 0.0% | 5.2 | 0.47 | 2.2 |
| 6 | 400 | 91.0% | 5.5% | 3.0% | 0.5% | 0.0% | 0.0% | 5.0 | 0.57 | 3.1 |
| 10 | 400 | 87.8% | 8.5% | 3.8% | 0.0% | 0.0% | 0.0% | 6.7 | 0.82 | 4.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 93.0% (n=400)
- Stage 6: Inner 89.3% (n=244), Outer 93.6% (n=156)
- Stage 10: Corner 85.4% (n=48), Inner 85.5% (n=145), Outer 88.4% (n=121), Sideways 91.9% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
