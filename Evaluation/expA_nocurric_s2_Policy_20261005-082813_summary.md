# Evaluation: expA_nocurric_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 70.5% | 11.5% | 18.0% | 0.0% | 0.0% | 0.0% | 4.4 | 1.43 | 8.1 |
| 6 | 400 | 70.0% | 13.5% | 16.5% | 0.0% | 0.0% | 0.0% | 4.4 | 1.45 | 9.0 |
| 10 | 400 | 69.8% | 13.3% | 17.0% | 0.0% | 0.0% | 0.0% | 5.2 | 1.85 | 11.4 |

**Success by bay group** (per stage):

- Stage 4: Inner 70.5% (n=400)
- Stage 6: Inner 63.9% (n=219), Outer 77.3% (n=181)
- Stage 10: Corner 59.1% (n=66), Inner 72.8% (n=147), Outer 67.3% (n=101), Sideways 75.6% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
