# Evaluation: expA_curric_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Geometry randomisation ON (target bay size, parked cars moved / turned / resized).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 89.8% | 6.8% | 3.5% | 0.0% | 0.0% | 0.0% | 5.3 | 0.64 | 3.9 |
| 6 | 400 | 92.5% | 5.3% | 2.3% | 0.0% | 0.0% | 0.0% | 5.3 | 0.56 | 3.5 |
| 10 | 400 | 86.0% | 10.5% | 3.5% | 0.0% | 0.0% | 0.0% | 7.1 | 0.98 | 5.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 89.8% (n=400)
- Stage 6: Inner 93.0% (n=244), Outer 91.7% (n=156)
- Stage 10: Corner 70.8% (n=48), Inner 89.0% (n=145), Outer 86.0% (n=121), Sideways 89.5% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
