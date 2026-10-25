# Evaluation: expA_curric_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 87.0% | 10.0% | 3.0% | 0.0% | 0.0% | 0.0% | 5.3 | 0.87 | 4.4 |
| 6 | 400 | 88.0% | 6.8% | 5.3% | 0.0% | 0.0% | 0.0% | 5.3 | 0.85 | 5.0 |
| 10 | 400 | 84.5% | 7.5% | 7.8% | 0.3% | 0.0% | 0.0% | 6.8 | 1.21 | 6.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 87.0% (n=400)
- Stage 6: Inner 85.4% (n=219), Outer 91.2% (n=181)
- Stage 10: Corner 78.8% (n=66), Inner 89.1% (n=147), Outer 77.2% (n=101), Sideways 89.5% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
