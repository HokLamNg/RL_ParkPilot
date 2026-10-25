# Evaluation: curric6_ppo_s1_64M (RuleBased)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 28 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 280 | 0.0% | 46.4% | 27.1% | 24.3% | 2.1% | 0.0% | - | 4.80 | 35.3 |
| 6 | 280 | 0.0% | 47.5% | 22.5% | 28.9% | 1.1% | 0.0% | - | 5.09 | 35.6 |
| 10 | 280 | 0.0% | 57.1% | 16.1% | 25.7% | 1.1% | 0.0% | - | 6.84 | 38.6 |

**Success by bay group** (per stage):

- Stage 4: Inner 0.0% (n=280)
- Stage 6: Inner 0.0% (n=154), Outer 0.0% (n=126)
- Stage 10: Corner 0.0% (n=46), Inner 0.0% (n=102), Outer 0.0% (n=67), Sideways 0.0% (n=65)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
