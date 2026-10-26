# 02 Car (`CarController`)

Source: `Assets/Project/Scripts/CarController.cs` (180 lines). Values below are the ones serialized on the car in
`Assets/Project/Prefabs/TrainingArea.prefab` (they match the code defaults).

## Purpose

The car the agent drives, in training, evaluation, the app and the showcase. It is a kinematic bicycle model driven
through a Rigidbody: every physics step the script computes a speed and a yaw rate and writes them into the body's
velocity. There are no wheel colliders or tyre forces. The comment at `CarController.cs:5-10` gives the reason: it stays
stable at the high time scales used for training (time scale 20).

## Interface

| Member | Meaning | Line |
|---|---|---|
| `SetInputs(steer, throttle)` | the only control input. Both clamped to -1..1. Steer: + = right. Throttle: + = forward, - = backward | 87-91 |
| `Speed` | signed speed along the car's forward axis, m/s (+ forward, - reversing) | 53 |
| `MaxSpeed` | the larger of `maxForwardSpeed` and `maxReverseSpeed` (3 m/s) | 56 |
| `SteerAngle` | current front-wheel angle in degrees (+ = right) | 55 |
| `SteerInput`, `ThrottleInput` | last commands, read by the showcase overlay and logs | 84-85 |
| `Body` | the Rigidbody (the agent reads its velocity and yaw rate as observations) | 57 |
| `Teleport(pos, rot)` | moves transform and Rigidbody to a pose and stops the car (`ResetState`) | 104-110 |
| `ResetState()` | zeroes inputs, speed, wheel angle and (if not kinematic) the body's velocities | 94-101 |
| `SetExternalControl(on)`, `MoveExternally(...)`, `ExternallyControlled` | showcase only: a script drives the car as a kinematic body. The car's own dynamics are paused | 113-131 |

Who calls what (verified by search):
- `ParkingAgent` sets the inputs from the policy's actions (`ParkingAgent.cs:403`), and reads `Speed`, `SteerAngle`,
  `Body.linearVelocity` and `Body.angularVelocity` for observations, the parked check and diagnostics.
- `ParkingEnvironment` places the car at the start of every episode with `Teleport` (`ParkingEnvironment.cs:442` for a
  start in front of the bay in `PlaceCar`, `:479` for a far start in `PlaceFar`, and `:535` for a scenario start pose).
- `ShowcaseController` uses `SetExternalControl` / `MoveExternally` for the scripted drive-in and to hold the car after an
  episode, and `Teleport` to reset it.

## Dynamics (one physics step, `FixedUpdate`, `CarController.cs:133-171`)

Physics runs at 50 Hz (`ProjectSettings/TimeManager.asset`: Fixed Timestep 0.02 s).

1. Speed is re-read from the body: `speed = dot(velocity, forward)`. So a collision that slows the body slows the
   model too.
2. Steering slew: the wheel angle moves towards `steer x maxSteerAngle` at most `steerRate` degrees per second.
3. Longitudinal:
   - The dead zone used is `dz = max(throttleDeadzone, 0.01)` (line 145), so it is 0.15 with the prefab value.
   - `|throttle| > dz`: the throttle is rescaled so it starts from 0 at the edge of the dead zone,
     `t = sign x (|throttle| - dz) / (1 - dz)`. If the car is moving (> 0.05 m/s) and `t` points the other way, it is
     braking (`brakeDeceleration`), otherwise accelerating (`acceleration`). `speed += t x a x dt`.
   - `|throttle| <= dz` ("foot off the pedal"): the car brakes to a stop at `autoBrakeDeceleration`
     (6 m/s²). If `throttleDeadzone` were 0, it would coast at `coastDeceleration` instead (not used now). Even then,
     commands up to 0.01 would count as "foot off the pedal".
   - Speed is clamped to -`maxReverseSpeed` .. +`maxForwardSpeed`.
4. Yaw: `yawRate = speed / wheelBase x tan(steerAngle)` (rad/s).
5. Written into the body: velocity = forward x speed (vertical component kept, so gravity still acts), angular
   velocity = (0, yawRate, 0).

So the car is a bicycle model referenced at the body's centre, without side slip: it turns on a circle of radius
`wheelBase / tan(steerAngle)` around a point beside its centre.

### Parameters and what they mean in practice

| Parameter | Value | Consequence |
|---|---|---|
| `maxForwardSpeed`, `maxReverseSpeed` | 3 m/s each (10.8 km/h) | parking speeds. The forward limit was 5 until reward version v11 (tooltip, line 15-16): nose-in entries at 5 m/s overshot the bay |
| `acceleration` | 4 m/s² | full throttle: 0 to 3 m/s in 0.75 s |
| `brakeDeceleration` | 8 m/s² | full opposite throttle from 3 m/s: stops in about 0.4 s |
| `throttleDeadzone` | 0.15 | added in v10 so that "stop" is a wide, noise-tolerant action |
| `autoBrakeDeceleration` | 6 m/s² | inside the dead zone: from 3 m/s to rest in 0.5 s |
| `coastDeceleration` | 1.5 m/s² | only if the dead zone is 0 (not the case) |
| `wheelBase` | 2.6 m | |
| `maxSteerAngle` | 35° | tightest turning radius 2.6 / tan 35° = 3.7 m |
| `steerRate` | 150°/s | centre to full lock in 0.23 s, lock to lock in 0.47 s |

### Body (prefab, `CarAgent` object)

- Rigidbody: mass 1200, linear damping 0, angular damping 0.05, gravity on. Set in `Awake` (lines 59-81):
  interpolation on, continuous speculative collision detection, rotation about X and Z frozen (no roll or pitch).
- Box collider 1.9 x 1.4 x 4.4 m (width x height x length), centre 0.7 m up.
- All of the car's non-trigger colliders get a frictionless physics material (lines 66-77). The script sets
  velocity directly. With default friction (0.6) the ground would drag the car and it could barely move.

## External control (showcase only)

- `SetExternalControl(true)`: stops the car (velocities zeroed while still dynamic), then makes the body kinematic.
  `FixedUpdate` then does nothing (line 135).
- `MoveExternally(pos, rot, speed, steer)`: `MovePosition` / `MoveRotation` on the kinematic body, and stores the given
  speed and wheel angle so that the showcase gauges and the agent's observations stay meaningful.
- `SetExternalControl(false)`: body dynamic again, car stopped.
- `ResetState` skips the velocity writes on a kinematic body (line 98), because Unity does not accept velocities on
  kinematic bodies.

Training and evaluation never call these, so they do not affect results.

## Optional visuals

`frontLeftWheel` / `frontRightWheel`: if assigned, they are turned by the wheel angle (`UpdateWheelVisuals`). In the
prefab they are empty: the car is a box.

## Not modelled

Tyre slip, skidding, weight transfer, gears, engine response, rolling resistance beyond the dead-zone brake, and any
difference between front- and rear-axle turning centres. Collisions are handled by Unity physics on the box collider.
The "gear" shown in the showcase (D / R / N) is only the sign of `Speed` (see `12-Showcase.md`).
