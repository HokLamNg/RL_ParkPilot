# Evaluation: round5_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 96.0% | 2.3% | 1.5% | 0.3% | 0.0% | 0.0% | 5.2 | 0.33 | 2.0 |
| 6 | 400 | 95.8% | 2.5% | 1.8% | 0.0% | 0.0% | 0.0% | 4.9 | 0.36 | 2.5 |
| 10 | 400 | 96.8% | 2.0% | 1.3% | 0.0% | 0.0% | 0.0% | 6.4 | 0.35 | 2.3 |

**Success by bay group** (per stage):

- Stage 4: Inner 96.0% (n=400)
- Stage 6: Inner 95.4% (n=219), Outer 96.1% (n=181)
- Stage 10: Corner 92.4% (n=66), Inner 98.6% (n=147), Outer 97.0% (n=101), Sideways 96.5% (n=86)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
