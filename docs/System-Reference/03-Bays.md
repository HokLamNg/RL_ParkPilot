# 03 Parking bays (`ParkingSpot`, bay layout, parked cars)

Sources: `Assets/Project/Scripts/ParkingSpot.cs` (237 lines), `Assets/Project/Prefabs/ObstacleCar.prefab`, values serialized in `TrainingArea.prefab`.

## Purpose

A `ParkingSpot` is one bay: a rectangle on the ground with a direction, an occupied / free / target state, an optional
parked car and an optional coloured outline. All parking logic (target choice, the parked check, observations,
rewards) works through this class.

## Geometry and conventions

- The GameObject sits at the bay centre on the ground (`Center = transform.position`, line 65).
- Its forward axis (`Forward`, +Z) points from the aisle INTO the bay, the way a car faces after parking nose-in
  (class comment, lines 21-29). So the bay's open end is at `Center - Forward x length/2`.
- `size` = (width along local X, length along local Z) in metres. Code default 2.8 x 5.5, but every bay in the
  training car park is serialized as 3.2 x 4.88 m.
- Local frame used throughout the project ("bay frame"): x = sideways across the bay (+ = right when facing into the
  bay), z = depth along the bay (+ = further in, 0 = bay centre).

Geometry helpers (all work on the ground plane, in the bay frame):

| Method | Returns | Line |
|---|---|---|
| `ContainsPoint(p, margin)` | true if `p` lies inside the rectangle enlarged by `margin` | 166-171 |
| `OutsideDistance(p, margin)` | distance from `p` to that rectangle, 0 inside | 177-183 |
| `HeadingError(dir, allowBoth)` | angle between `dir` and the bay axis, 0-180°. With `allowBoth`, facing out counts too, so 0-90° | 189-194 |
| `Resize(size, localPos)` | changes size and position at runtime (geometry randomisation, generated car parks) and redraws the outline | 144-149 |

"Parked" is decided geometrically from the car's footprint corners (see `06-Agent.md`), not with trigger colliders.
The class comment explains why: triggers get hit by the ray sensors and are unreliable at high time scales.

## State

- `SetState(occupied, isTarget)` (157-163): stores the flags, shows or hides the parked car, recolours the outline.
- `IsOccupied`, `IsTarget`: read by the environment, agent and showcase.
- The target is never occupied: the environment always sets the target with `occupied = false`.

## Parked cars (obstacles)

- Each bay has a `parkedCar` object, enabled when the bay is occupied. In the training car park none is assigned in
  the prefab (all 30 are empty), so `ParkingEnvironment` creates one from `ObstacleCar.prefab` at the bay centre when
  it initialises the bays (`04-Environment.md`).
- `ObstacleCar.prefab`: a cube scaled 1.9 x 1.5 x 4.4 m, tag `ObstacleCar`, a box collider, no Rigidbody
  (a static obstacle). The ray sensors detect the `ObstacleCar` tag.
- Known quirk: parked cars are half sunk into the ground. The prefab's root is raised 0.75 m, but
  `InitialiseSpots` sets the spawned car's `localPosition` to zero (`ParkingEnvironment.cs:302`). The bays sit at local
  y 0, level with the ground, so each parked car is a 1.5 m tall box centred on the ground: 0.75 m above it and
  0.75 m below it. This has been the case in all training and evaluation, so every model learnt with it, and it is
  deliberately left unchanged. The cars still block the car (box collider) and are seen by the ray sensors.
- Geometry randomisation moves, turns and resizes parked cars a little per episode (`04-Environment.md`).

## Outline (indicator)

- If no `indicator` is assigned and `autoCreateIndicator` is on, `EnsureIndicator` (101-126) creates a looped
  `LineRenderer` outline, 0.12 m wide, 0.15 m above the bay, inset by half the line width. Line renderers have no
  collider, so neither physics nor the ray sensors see them.
- Colours (`UpdateIndicator`, 205-214): green = free, red = occupied, yellow = target. `SetIndicatorOverride(colour)`
  (showcase) forces a colour, e.g. green / red for the result. `null` restores the normal colours.
- `SetIndicatorVisible(false)` hides it (the showcase hides all outlines before the CCTV photo so the photo cannot
  reveal occupancy).
- `OnDrawGizmos` draws the rectangle and an arrow in the "parked" direction in the Scene view.

## Bay groups (`BayGroup`)

Derived from the bay's name (`GroupFromName`, 82-88):

| Group | Rule | Training car park bays |
|---|---|---|
| `Sideways` | name contains `_Mid` | MidN_1-3, MidS_1-3 (6) |
| `Corner` | name contains `_N1_` or `_S1_` and ends in `_W1` / `_E1` | N1_W1, N1_E1, S1_W1, S1_E1 (4) |
| `Outer` | other `_N1_` / `_S1_` bays | N1_W2, W3, E3, E2 and the same in S1 (8) |
| `Inner` | everything else | N2_* and S2_* (12) |

Uses: which bays a curriculum stage may target (`targetGroups`, `ParkingEnvironment.cs:24`, and `RandomTargetIndex`, `:610-621`, group test at `:614`), statistics per
group (`Parking/ByGroup/...`, `ParkingAgent.cs:672`) and the `group` column of evaluation results. Generated bays are
named `Bay_P00`, `Bay_P01`, ... (see `05-Generated-Car-Parks.md`), so they all count as `Inner`.

## Layout facts for generated bays

Four fields are set by `ProceduralLot` for generated bays only and stay at their defaults elsewhere (36-43):
`layoutAisleWidth` (m, -1 = not generated), `layoutPillarBeside`, `layoutWallBehind`, `layoutRowEnd`. They are
`NonSerialized` and used for statistics and evaluation columns, never as observations.

## The training car park's 30 bays

The bays sit on the painted lines of the asset's `Centre Plane`, as fractions of the lot size
`2 x areaHalfExtents.x` (= 40 m): column centres 0.0713 / 0.1507 / 0.2301, row centres 0.080 / 0.354 (mirrored for the
other side), bay size 0.080 x 0.122 of the lot (= 3.2 x 4.88 m). They are stored in `env.spots`, with `fixedTargetIndex = 8`.

Positions in the area's local frame (x east, z north, metres), from the prefab:

| Row (facing) | Bays (x) | z |
|---|---|---|
| N1 (facing +Z, north) | W1 -17.15, W2 -13.97, W3 -10.80, E3 10.80, E2 13.97, E1 17.15 | 16.80 |
| N2 (facing +Z) | same x | 5.84 |
| S2 (facing -Z, south) | same x | -5.84 |
| S1 (facing -Z) | same x | -16.80 |
| MidN (facing +X, east) | x 0 | 17.15, 13.97, 10.80 (MidN_1, 2, 3) |
| MidS (facing +X) | x 0 | -17.15, -13.97, -10.80 (MidS_1, 2, 3) |

The order of `env.spots` (the index used everywhere, e.g. "bay 8") is: N1 W1, W2, W3, E3, E2, E1 (0-5), N2 (6-11),
S2 (12-17), S1 (18-23), MidN_1-3 (24-26), MidS_3-1 (27-29). Index 8 is Bay_N2_W3, the fixed target of the early
curriculum stages.

Consequences of this layout that other parts rely on:
- The N2/S2 rows open onto the central aisle along z = 0. The N1/S1 rows open onto aisles centred at z of about
  +/-10.95 (bay mouth at |z| = 14.36, aisle centre 3.4 m in front of it).
- The sideways bays open west (their mouths at x = -2.44) onto the north-south corridor centred at x of about -5.85.
- These are the "aisles" and "corridors" the showcase's scripted drive-in uses (`12-Showcase.md`).
