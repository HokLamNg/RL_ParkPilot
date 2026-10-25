# Evaluation: round6_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 97.3% | 0.3% | 2.5% | 0.0% | 0.0% | 0.0% | 5.1 | 0.32 | 1.4 |
| 6 | 400 | 96.5% | 1.8% | 1.8% | 0.0% | 0.0% | 0.0% | 5.0 | 0.35 | 1.7 |
| 10 | 400 | 98.3% | 0.5% | 1.3% | 0.0% | 0.0% | 0.0% | 6.4 | 0.27 | 1.4 |

**Success by bay group** (per stage):

- Stage 4: Inner 97.3% (n=400)
- Stage 6: Inner 97.3% (n=219), Outer 95.6% (n=181)
- Stage 10: Corner 98.5% (n=66), Inner 95.9% (n=147), Outer 100.0% (n=101), Sideways 100.0% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
