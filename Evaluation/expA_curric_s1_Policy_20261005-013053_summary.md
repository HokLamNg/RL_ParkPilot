# Evaluation: expA_curric_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 87.3% | 8.3% | 4.5% | 0.0% | 0.0% | 0.0% | 5.3 | 0.86 | 4.2 |
| 6 | 400 | 87.5% | 9.3% | 3.3% | 0.0% | 0.0% | 0.0% | 5.2 | 0.78 | 4.7 |
| 10 | 400 | 78.8% | 13.5% | 7.0% | 0.8% | 0.0% | 0.0% | 7.1 | 1.63 | 8.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 87.3% (n=400)
- Stage 6: Inner 87.2% (n=219), Outer 87.8% (n=181)
- Stage 10: Corner 66.7% (n=66), Inner 85.0% (n=147), Outer 77.2% (n=101), Sideways 79.1% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
