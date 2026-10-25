# Evaluation: full3_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 98.8% | 0.5% | 0.8% | 0.0% | 0.0% | 0.0% | 5.1 | 0.18 | 1.5 |
| 6 | 400 | 100.0% | 0.0% | 0.0% | 0.0% | 0.0% | 0.0% | 5.0 | 0.15 | 1.1 |
| 10 | 400 | 98.8% | 0.5% | 0.8% | 0.0% | 0.0% | 0.0% | 6.4 | 0.20 | 1.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 98.8% (n=400)
- Stage 6: Inner 100.0% (n=219), Outer 100.0% (n=181)
- Stage 10: Corner 98.5% (n=66), Inner 98.6% (n=147), Outer 99.0% (n=101), Sideways 98.8% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
