# Evaluation: rdqn2_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 98.3% | 0.3% | 1.3% | 0.3% | 0.0% | 0.0% | 4.8 | 0.33 | 1.8 |
| 6 | 400 | 97.5% | 1.0% | 1.5% | 0.0% | 0.0% | 0.0% | 4.8 | 0.39 | 2.1 |
| 10 | 400 | 98.3% | 0.8% | 1.0% | 0.0% | 0.0% | 0.0% | 6.3 | 0.35 | 1.9 |

**Success by bay group** (per stage):

- Stage 4: Inner 98.3% (n=400)
- Stage 6: Inner 98.6% (n=219), Outer 96.1% (n=181)
- Stage 10: Corner 93.9% (n=66), Inner 98.6% (n=147), Outer 99.0% (n=101), Sideways 100.0% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
