# Evaluation: finetune_occ_s3 (Policy, deterministic)

Seeds: 100000 + stage x 1000 + area; 10 episodes per area per stage; 40 areas. Occupancy randomisation ON (0-27 parked cars). Procedural car parks ON (a new generated car park per layout).

| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |
|---|---|---|---|---|---|---|---|---|---|---|
| 10 | 400 | 67.8% | 26.3% | 5.5% | 0.5% | 0.0% | 0.0% | 6.7 | 1.67 | 10.2 |

**Success by bay group** (per stage):

- Stage 10: Inner 67.8% (n=400)

Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.
