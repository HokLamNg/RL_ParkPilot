# Evaluation: round4_bay_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 90.8% | 3.5% | 5.8% | 0.0% | 0.0% | 0.0% | 5.1 | 0.59 | 3.5 |
| 6 | 400 | 93.0% | 4.5% | 2.5% | 0.0% | 0.0% | 0.0% | 4.9 | 0.56 | 4.0 |
| 10 | 400 | 89.8% | 4.8% | 5.5% | 0.0% | 0.0% | 0.0% | 6.6 | 0.74 | 5.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 90.8% (n=400)
- Stage 6: Inner 91.3% (n=219), Outer 95.0% (n=181)
- Stage 10: Corner 87.9% (n=66), Inner 89.1% (n=147), Outer 90.1% (n=101), Sideways 91.9% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
