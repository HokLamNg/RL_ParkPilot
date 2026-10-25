# Evaluation: round6_s2 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 97.5% | 0.3% | 2.3% | 0.0% | 0.0% | 0.0% | 5.1 | 0.23 | 1.4 |
| 6 | 400 | 97.5% | 1.0% | 1.5% | 0.0% | 0.0% | 0.0% | 5.0 | 0.29 | 1.7 |
| 10 | 400 | 97.8% | 1.0% | 1.3% | 0.0% | 0.0% | 0.0% | 6.4 | 0.33 | 2.0 |

**Success by bay group** (per stage):

- Stage 4: Inner 97.5% (n=400)
- Stage 6: Inner 98.6% (n=219), Outer 96.1% (n=181)
- Stage 10: Corner 97.0% (n=66), Inner 96.6% (n=147), Outer 99.0% (n=101), Sideways 98.8% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
