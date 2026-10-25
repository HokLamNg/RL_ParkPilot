# Evaluation: curric6_ppo_s1_64M (Random)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 28 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 280 | 0.0% | 2.9% | 84.3% | 12.9% | 0.0% | 0.0% | - | 8.85 | 42.3 |
| 6 | 280 | 0.0% | 1.8% | 86.8% | 11.4% | 0.0% | 0.0% | - | 8.68 | 43.9 |
| 10 | 280 | 0.0% | 2.9% | 88.9% | 8.2% | 0.0% | 0.0% | - | 10.38 | 44.2 |

**Success by bay group** (per stage):

- Stage 4: Inner 0.0% (n=280)
- Stage 6: Inner 0.0% (n=154), Outer 0.0% (n=126)
- Stage 10: Corner 0.0% (n=46), Inner 0.0% (n=102), Outer 0.0% (n=67), Sideways 0.0% (n=65)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
