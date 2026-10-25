# Evaluation: curric6_ppo_s1_64M (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 28 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 280 | 90.4% | 4.6% | 5.0% | 0.0% | 0.0% | 0.0% | 6.1 | 0.62 | 4.2 |
| 6 | 280 | 89.6% | 5.0% | 5.4% | 0.0% | 0.0% | 0.0% | 6.6 | 0.61 | 4.5 |
| 10 | 280 | 89.3% | 7.1% | 3.6% | 0.0% | 0.0% | 0.0% | 6.6 | 0.83 | 5.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 90.4% (n=280)
- Stage 6: Inner 88.3% (n=154), Outer 91.3% (n=126)
- Stage 10: Corner 82.6% (n=46), Inner 89.2% (n=102), Outer 91.0% (n=67), Sideways 92.3% (n=65)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
