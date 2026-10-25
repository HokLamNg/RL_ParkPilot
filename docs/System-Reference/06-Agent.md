# 06 Agent (`ParkingAgent`): observations, actions, rewards, endings, statistics

Source: `Assets/Project/Scripts/ParkingAgent.cs` (1,079 lines, checked on 2026-10-17). Serialized values checked in
`Assets/Project/Prefabs/TrainingArea.prefab` (object `CarAgent`). They equal the code defaults unless stated.

## Purpose

The ML-Agents `Agent` that drives the car. It turns the policy's two actions into car inputs, builds the observation
vector, computes the reward, decides when an episode ends, and reports each episode (TensorBoard statistics and an
`EpisodeFinished` event used by the evaluation runner, the app and the showcase).

## Set-up on the car (prefab)

| Component / setting | Value |
|---|---|
| Behavior Parameters | name `ParkingAgent`. Vector observation size 9 (set at runtime to 9 or 11, see "Switches"). 2 continuous actions, no discrete branches. Behaviour type Default |
| Decision Requester | period 5, take actions between decisions on: the policy decides every 5 physics steps (10 Hz) and the action is repeated on the steps in between |
| Max Step | 1500 physics steps = 30 s (as in the class comment, lines 15-18). Far starts get `farStartMaxStep` = 3000 (60 s) instead (lines 262-263, see "Episode endings") |
| Main ray sensor (`RayPerceptionSensor`) | tags `Wall`, `ObstacleCar`. 8 rays per direction, max 180° (so 17 rays around the full circle, 22.5° apart). Sphere cast radius 0.2 m. Length 12 m. 0.5 m above the car. Every layer except Ignore Raycast. 68 inputs (17 x 4). Alternating ray order (`m_AlternatingRayOrder`): ray 1 points straight ahead, then alternately right and left, so rays 16 and 17 both point straight back |
| Footprint (`body`) | the car's box collider 1.9 x 4.4 m (`02-Car.md`) |

`OnActionReceived` therefore runs every physics step (0.02 s). `episodeSteps` counts physics steps.

## Actions (`OnActionReceived`, lines 375-427)

Two continuous actions, used directly as car inputs: `[0]` steering, `[1]` throttle, both -1..1
(`CarController.SetInputs`, see `02-Car.md` for what they do). If `Paused` (showcase drive-in), actions are ignored and
no reward or ending is computed. `EvaluationActionNoise` (evaluation only) adds Gaussian noise, redrawn when a new
decision arrives (see "Evaluation action noise").

## Observations (`CollectObservations`, lines 313-354)

Vector observation, in this order:

| # | Quantity | Scaling |
|---|---|---|
| 1 | bay centre's sideways position in the car's frame (+ = to the right) | / 30 m, clipped to -1..1 |
| 2 | bay centre's forward position in the car's frame (+ = ahead) | / 30 m, clipped |
| 3 | sin of the signed angle from the car's heading to the bay axis | |
| 4 | cos of that angle | |
| 5 | depth of the car centre along the bay axis (bay frame z, 0 = bay centre, + = further in) | / 3 m, clipped |
| 6 | forward velocity of the car (body frame) | / 5 m/s (not clipped, the car's top speed is 3, the scale stayed at the old top speed so the input kept its meaning, lines 147-149) |
| 7 | yaw rate | / 2 rad/s, clipped |
| 8 | wheel angle | / 35° |
| 9 | sideways offset of the car centre from the bay centre-line (bay frame x) | / 2 m, clipped |
| 10 | bay width (only with bay-size observations) | (w - 3.25) / 0.35, clipped to -1.5..1.5 |
| 11 | bay length (only with bay-size observations) | (l - 5.0) / 0.4, clipped to -1.5..1.5 |

Notes:
- Slots 5 and 9 replaced earlier inputs in reward version v9 (comments at lines 336 and 344).
- The heading in slots 3-4 is the full signed angle (-180..180°), so the agent can tell nose-in from reverse-in
  alignment, even though the parked check accepts both.
- With no target, all slots are 0.

Plus the sensors, which ML-Agents orders by sensor name: `RayPerceptionSensor` (68), `VectorSensor_size9/11`, and
with near rays `ZNearRays` (124). Each ray reports 4 numbers (ML-Agents `RayPerceptionSensor.ToFloatArray`): a one-hot
of the tag hit (`Wall`, `ObstacleCar`, all zeros if the object hit has neither tag), a flag that is 1 if the ray missed
everything, and the distance to the hit as a fraction of the ray length (1.0 on a miss). Pillars and row walls are
tagged `Wall`, so they look like walls.

## Switches: bay-size inputs and near-range rays (lines 165-258)

Both are decided once, when the agent is enabled (`OnEnable`), because the policy's input size cannot change later.

| Switch | Turned on by | Effect |
|---|---|---|
| `BaySizeObservation` | build started with `--bay-size-obs`, or `BaySizeObservationOverride` | vector size 11 instead of 9 |
| `NearRays` | build started with `--near-rays`, or `NearRaysOverride` | adds a second ray sensor `ZNearRays`: 15 rays per direction over +/-150° (31 rays, 10° apart), 4 m, sphere radius 0.1 m, same tags, layer mask and heights as the main sensor: 124 inputs |

Runners (evaluation, scenario/app, showcase) call `ParkingAgent.ConfigureFor(model)` in `Awake` (they run before the
agents are enabled): it reads the model's input sizes (`ModelInputSizes`) and sets both overrides to match
(11 present means bay size on, 124 present means near rays on). Models trained before round 4 have inputs 68 + 9.
Round 4 has 68 + 11. Round 5 and everything trained since have 68 + 11 + 124: the round 6 pilots, `round6_s1-s3`, the
full run (`full3_s1-s3`) and the DQN round 6b models `rdqn2_s1-s3` (`13-DQN-Baseline.md`). One evaluation session cannot
mix models with different inputs.

## Reward (shaped mode, the default)

Per physics step (lines 397-403):

`reward = -0.001 (step penalty) + (Phi_now - Phi_before)` (shaping scale 1)

Potential `Phi` (`Potential`, lines 993-1010), with `d` = straight distance from the car centre to the bay centre (m),
`r` = the reward distance (below) and `e` = heading error to the bay axis (0-90°, reverse-in counts):

```
Phi = - r / 30
      + 0.5 x closeness x alignment          closeness = clamp01(1 - d / 6),  alignment = 1 - e / 90
      + 0.5 x inBay                          mean over the 4 footprint corners of clamp01(1 - outside / 1.5)
      + 0.5 x lineUp                         clamp01(1 - d / 8) x clamp01(1 - |lateral| / 0.75) x clamp01(1 - e / 30)
      + 0.5 x stop                           inBay x clamp01(1 - e / 20) x clamp01(1 - |speed| / 1)
      + 0.3 x slowApproach                   clamp01(1 - d / 6) x clamp01(1 - |speed| / 2)
```

`outside` = a corner's distance outside the bay rectangle enlarged by 0.15 m. `lateral` = offset from the bay
centre-line.

Reward distance `r` (`environment.RewardDistance`, round 6): with the environment parameter `route_distance` = 1
it is the route distance from the car centre to the bay centre along drivable space (the shortest route on the
`DrivableMap` grid around parked cars, pillars and walls, enlarged by the car's half-width plus `route_clearance`,
default 0.3 m, with empty bays passable at `empty_bay_cost` times the cost, default 3). With `route_distance` = 0 (the
default, rounds 1-5 and `r6pilot_straight`) it is the straight line, so `r` = `d`. `r6pilot_route` and round 6
(`parking_ppo_round6.yaml`) use 1. The full run (`parking_ppo_full.yaml`) uses 0 until its last lesson G7 and 1 from
then on. Only this first term changes. The other
terms always use the straight distance `d`. The parameters are described in `04-Environment.md`.

Because the reward is the change in `Phi`, driving in circles or in and out again earns nothing overall
(comments at lines 984-992 and 1012-1026). The difference is taken without the discount factor (Phi_now - Phi_before, not
gamma x Phi_now - Phi_before). So the policy-invariance result of Ng, Harada and Russell (1999), which needs
gamma x Phi_now - Phi_before, does not strictly apply: per decision the shaping equals that form plus
(1 - gamma) x Phi_now.

Terminal rewards (`EndWith`):

| Ending | Shaped | Sparse |
|---|---|---|
| Parked | +2 | +2 |
| Collision | -1 | -1 |
| Out of bounds | -1 | -1 |
| Stuck, No progress | -1 | 0 |
| Timeout | none (treated as a truncation) | none |

Contact penalties (shaped only): a soft contact costs -0.3 (stages with `softContact`, see below), and every physics
step touching a wall or parked car costs -0.005 (`OnCollisionStay`, any stage).

Sparse mode (`sparse_reward` = 1, Experiment B): no step penalty, no shaping, no contact penalties. Only the terminal
rewards above.

## Episode endings

| Ending | Condition | Code |
|---|---|---|
| Parked | the parked condition holds for 25 consecutive physics steps (0.5 s) | 413-420 |
| Collision | `OnCollisionEnter` with a collider tagged `Wall` or `ObstacleCar`, unless it is a soft contact | 571-589 |
| Out of bounds | car outside the area or 2 m below it | 407-411 |
| Stuck | the car has moved less than 0.3 m and turned less than 10° for 150 steps (3 s). With `stuck_steps_near_bay` > 0, the limit near the bay (within 3 m of its centre, or a footprint corner inside) is that many steps instead (round 5: 400 = 8 s) | 461-474 |
| No progress | the best potential has not improved by at least 0.02 for 500 steps (10 s) | 434-444 |
| Timeout | Max Step reached: 1500 (30 s), or 3000 (60 s, `farStartMaxStep`) when the episode is a far start. `OnEpisodeBegin` sets `MaxStep = FarStart ? max(base, 3000) : base` after each reset. `FarStart` is set for far starts (`far_start_share`, evaluation `farStarts`) and for scenario starts more than 15 m from the bay (e.g. the showcase's centre hand-over). Reported at the start of the next episode | 289, 296 |

Stuck and No progress are only checked while the car is not in the middle of a parked hold, and only when early endings
are on (`early_endings`, default 1). They were added in reward versions v6 and v7 to break local optima of the shaped
reward (tooltips, lines 122-142).

Parked condition (`IsParked`, 721-729): |speed| <= 0.3 m/s, heading error <= 10° (nose-in or reverse-in,
`allowReverseParking`), and all 4 footprint corners inside the bay enlarged by 0.15 m.

Soft contact (stages 3-8 only, the stage's `softContact` flag): an impact slower than 1 m/s is penalised (-0.3) but
the episode continues, up to 3 per episode. The next one is a crash. In stages 0-2 and 9-10 every touch of a wall or
parked car ends the episode. All fine-tuning rounds use stage 10.

What was hit (`CollisionKind`): `car` (tag `ObstacleCar`), `pillar` / `row wall` (by object name, generated car
parks), otherwise `boundary wall`.

## Episode report (`Report`, lines 619-686, `AddSituationStats` 692-719)

`EpisodeResult` (lines 25-51) holds the outcome, steps, stage, target, final distance and heading error, reward, the
spawn values, approach shape (`Straight` under 45° of turning before the first full entry, `Curved` 45-135°, `UTurn`
above), nose-in or reverse-in, number of entries, entry speed, soft contacts, layout index and seed epoch (for
evaluation), what was hit, and (round 6) `spawnFar` (far start, or a scenario start over 15 m) and `spawnRoute`
(route distance from the start to the bay in m, -1 if no route map was built). It is passed to `EpisodeFinished` listeners.

TensorBoard statistics written each episode (prefix `Parking/`, `Report` and the functions it calls). They do not
affect rewards, actions or observations (comment in the diagnostics section). Two groups, about 117 cards in a run
with far starts and generated car parks:

Outcome monitoring:
- outcome rates: `SuccessRate`, `SuccessRateProcedural` / `SuccessRateTrainingLot`, `CollisionRate`, `TimeoutRate`,
  `StuckRate`, `NoProgressRate`. `CollisionWith/{car, pillar, row wall, boundary wall}` (share of collisions) and
  `StuckType/{Frozen, NearBay, Elsewhere}` ("Frozen" = ended as stuck within the first 155 steps, never moved)
- per bay group (`ByGroup/<group>/{SuccessRate, CollisionRate, StuckRate}`)
- per situation (`AddSituationStats`), each with `{SuccessRate, CollisionRate, StuckRate}`: the start,
  `Situation/{lot}/{StartClose|StartNormal|FarStart}` (under 7 m, 7 m or more, a far start), and
  `Situation/{lot}/Neighbours0-2` on both kinds of car park (`lot` = `Generated` or `TrainingLot`), and only on generated
  car parks `Situation/Generated/{PillarBeside|PillarNone}`,
  `Situation/Generated/{AisleNarrow|AisleWide}` (aisle under / over 8 m) and `Situation/Generated/{HardCaseYes|HardCaseNo}`
- `FinalDistance`, `FinalHeadingError`, `SoftContactsPerEpisode`, `StepsToPark`

Targeted diagnostics (each added to test one explanation during development, kept for later checks):
- failure diagnostics (`Diag/...`, `ReportDiagnostics`, line 884, v9 to v10): the furthest "parked" condition
  reached in a failed episode (`FailReason/{NeverInBay, PartlyIn, InButCrooked, InAlignedNotStopped}`), entries, depth
  and lateral offset at the best moment, best heading while fully inside, speed while aligned
- spawn, approach and finish breakdowns (`Spawn/Heading/...`, `Spawn/Lateral/...`, `Approach/...`, `Finish/...`, `ReportApproachStats`, v11): success by start heading and sideways offset, by approach shape (share and
  success) and by finish direction, entry speed (under `Finish/<direction>/EntrySpeed`)

Removed on 2026-10-17 (to-do T1) as duplicates or constant: the per-group copies of the diagnostics
(`ByGroup/<group>/FailReason/...` and `ByGroup/<group>/Diag/...`), `ByNeighbours/...` (covered by
`Situation/<lot>/Neighbours*`), `Approach/<kind>/EntrySpeed` (same values as `Finish/<direction>/EntrySpeed`),
`CurriculumStage` (ML-Agents logs `Environment/Lesson Number/curriculum_stage`), `OutOfBoundsRate` (always 0, the
boundary walls end the episode first) and `Debug/BaseDrift*` (a one-time check of the round 5 geometry fix).
Merged the same day: the start split (until then `Situation/{lot}/{StartClose|StartFar}` by 7 m and
`Situation/{lot}/{FarStartYes|FarStartNo}` overlapped), `Spawn/Distance/...` (covered by the start split) and the rare
per-group `TimeoutRate` and `NoProgressRate`. Runs trained before that date still contain the old tags.

## Heuristic modes (`Heuristic`, lines 476-566)

Used when the behaviour type is Heuristic Only:
- Keyboard: W/S or arrows = throttle, A/D or arrows = steer, Space = brake.
- Random: uniform random steer and throttle each decision (evaluation baseline).
- RuleBased: a hand-written driver (evaluation baseline, nose-in only, no obstacle avoidance): drive forward to a
  staging point 6 m in front of the bay on its centre-line. Then follow the centre-line using a look-ahead point 3 m
  ahead on it, creep at 0.8 m/s, brake within 0.3 m of the bay centre. If more than 30° off while still outside, reverse
  for 15 decisions (1.5 s) to line up again. The comment records that a first version aimed at the bay centre point and
  parked 0%.

## Hooks for other parts (read-only unless stated)

| Member | Used by |
|---|---|
| `EpisodeFinished` event | evaluation runner, scenario runner (app), showcase |
| `Paused` (writable) | showcase: ignore actions and endings during the scripted drive-in and after the result |
| `GetTrace()` | scenario traces, showcase gauges and logs: distance, heading error, depth, lateral, shaping terms, corners inside, slow / aligned / parked flags |
| `GetDebugCounters()` | showcase overlay: steps, parked hold, soft contacts, steps since progress / since moving and their limits |
| `EvaluationActionNoise` (static, writable) | evaluation runner (see below) |
| `HoldReset` (on the environment) | when set, `OnEpisodeBegin` skips the reset so the final pose stays visible |

### Evaluation action noise (lines 359-394)

Unity's own "stochastic" inference does not resample its noise in practice, so the evaluation
runner can instead add Gaussian noise with a given standard deviation per action to the deterministic action. The noise
is redrawn whenever the incoming action changes, i.e. once per decision, as in training. The random generator is seeded
per agent (1234 + a counter). Zero = off (the default).
