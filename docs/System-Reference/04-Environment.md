# 04 Environment (`ParkingEnvironment`, the training area)

Sources: `Assets/Project/Scripts/ParkingEnvironment.cs` (705 lines), `Assets/Project/Scripts/DrivableMap.cs`
(317 lines), `Assets/Project/Prefabs/TrainingArea.prefab`, `Assets/Project/Scenes/Training.unity` (checked on
2026-10-17).

## Purpose

One `ParkingEnvironment` sits on the root of each TrainingArea: a 40 x 40 m car park with walls, 30 bays and one
car. At the start of every episode it builds the situation: which car park (the training one or a generated one),
the target bay, the parked cars, the bay and car geometry, and the car's start pose. It also answers geometric
questions for the agent (inside the area? overlapping an obstacle?). Everything is placed relative to the area's
transform, so the prefab can be copied many times in one scene.

## The training area (prefab and scene)

| Item | Value | Where |
|---|---|---|
| Drivable area | `areaHalfExtents` 20 x 20 m (code default 15 x 15, overridden in the prefab) | prefab |
| Walls | 4 boxes tagged `Wall`, 1.5 m high, 0.5 m thick, centred on the area edges at +/-20 m ("North / South / East / West Walls") | prefab |
| Ground | a plane scaled 4 x 4 (40 x 40 m) | prefab |
| Bays | 30, 3.2 x 4.88 m (`03-Bays.md`) | prefab |
| Parked-car prefab | `ObstacleCar.prefab` | `obstacleCarPrefab` |
| Car spawn height | 0.05 m above the area | `carSpawnHeight` |
| Areas in `Training.unity` | 40 instances of the prefab, no per-instance overrides of the environment's fields | scene |

Fields added after the prefab was last saved (geometry, occupancy, early-ending and procedural overrides, and the
round 6 fields) are not serialized in it, so the code defaults listed below apply.

## Episode reset (`ResetArea`, lines 361-416)

Called by the agent at the start of every episode (`06-Agent.md`). In order:

1. Read the environment parameters (`ReadParameters`, 623-655, table below).
2. `LayoutIndex++`.
3. Scenario mode: if `ActiveScenario` is set (app, scenario tests, showcase), apply it and stop here (see below).
4. Choose the car park (`ChooseLayout`, 238-265): with probability `ProceduralShare`, a new generated car park,
   otherwise the training car park (`05-Generated-Car-Parks.md`).
5. Target bay:
   - hard-case episode (generated car park, probability `HardCaseShare`): a bay with a pillar beside it, else one on
     an aisle under 8 m, else any bay (`HardTargetIndex`, 600-608)
   - else, if the stage has `randomTarget`: a random bay from the stage's `targetGroups` (`RandomTargetIndex`, 610-621)
   - else `fixedTargetIndex` (8 = Bay_N2_W3).
6. Parked cars: the other bays are shuffled and the first *n* are occupied. *n* is drawn from the stage's
   `occupiedBays` (min, max inclusive), or with occupancy randomisation from `occupancyRange` = 0-27 on the training
   car park and 0 to 90% of the bays on a generated one. The target is never occupied.
7. Tight-bay practice: with probability `tightBayChance` (per stage) both direct neighbours of the target are also
   occupied. `TargetOccupiedNeighbours` (0-2) is then counted.
8. Geometry: `RestoreGeometry()` puts every bay and parked car back to its base. Then, if geometry randomisation
   is on (always on generated car parks, on a `GeometryShare` of training-car-park episodes), `RandomiseGeometry`.
   Then `Physics.SyncTransforms()`.
9. Route map (round 6, `BuildMap`, 190-203): only when `FarStartShare` > 0 or `UseRouteDistance` is on, the
   `DrivableMap` is rebuilt for this episode's bays, parked cars and target (see below). Otherwise the map is marked
   unused (`Invalidate`).
10. Place the car (`PlaceCar`). Then `SpawnRoute` (the route distance from the start to the bay centre, -1 without
    a map) and the read-only `routeMapInfo` text are set (lines 411-415).

### Neighbours (`FindNeighbours`, 339-358)

Bay B is a direct neighbour of bay A if it faces the same way (dot product of their forward axes at least 0.9), its
centre is between 0.5 and 1.5 bay widths to A's side, and less than half a bay length along A's axis. For the
sideways bays that means the bays above and below. `TargetSurroundings()` (324-337, evaluation) reports the parked cars
in the whole car park and whether the left / right neighbour (as seen driving in) is occupied.

### Start pose (`PlaceCar`, 418-450)

First, with probability `FarStartShare`, the episode is a far start (no random draw when the share is 0) and
`PlaceFar` is tried (next section). If it finds no pose, `FarStart` is set back to false and the normal start is used.

Normal ("near") start: the car starts in front of the target bay, measured from the bay centre:
- `distance` back along the bay axis (from `spawnDistance`, in a hard case, half of the time at most 7 m),
- `lateral` sideways offset (+/- `lateralOffset`),
- `yaw` heading offset from facing straight into the bay (+/- `headingNoise`).

A pose is rejected if it is within 2 m of the area edge or if the car's box (plus 0.2 m) would overlap a `Wall` or
`ObstacleCar` collider (`Overlaps`, 667-677). After `maxSpawnAttempts` = 30 failures it uses a fallback: 5 m
straight in front, facing the bay, without the overlap check, and logs a warning. The values used are stored as
`SpawnDistance`, `SpawnLateral`, `SpawnYaw`, `SpawnFallback` (logged per episode).

Note: because `distance` is measured from the bay centre, a 5 m start puts the car's centre only about 2.6 m in front
of the bay mouth.

### Far starts (round 6, `PlaceFar`, 458-489)

Needs the route map (returns false without it). Up to `maxSpawnAttempts` = 30 tries:
- Showcase hand-over pose: on the training car park only, a `showcaseStartShare` (0.15) of the tries use the pose
  where the showcase hands the car to the agent: x -1..0 m, z -0.4..+0.4 m in the area's frame (the car park centre),
  yaw 90° +/-8° (facing east along the central aisle).
- Otherwise an aisle cell: `DrivableMap.SampleAisleCell` picks a free cell outside every bay whose route distance
  to the target lies in `farStartRoute` (10-40 m). If it finds none, `PlaceFar` gives up at once. The heading is
  along one of the area's axes +/-15° half of the time, else any direction.
- The same checks as a near start: rejected within 2 m of the area edge or when `Overlaps` reports a wall or parked
  car.

The logged values differ from a near start: `SpawnDistance` is the straight-line distance to the bay centre,
`SpawnLateral` is 0, and `SpawnYaw` is the signed angle between the bay's axis and the car's heading. A far start
also raises the agent's time limit: `ParkingAgent.OnEpisodeBegin` sets `MaxStep` to at least `farStartMaxStep`
(3000 physics steps = 60 s, instead of the prefab's 1500) while `FarStart` is true (`ParkingAgent.cs:263` and `:296`).

### Route map (round 6, `DrivableMap`)

The distance a car would actually have to drive to the target bay, around parked cars, pillars and walls (class
comment, `DrivableMap.cs:6-20`). The policy never sees it. It is used for far starts and, with `route_distance` on, for
the reward's distance term. Built at each reset by `Build` (81-142):
- A grid of `mapCellSize` (0.4 m) cells over the whole area (100 x 100 cells for 40 x 40 m).
- Obstacles: every enabled, non-trigger collider under the area tagged `Wall` or `ObstacleCar` (boundary walls,
  parked cars, generated pillars and row walls). Its ground footprint (an oriented rectangle for box colliders, else the
  bounding box) is enlarged by `carHalfWidth` + `route_clearance` (0.95 + 0.3 = 1.25 m by default), and every cell
  whose centre lies within that distance of the footprint is blocked. A free cell is therefore a place where the car's
  centre can be with room to spare.
- Target zone: inside the target bay and up to 1.5 m in front of its open end, only an obstacle's own footprint
  blocks, not the enlargement (`InTargetZone`, 222-226). So a bay between two parked cars or beside a pillar stays
  reachable. The cell at the target centre is never blocked.
- Costs: the target bay and the aisles cost 1 per metre, every other bay costs `empty_bay_cost` (3) per metre.
  Occupied bays are mostly covered by their car anyway, so in practice this applies to empty bays: routes may cross
  them but prefer the aisles.

`Distance(world, target)` (149-173): the straight line when the map is not ready. Otherwise bilinear interpolation
between the four surrounding cell centres when all four are reachable. Next to an obstacle (inside the enlarged zone),
the smallest "route of a reachable cell + straight step to it" over the cells within about 3 m. If none is reachable
there, the straight line. The result is never below the straight-line distance.

`SampleAisleCell` (177-192) draws up to 400 random cells from the caller's generator. `DrivableMap` itself draws no
random numbers. `Summary()` gives the Inspector text (share of blocked, reachable and unreachable free cells, longest
route).

`RewardDistance(worldPos)` (180-187): the route distance to the target centre when `UseRouteDistance` is on and the map
is ready, else the straight-line distance on the ground. The agent's potential uses it for its far-range distance term
(`ParkingAgent.cs:1004`). The other terms that depend on distance (alignment, line-up, slow approach) keep the
straight-line distance (`06-Agent.md`).

### Curriculum stages (`DefaultStages`, 212-225, identical in the prefab)

| # | Name | Spawn distance (m) | Lateral (m) | Heading (°) | Parked cars | Target | Tight-bay chance | Soft contact |
|---|---|---|---|---|---|---|---|---|
| 0 | Fixed bay, near, aligned | 5-7 | 0.5 | 10 | 0 | fixed (bay 8) | 0 | no |
| 1 | Moderate spawn + heading | 5-9 | 2 | 45 | 0 | fixed | 0 | no |
| 2 | Random spawn + heading | 5-12 | 4 | 90 | 0 | fixed | 0 | no |
| 3 | Static parked cars | 5-12 | 4 | 90 | 1-3 | fixed | 0.3 | yes |
| 4 | Random inner bay | 5-12 | 4 | 90 | 1-3 | Inner | 0.5 | yes |
| 5 | Outer-row practice | 5-8 | 1.5 | 45 | 1-3 | Outer | 0.3 | yes |
| 6 | Inner + outer rows | 5-12 | 4 | 90 | 1-3 | Inner, Outer | 0.5 | yes |
| 7 | + Corner bays | 5-12 | 4 | 90 | 1-3 | Inner, Outer, Corner | 0.5 | yes |
| 8 | + Sideways (all bays) | 5-12 | 4 | 90 | 1-3 | all | 0.5 | yes |
| 9 | Sharper turnarounds | 5-13 | 6 | 135 | 1-4 | all | 0.5 | no |
| 10 | Full task | 5-14 | 8 | 180 | 2-6 | all | 0.5 | no |

"Soft contact" lets low-speed touches be penalised without ending the episode (`06-Agent.md`). The stage is chosen by
the trainer (`curriculum_stage`) or forced with `stageOverride`. All fine-tuning rounds use stage 10.

## Environment parameters (`ReadParameters`)

Read at every reset from ML-Agents (`Academy.Instance.EnvironmentParameters`), so a trainer curriculum can change them
during a run. An Inspector override takes precedence where one exists.

| Parameter | Default | Inspector override | Effect |
|---|---|---|---|
| `curriculum_stage` | 0 | `stageOverride` (>= 0) | stage index 0-10 |
| `sparse_reward` | 0 | `sparseRewardDefault` (default value only) | 1 = sparse reward (Experiment B) |
| `early_endings` | 1 | `earlyEndingsOverride` (1 / 0) | stuck and no-progress endings on / off |
| `procedural_layout` | 0 | `proceduralOverride` (0-1) | share of episodes on a generated car park |
| `procedural_difficulty` | 2 | none | generator level 0 / 1 / 2 (`05-Generated-Car-Parks.md`) |
| `hard_case_share` | 0 | none | share of generated-car-park episodes that are hard cases |
| `geometry_share` | 1 | none | share of training-car-park episodes randomised when geometry randomisation is on |
| `stuck_steps_near_bay` | 0 (off) | none | stuck limit near the bay, in physics steps (`06-Agent.md`) |
| `occupancy_randomization` | 0 | `occupancyRandomOverride` (1 / 0) | many-parked-cars mode |
| `geometry_randomization` | 0 | `geometryRandomOverride` (1 / 0) | geometry randomisation |
| `far_start_share` | 0 | `farStartOverride` (0-1) | share of episodes with a far start (round 6) |
| `route_distance` | 0 | `routeDistanceOverride` (1 / 0) | 1 = the reward's distance term uses the route map (round 6) |
| `route_clearance` | 0.3 (kept >= 0) | none | extra clearance (m) around obstacles in the route map |
| `empty_bay_cost` | 3 (kept >= 1) | none | route cost factor inside bays other than the target |

Round 6 Inspector fields (not in the prefab, code defaults): `farStartOverride` -1 and `routeDistanceOverride` -1 (use
the trainer's value), `farStartRoute` 10-40 m, `showcaseStartShare` 0.15, `mapCellSize` 0.4 m, `carHalfWidth` 0.95 m,
and `routeMapInfo`, a read-only text updated at each reset (map summary, target, near or far start, straight and route
distance of the start).

Configs that use the round 6 parameters: `Config/parking_ppo_round6.yaml` (`far_start_share` 0.4, `route_distance` 1,
`route_clearance` 0.3, `empty_bay_cost` 3, with `procedural_layout` 0.5) and `Config/parking_ppo_full.yaml`
(`route_distance` 0 and `far_start_share` 0 until its last lesson G7, then 1 and 0.4).

## Geometry randomisation (`RandomiseGeometry`, 568-588)

For real car parks that are not built exactly like the training one:
- Target bay: random width 2.8-3.7 m and length 4.6-5.4 m. The open end stays where it is, so a longer bay
  extends further back (as in the Assist app, where photos give the bay's mouth).
- Every parked car (occupied bays only): moved sideways up to +/-0.25 m, deeper by -0.3 to +1.0 m, turned up to
  +/-3°, width scaled 0.9-1.05 and length 0.9-1.1.

`RestoreGeometry` (549-561) undoes all of it at the next reset, from the base recorded by `InitialiseSpots`.

Geometry drift bug (fixed). `InitialiseSpots` records the current geometry as the base, and
it runs whenever the car park switches (`ReplaceSpots`). Before the fix, a switch happened before the previous
episode's randomisation was undone, so each switch locked one random change into the base and the changes
accumulated over training. `ChooseLayout` now calls `RestoreGeometry` first (line 245). `BaseGeometryDrift()`
(281-293) measures the parked cars' recorded base against a clean car park. It was logged as `Parking/Debug/BaseDrift*`
(always 0 after the fix) until 2026-10-17, when the statistic was removed (`06-Agent.md`). The method is still
there. Training of rounds 3 and 4 ran with the bug. Their evaluations did not (the test sets never combine layout
switching with geometry randomisation).

## Reproducibility

- Each area has its own `System.Random` (`randomSeed` -1 = random). `Reseed(seed)` restarts the sequence and
  increments `SeedEpoch`. The evaluation runner uses it to give every model identical test layouts (`08-Evaluation.md`).
- Several features are written to draw no random numbers when they are off (`ChooseLayout` line 240, the hard-case
  draw line 374, the geometry-share draw line 402, the close-start draw line 420, the far-start draw line 421, and one
  occupancy draw whichever range is used, lines 384-387). So adding a feature does not change the layouts of earlier
  evaluation sets as long as it stays at its default.
- Round 6: building the route map draws nothing. The far-start draw happens only when `far_start_share` > 0, and
  `PlaceFar` (showcase-pose share, `SampleAisleCell`, heading) then takes its draws from the same per-area generator.
  With far starts off and `route_distance` on, the layouts are the same as without the route map.

## Scenario mode (app, scenario tests, showcase)

`ActiveScenario` (a `Scenario`, 500-509): exact target index, exact occupied bays, and either an exact start pose
(`startLocalPosition`, `startYaw` in the area's frame) or a seeded spawn from a stage's rules. When set, every reset
reproduces it (`ApplyScenario`, 520-547). No layout switching or geometry randomisation happens in this mode.

Round 6 in scenario mode: the route map is built only when `UseRouteDistance` is on (else it is marked unused), so the
reward matches training. With an exact start pose, `FarStart` is set when the start is more than 15 m from the bay
centre (straight line), so the agent gets the longer far-start time limit, e.g. for the showcase's centre hand-over.
`SpawnRoute` and `routeMapInfo` are not updated in this mode.

`HoldReset` (518): when true, the agent skips its automatic reset after an episode, so the final pose stays visible
(app and showcase).

## Other members

| Member | Purpose |
|---|---|
| `ReplaceSpots(list)` | swap the area's bays at runtime (generated car parks, the Assist app's photo layouts). Re-initialises parked cars, outlines, neighbours and base geometry |
| `IsOutOfBounds(p)` | outside the area (no margin) or more than 2 m below it: ends an episode as `OutOfBounds` |
| `IsInsideArea(p, margin)`, `Overlaps(...)`, `IsObstacle(c)` | geometry checks. Obstacles are colliders tagged `Wall` or `ObstacleCar` (generated pillars and row walls are tagged `Wall`) |
| `LayoutDescription` | "training car park" or the generator's description |
| `Target`, `CurrentStage`, `IsProceduralLayout`, `HardCase`, `TargetOccupiedNeighbours`, ... | read by the agent, statistics and runners |
| `RewardDistance(p)` | distance to the target used by the reward (route or straight line, see above) |
| `FarStart`, `FarStartShare`, `UseRouteDistance`, `RouteClearance`, `EmptyBayCost`, `SpawnRoute` | round 6 parameters and state of this episode. The agent reads `FarStart` (time limit, statistics, episode record) and `SpawnRoute` (episode record) |
| `routeMapInfo` | read-only Inspector text about the route map and this episode's start |
| `OnDrawGizmosSelected` | with the area selected in Play mode and the route map built, draws it in the Scene view: blocked cells red, reachable cells green (near) to blue (40 m), unreachable free cells grey |
| `obstacleMaterial` | the first wall's material, reused for generated pillars and walls |
