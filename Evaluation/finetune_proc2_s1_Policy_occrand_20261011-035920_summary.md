# Evaluation: finetune_proc2_s1 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 4 | 400 | 61.3% | 11.8% | 25.3% | 1.5% | 0.3% | 0.0% | 5.0 | 1.42 | 8.0 |
| 6 | 400 | 66.0% | 10.8% | 22.3% | 1.0% | 0.0% | 0.0% | 5.0 | 1.34 | 8.5 |
| 10 | 400 | 63.0% | 10.3% | 26.3% | 0.5% | 0.0% | 0.0% | 6.5 | 1.71 | 10.7 |

**Success by bay group** (per stage):

- Stage 4: Inner 61.3% (n=400)
- Stage 6: Inner 64.6% (n=237), Outer 68.1% (n=163)
- Stage 10: Corner 63.9% (n=61), Inner 63.0% (n=154), Outer 66.7% (n=105), Sideways 57.5% (n=80)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
