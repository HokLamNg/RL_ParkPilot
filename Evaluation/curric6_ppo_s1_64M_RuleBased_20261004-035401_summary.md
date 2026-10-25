# Evaluation: curric6_ppo_s1_64M (RuleBased)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 28 areas.

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 280 | 29.3% | 41.1% | 6.1% | 22.5% | 1.1% | 0.0% | 14.8 | 4.32 | 28.6 |
| 6 | 280 | 27.9% | 38.9% | 6.4% | 26.4% | 0.4% | 0.0% | 14.7 | 4.58 | 27.9 |
| 10 | 280 | 19.6% | 51.8% | 3.2% | 24.6% | 0.7% | 0.0% | 16.7 | 6.55 | 34.9 |

**Success by bay group** (per stage):

- Stage 4: Inner 29.3% (n=280)
- Stage 6: Inner 23.4% (n=154), Outer 33.3% (n=126)
- Stage 10: Corner 17.4% (n=46), Inner 16.7% (n=102), Outer 22.4% (n=67), Sideways 23.1% (n=65)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
