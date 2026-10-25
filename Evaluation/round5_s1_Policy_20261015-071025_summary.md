# Evaluation: round5_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 94.8% | 2.0% | 3.3% | 0.0% | 0.0% | 0.0% | 4.9 | 0.41 | 1.9 |
| 6 | 400 | 93.8% | 2.8% | 3.5% | 0.0% | 0.0% | 0.0% | 4.8 | 0.48 | 3.1 |
| 10 | 400 | 95.8% | 2.8% | 1.5% | 0.0% | 0.0% | 0.0% | 6.2 | 0.40 | 2.5 |

**Success by bay group** (per stage):

- Stage 4: Inner 94.8% (n=400)
- Stage 6: Inner 93.2% (n=219), Outer 94.5% (n=181)
- Stage 10: Corner 93.9% (n=66), Inner 96.6% (n=147), Outer 95.0% (n=101), Sideways 96.5% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
