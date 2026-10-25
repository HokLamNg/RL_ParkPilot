# Evaluation: expA_nocurric_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 75.5% | 10.8% | 13.8% | 0.0% | 0.0% | 0.0% | 4.5 | 1.14 | 6.0 |
| 6 | 400 | 74.0% | 13.3% | 12.5% | 0.3% | 0.0% | 0.0% | 4.5 | 1.27 | 7.3 |
| 10 | 400 | 77.5% | 14.3% | 8.3% | 0.0% | 0.0% | 0.0% | 5.3 | 1.34 | 7.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 75.5% (n=400)
- Stage 6: Inner 69.9% (n=219), Outer 79.0% (n=181)
- Stage 10: Corner 68.2% (n=66), Inner 82.3% (n=147), Outer 75.2% (n=101), Sideways 79.1% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
