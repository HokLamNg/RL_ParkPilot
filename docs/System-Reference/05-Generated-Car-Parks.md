# 05 Generated car parks (`ProceduralLot`)

Source: `Assets/Project/Scripts/ProceduralLot.cs` (223 lines). Used by `ParkingEnvironment.ChooseLayout`
(`04-Environment.md`).

## Purpose

Real car parks differ in aisle width, bay size, pillars and walls. The generator lays out a new random car park
inside the training area's boundary walls for an episode, so the agent cannot rely on one car park's proportions
(class comment, lines 6-17). It is used in training (rounds 3-6 and the full run, environment parameter `procedural_layout`,
e.g. 0.5 with difficulty 2 in `Config/parking_ppo_round6.yaml`) and in the "generated car parks" evaluation set.

## When it is used

`ParkingEnvironment.ChooseLayout` (`ParkingEnvironment.cs:238-265`) picks a generated car park with probability
`ProceduralShare` per episode:
- switching to generated: the training car park's 30 bays are freed and hidden, the generator's root is shown, a
  new layout is generated and becomes the area's bay list (`ReplaceSpots`)
- switching back: generated bays are freed, the root hidden, the 30 original bays restored
- generated to generated: a fresh layout each episode.

The area's own walls (+/-20 m) and ground stay in every case. The training car park's painted lines stay visible on
the ground. They are artwork only and invisible to the sensors.

## Settings (`Settings`, `Default`, lines 20-37)

| Setting | Default range | Meaning |
|---|---|---|
| `bayWidth` | 2.9-3.6 m | one width per car park |
| `bayLength` | 4.6-5.4 m | one length per car park |
| `aisleWidth` | 6.0-11.0 m | per pair of facing rows, open end to open end |
| `pillarChance` | 0.5 | per car park |
| `wallBetweenChance` | 0.4 | per back-to-back row pair |
| `crossAisleChance` | 0.5 | per car park |
| `pillarSize` | 0.4-0.7 m | square cross-section, 1.5 m high |
| `pillarSetback` | 0.2-1.2 m | distance from the bay mouth towards the back of the bay |

Measurements are simulation metres. The comment notes that the Assist app scales real car parks so bays are about
3.2 m wide, so a 6-11 m aisle corresponds to about 4.7-8.6 m in reality.

### Difficulty levels (`ForDifficulty`, lines 43-58, environment parameter `procedural_difficulty`)

| Level | Aisles | Pillars | Walls between rows | Used in |
|---|---|---|---|---|
| 0 | 8-11 m | none | none | round 4 and 5 first lesson |
| 1 | 7-11 m | none | as default (0.4) | second lesson |
| 2 (default) | 6-11 m | as default (0.5) | as default | later lessons and all evaluation |

## Layout algorithm (`Generate`, lines 86-178)

Local frame of the area: x east, z north, metres. `Edge` = 19.75 m (the boundary walls' inner face).

1. Draw the car park's bay width `w` and length `l`, whether it has pillars, the pillar spacing (every 2 or 3 bays),
   pillar size and setback.
2. Columns (along x): margin `xEdge` 0.3-2.0 m at both ends. With probability `crossAisleChance`, a cross aisle
   6-9 m wide centred at x in -5..+5 splits each row into two segments.
3. Rows (along z), from the south wall up (lines 107-133):
   - first row's back at z = -19.75 + (0.2-1.2), open end facing +z (north)
   - an aisle of random width, then a row facing -z (south) across it
   - that row is back-to-back with the next +z-facing row, optionally with a 0.2 m wall between them, full width,
     1.5 m high. The wall is only placed if there is room for it (z + 0.4 < `Edge`, line 126)
   - repeat until the space runs out. If there is no room for an opposite row, the last row faces open space up to
     the north wall (its recorded aisle width is that space). The loop stops when the remaining room is less than the
     narrowest aisle.
   The number of rows follows from the drawn sizes. A Python replica of the generator over 2,000 layouts gave
   3-4 rows, about 30-48 bays, always inside the walls.
4. Bays (lines 135-167): in each row and segment, `n = floor(segment length / w)` bays, centred in the segment.
   Each bay's forward axis points from its open end into the bay (`yaw` 180° for +z-facing rows, 0° for -z-facing).
5. Pillars: on the bay lines between bays, at line k = pillarEvery, 2 x pillarEvery, ... (never at a row's two
   ends), set back `pillarSetback` from the bay mouth towards the back of the bay. So a pillar stands between two
   bays, just inside their mouths.
6. Unused pooled objects are hidden. `Description` summarises the layout (e.g. "4 rows, 2 aisles, bay 3.12 x 5.03 m,
   38 bays, pillars every 3, 1 wall(s), cross aisle").

### Layout facts recorded per bay (lines 151-156)

Set on each generated `ParkingSpot` for statistics and evaluation columns (never used as observations):
- `layoutAisleWidth`: the aisle in front of the bay
- `layoutWallBehind`: a row wall behind the bay
- `layoutRowEnd`: first or last bay of its segment
- `layoutPillarBeside`: a pillar on one of the bay's two side lines.

These are computed from the same draws as the layout, so recording them draws no extra random numbers: evaluation
layouts stay identical to runs made before they existed.

## Obstacles and pooling

- Bays are pooled `ParkingSpot` objects named `Bay_P00`, `Bay_P01`, ... (so their `Group` is `Inner`). The environment
  gives each one a parked car (from `ObstacleCar.prefab`) and an outline the first time it is used.
- Pillars and row walls are pooled Unity cubes tagged `Wall`, using the boundary walls' material. Their height
  (1.5 m) matches the boundary walls, so the ray sensors see them the same way. A pillar is therefore reported to the
  agent as a "wall". Nothing tells it apart from the boundary walls.
- Nothing is created or destroyed after the pools have grown to their largest size.

## Where the start pose and parked cars come from

The generator only lays out bays and obstacles. Target choice, parked cars (0-90% of the bays with occupancy
randomisation), hard cases and the car's start pose are done by `ParkingEnvironment` with the same rules as on the
training car park: the car starts in front of the target bay, and a pose is rejected near the walls or overlapping
an obstacle (`04-Environment.md`). With geometry randomisation on, generated car parks are always randomised as well.

Round 6: with `far_start_share` > 0, generated car parks also get far starts in their aisles (a free aisle cell
10-40 m from the target along the route). The route map is built over the generated layout, its pillars, row walls
and parked cars included. The showcase hand-over pose is used on the training car park only, never on a generated one.
