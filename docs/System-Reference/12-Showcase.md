# 12 Showcase (the driver-facing demo in Unity)

Sources: `Assets/Project/Scripts/ShowcaseController.cs` (logic), `ShowcaseUI.cs` (interface logic and model choice, same
partial class), `ShowcaseUIView.cs` and `Assets/Project/UI/ShowcaseUI.prefab` (interface objects, since 2026-10-11), the saved `Assets/Project/Scenes/Showcase.unity`,
`Scenarios/showcase/`. All checked on 2026-10-17.

## Purpose

A complete, watchable demonstration of the product in the asset's town: a car arrives at a car park, a camera photo is
classified by the occupancy CNN, the driver picks a free bay, the car drives in, and the trained RL policy parks.
It is also a test bench (batch test, run logs, debug overlay).

What is learned and what is scripted:

| Part | How it works |
|---|---|
| Which bays are free | Learned: occupancy CNN on the CCTV photo (`10-Occupancy-CNN.md`) |
| Driving from the town to near the bay | Scripted: a hand-made route for this car park, followed by a simple pure-pursuit controller. Not RL, no obstacle avoidance |
| Parking from the hand-over pose into the bay, and deciding when it is parked | Learned: the RL policy, PPO `full3_s1` by default or the DQN `rdqn2_s2` if chosen in Settings (below), the same agent as in training and evaluation. The episode ends by the environment's normal parked check (`06-Agent.md`) |

## How the scene is set up

`Assets/Project/Scenes/Showcase.unity` and the `.meta` files of the showcase scripts and models are in git, so a clone
opens the same scene. It is made of:

1. The town, roads, sky and light of the asset's `Assets/Parking Env/Scenes/SampleScene.unity`. Every root object
   except the camera and light is grouped under `Town` (the first camera is the player view, any further camera is
   switched off).
2. One `TrainingArea` prefab at the origin as `ShowcaseArea` (the training car park with its 30 bays and the agent),
   with a 7 m entrance gap in the west wall at z = 0 (the central aisle) and the wall renderers hidden. The wall
   colliders stay: the agent was trained with them and its rays still see them.
3. The town is scaled so its car park plane matches the training one, its roads are level with the car park floor,
   town renderers inside the car park (its own bay lines) are hidden and every town collider is off. The agent's world
   is exactly the training car park.
4. `StartPoint` on a town road west of the car park, two `ApproachPoint`s along the roads to the west wall, and
   `EntrancePoint` 4 m inside the gap (x = -16, z = 0), facing east.
5. The `CCTV Camera` at (0, 30, -32) looking north over the car park, field of view 55, disabled (it renders only on
   capture).
6. Player view: SampleScene's camera, tagged `MainCamera`, placed 9 m behind and 5 m above the start. The controller
   moves it at run time.
7. The `ShowcaseController` with the policy `ExpFull/full3_s1.onnx` (was `ExpR6/round6_s1.onnx` until 2026-10-17), the
   alternative policy `ExpR6/rdqn2_s2.onnx` (the round 6b DQN) and the CNN `Vision/occupancy_cnn.onnx`, and the start,
   entrance and approach points and both cameras assigned.
8. The interface: an instance of `ShowcaseUI.prefab`, assigned to `ShowcaseController.ui`, and an EventSystem with the
   Input System UI module.

## Flow and phases

`Phase`: `Menu -> Arrive -> Photo -> Driving -> Parking -> Done`, plus `Generating` (synthetic CNN data).

| Phase | What happens | Camera |
|---|---|---|
| Menu | Title, Start parking, How it works, Settings, Exit. A new random car park is prepared. The footer shows the active parking policy (algorithm and model name) and the free-bay detection | orbits the car park (55 m out, 32 m up, 6 degrees/s) |
| Arrive | The car waits at `StartPoint` in town. Card "Find a parking space" | chase camera, 9 m behind, 5 m up |
| Photo | Capture: the CCTV renders 1000 x 750, every fully visible bay is classified. The photo is shown with a dot per bay (green free, red taken). Hover shows the bay name and the detection confidence. Click a free bay, confirm | (photo on screen) |
| Driving | Scripted drive-in along the route (orange line), progress bar by distance | chase camera |
| Parking | The RL agent drives (blue line). Gauges and a parked checklist | elevated view of the bay (12 m in front, 14 m up) |
| Done | Result screen. The bay outline turns green or red and the screen flashes | elevated view |

A new car park (`Restart`): every one of the 30 bays is occupied with probability `occupiedShare` (default 0.6, set 10
to 90% in Settings, applied at the next new car park), bay outlines hidden so the photo does not give occupancy away.

## Step details

### Capture and choice

- `RenderPhotoAndQuads`: renders the photo, projects each bay's four ground corners into it. A bay is visible only if
  all four corners are in the image (in practice all 30: 9,720 visible-bay calls in the 324 runs of `results.csv` on
  2026-10-17).
- `ClassifyBays`: the CNN (or scene truth if no CNN is assigned). Details in `10-Occupancy-CNN.md`.
- The tooltip says "Photo detection is X% confident this bay is free/taken": the CNN's softmax for its call, not a
  parking success rate.
- `RejectReason`: a bay the CNN calls taken cannot be chosen ("That bay is taken. Choose a green bay."). A taken bay
  that the CNN calls free can be chosen. The agent then gets a target with a car in it (no such case in the Showcase run
  log so far: 0 false-free calls).
- Every choice records a CNN check against the scene's truth (`CnnCheck`) for the run log.

### Scripted drive-in (`StartDrive`, `RoundCorners`, `DriveStep`)

The route is built in the car park's local frame and is specific to this car park's layout: entrance gap at the
west wall at z = 0, central aisle along z = 0, north-south corridors at x = -5.8 and +5.8 (between the side bays and
the middle column).

Route by bay type (`handoverAtCentre` off, the default):
- Bay on the central aisle (aisle centre-line within 4 m of z = 0, rows N2 and S2): entrance, then along z = 0 to
  8 m (`minHandoverLateral` + 3) past the bay in the driving direction.
- Bay on the north or south aisle (outer rows N1 and S1): entrance, along
  z = 0 to the corridor on the bay's side (x = -5.8 or +5.8), up the corridor to the bay's aisle centre-line (3.4 m in
  front of the bay's open end), then along the aisle to 8 m past the bay.
- Sideways bay (the middle column, `MidN` / `MidS`, opening east or west): along z = 0 to the corridor in front of
  its open end, then along that corridor to 8 m past the bay (north or south).

Before the hand-over the route always starts at the car's position (and goes through the approach points unless the car
starts at the entrance). Every corner is replaced by a circular arc of radius 5 m (`cornerRadius`, smaller if the legs
are short, the car's tightest turn is about 3.7 m). Points on a straight line are dropped first.

`DriveStep` (every physics step, kinematic: `CarController.MoveExternally`, the car's own dynamics are paused):
- pure pursuit: aim at the first route point more than 3 m away
- target speed `driveSpeed` 4 m/s, reduced up to 65% for sharp aim angles, easing to a stop at the end of the route,
  acceleration 3 m/s^2
- yaw rate limited by a bicycle model with the car's wheelbase 2.6 m and maximum steering 35 degrees
- the displayed wheel angle is derived from the yaw rate.

Hand-over rules (aisle mode). The RL agent takes over at the first step where all hold:
1. the car is inside the car park (2.5 m margin)
2. it is on the last leg of the route
3. it is lined up: heading within `handoverMaxAngle` 20 degrees of the last leg
4. it is at least `minHandoverLateral` 5 m to the side of the bay (in the bay's own frame)
5. it is within `handoverDistance` 10 m of the bay centre.

On the aisle centre-line (about 5.8 m in front of the bay centre) rules 4 and 5 together mean a hand-over 5 to about
8 m to the side. If the car enters the aisle closer than 5 m to the bay, it drives past and hands over 5 m beyond it,
facing away (the agent then turns or reverses). If the conditions never hold, it hands over at the end of the route.
Reasons: a car still turning in from a corridor gave the agent a diagonal start against the row, and a
start 3.6 m beside a bay led to swinging straight in without room (2 collisions in 3 runs). Tighter angle limits (10 and
15 degrees) were tested in simulation and broke the W2 and E2 bays, so 20 degrees stays.

Centre mode (`handoverAtCentre`): the route ends at the car park centre (0, 0) and the agent takes over there.
Far bays then start 15-24 m away, often behind the car. `round5_s1` was never trained on such starts and parked 22/30
from the centre (batch 1014-184515). The round 6 models were trained with far starts and park 30/30 from the
centre, both PPO `round6_s1` and DQN `rdqn2_s2`. The adopted `full3_s1` parks 30/30 from both the centre and
the aisle (batches 1017-164221 and 1017-164838). The aisle hand-over stays the default because it is the
shorter drive. Centre mode is offered in Settings and used automatically by "Try again" after a failure (below).

### RL parking (`HandOver`, `OnEpisodeFinished`)

- The car is released to physics. A `ParkingEnvironment.Scenario` is set with the target, the car's current pose as the
  start and every occupied bay, then `EndEpisode()` starts an episode that rebuilds exactly this situation
  (`04-Environment.md`, scenario mode). The policy runs deterministically in inference mode.
- The episode ends by the environment's own rules: parked (held for 25 steps), collision, stuck, no progress, timeout
  or out of bounds. The time limit is 1500 steps (30 s), or 3000 steps (60 s) when the hand-over pose is more than
  15 m from the bay centre, which happens for far bays with the centre hand-over (`04-Environment.md`, scenario mode).
- At the end the agent is paused, the reset is held and the car is made kinematic again, so it cannot roll on with the
  policy's last action. The 0-step episode end that ML-Agents reports for its own first episode is ignored.

### Model selection (PPO or DQN, since round 6)

- `ShowcaseController.model` is the main policy, `alternativeModel` a second policy the driver can choose in Settings
  (empty = no choice). In `Showcase.unity`: `model` = `ExpFull/full3_s1.onnx` (PPO, was `ExpR6/round6_s1.onnx` until 2026-10-17),
  `alternativeModel` = `ExpR6/rdqn2_s2.onnx` (the round 6b DQN, `13-DQN-Baseline.md`).
- `ActiveModel` (`ShowcaseUI.cs`): the alternative model if the driver chose it (`settingAlternative`) and it is
  assigned, otherwise `model`.
- Inputs must match. `Awake` sets the agent's optional inputs (bay size, near rays) from `model`
  (`ParkingAgent.ConfigureFor`). `InitUI` compares `ParkingAgent.ModelInputSizes` of both models. If they differ, it
  logs an error and clears `alternativeModel` for the session, so the Settings row is disabled. Both current models have
  68 + 11 + 124 inputs.
- `ApplyModel` switches the agent with `agent.SetModel` (deterministic inference), only when the model changes. It is
  called in `ParkIn` (every confirmed bay and every Try again) and at the start of a batch test. A change in Settings
  therefore applies to the next parking, never to one that is running.
- The choice is reset to PPO at every start (`InitUI` sets `settingAlternative` to false).
- Algorithm label (`Algorithm`): "DQN" if the model name contains "dqn", otherwise "PPO". It is shown in the menu
  footer ("Parking policy: PPO (full3_s1)") and the Settings button. The debug panel shows the active model's name, and
  the `policy` column of `results.csv` and the first line of a batch summary record it.

### Result screen

- Parked: time, distance off centre (cm), angle, and a rating: Excellent (< 0.2 m and < 2 degrees), Good (< 0.35 m
  and < 5 degrees), otherwise Acceptable.
- Failed: the reason in plain words (collision with a parked car, pillar, wall or the car park wall, stopped before
  fully in the bay, stopped getting closer, took too long, left the area) and a tip.
- Buttons: Park again / Try again, another approach (T), Choose another bay (C, back to the same photo), New
  car park (R), Main menu (M).
- Try again puts the car at the entrance gap (x = -22, z = 0, facing east), waits 1.2 s so the restart is
  visible, and drives in again. The policy is deterministic, so after a failure it switches the hand-over mode
  (aisle <-> centre) to make the attempt different. After a success it keeps the mode.

### Live parking gauges

| Gauge | Source |
|---|---|
| Gear D / R / N | The sign of the car's speed (> 0.05 m/s forward = D, < -0.05 m/s = R, else N). The car has no gearbox: the policy's throttle sign drives forwards or backwards. It is a direction indicator |
| Speed | `CarController.Speed` in km/h |
| Steering | wheel angle as a share of the 35 degree maximum |
| To the bay, Angle | distance to the bay centre and heading error (`ParkingAgent.GetTrace`). Angle turns green when within the parked tolerance |
| Checklist | corners in bay n/4, straight, stopped, and "Holding position" (the 25-step hold) |

## Keys

| Where | Keys |
|---|---|
| Menu | Enter start, H help, Esc exit (confirm) |
| Arrive | C capture, R new car park, B batch test, G generate CNN data (not shown in the UI), D debug overlay, H help, Esc pause menu |
| Photo | C retake photo, Esc back |
| Driving, Parking | X cancel, Esc pause (time stops) |
| Done | T try again, C choose another bay, R new car park, M main menu, Esc pause |
| Dialogs | Enter confirms (park here, exit), Esc closes, X in the pause menu cancels a running parking |
| Batch running | B or Esc stops after the current run, D debug overlay |

## Settings (menu or pause)

- Assistant takes over: in the bay's aisle (default) or at the car park centre. Applied when a bay is confirmed.
- How full the car park is: 10-90% in steps of 10, applied at the next new car park.
- Sensor view: the debug overlay (also key D).
- Parking model: toggles `settingAlternative` between `model` and `alternativeModel`. The button shows the
  algorithm and the model name, "PPO  (full3_s1)" or "DQN  (rdqn2_s2)". It is disabled when no alternative model is
  assigned or its input sizes do not match `model`. Applied at the next parking (`ApplyModel`).
- Developer: Run batch test (only from the menu or the Arrive step).

The scene file has `handoverAtCentre` off, and `InitUI` sets it (and the Settings choices for the hand-over and the
model) to false at start anyway, so the Inspector value has no effect. Use Settings.

## Built app (window and output files)

- Window (`MoveToLandscapeDisplay`, started in `Start`): the interface is laid out for landscape screens. In a built
  app whose window opens on a portrait monitor (for example a vertical second screen), the window is moved to the
  largest landscape monitor. It is skipped in the editor, when the app is started with `-monitor N` (an explicit
  choice), and when no landscape monitor exists.
- Output files (`ShowcaseController.OutputPath`): the run log (`Scenarios/showcase/`) and the synthetic CNN data
  (`Data/unity_patches/`) are written relative to the project folder in the editor, next to the `.exe` in a Windows
  build, and to `Application.persistentDataPath` in a macOS build (`~/Library/Application Support/<company>/<product>`,
  because the `.app` bundle is often read-only and writing into it breaks its signature).

## Developer tools

### Debug overlay (key D, `UpdateRays`, `DrawDebugPanel`)

- Re-casts both ray sensors of the agent (main rays and, for round 5, round 6 and full run models, the near rays) with the
  same input the policy gets and draws each ray: red = parked car, orange = wall, yellow = other tag, green = no hit.
- A panel (native resolution, right side): active model and decision period, steering and throttle commands, wheel
  angle and speed, the first 9 vector observations with names (`ObservationNames`), nearest car and wall hit, the
  parked check and hold progress, reward (cumulative, last step, potential and its parts), the episode-ending counters
  (steps, no progress, not moved) and speed and reward history. The current models (full run PPO and round 6b DQN) have 68 main
  ray + 11 vector + 124 near-ray inputs. The 2 bay-size inputs (the last two vector inputs) are not shown.

### Run log (`logRuns`, `Scenarios/showcase/`)

- `results.csv`, one row per parking: run id, time, policy (the active model, PPO or DQN), hand-over mode, target index
  and name, bay group, occupied neighbours, occupied bays, hand-over pose (x, z, yaw, distance), outcome, collided with,
  seconds, final distance and heading, nose-in, approach, entries, soft contacts, reward, CNN check (`cnn_used`,
  `cnn_visible`, `cnn_correct`, `cnn_false_free`, `cnn_false_occupied`, `cnn_p_target`) and the batch id. A file with an
  older column layout is renamed `results_old_<time>.csv`.
- `runs/<run id>.csv`: one row per physics step of the parking (pose, speed, commands, the first 9 observations,
  the parked-check terms, reward, nearest car and wall ray).
- `runs/<run id>.png`: screenshot at the end.
- The folder is relative to the output location above (project folder in the editor).

### Batch test (key B or Settings)

`batchRuns` 30 parkings in a row at `batchTimeScale` 4 (physics per step unchanged), each with a new random car park,
a CCTV photo and the CNN, and a random bay the CNN calls free (any bay, including sideways bays). Runs that do not
finish within 180 s real time are skipped. It uses the hand-over chosen in Settings (`settingCentre`, set at the start
of the batch since 2026-10-11. Before, it used whatever Try again had last switched to) and the parking model chosen in
Settings (`ApplyModel` at the start of the batch). Summary: `Scenarios/showcase/batch_<id>.txt` (first line: parked
count, policy and hand-over, then success by bay group, failures by cause and bay, mean time, mean hand-over distance
for parked and failed runs).

| Batch | Policy | Hand-over | Parked |
|---|---|---|---|
| 1011-032702 | `finetune_occ_s3` | centre | 27/30 |
| 1011-033528 | `finetune_occ_s3` | aisle, no 5 m minimum | 27/30 |
| 1011-034721 | `finetune_occ_s3` | aisle, 5 m minimum | 24/30 |
| 1011-164400 | `round5_s1` | aisle, 5 m minimum | 30/30 |
| 1014-184515 | `round5_s1` | centre | 22/30 |
| 1015-175940 | `rdqn2_s2` (DQN) | aisle | 28/30, 5.0 s |
| 1015-180344 | `rdqn2_s2` (DQN) | centre | 30/30, 8.7 s |
| 1015-180807 | `round6_s1` (PPO) | centre | 30/30, 9.0 s |
| 1017-164221 | `full3_s1` (PPO, adopted) | centre | 30/30, 9.1 s |
| 1017-164838 | `full3_s1` (PPO, adopted) | aisle | 30/30, 4.9 s |

For `round5_s1` (aisle), 30/30 against 24/30 is significant (Fisher exact p = 0.024), against 27/30 it is not
(p = 0.24). The bay draws differ between batches. The two DQN failures in the aisle batch (`Bay_S2_E3`, `Bay_S2_E1`,
inner bays) both stopped inside the bay, straight, but 0.37-0.39 m from the centre and ended as stuck, the DQN's typical
failure. No PPO `round6_s1` aisle batch was run. The adopted `full3_s1` (since 2026-10-17) parked 30/30 in both hand-over modes, every car nose-in with one entry into the bay.

### Synthetic CNN data (key G)

See `10-Occupancy-CNN.md`: 200 rendered car park variations, every visible bay saved as a labelled 96 x 96 patch to
`Data/unity_patches/` (relative to the output location above).

## Interface implementation (scene objects, since 2026-10-11)

The interface is a uGUI prefab in the scene, `Assets/Project/UI/ShowcaseUI.prefab`, edited in the Scene view and
the Inspector. (Before 2026-10-11 it was drawn by code with `OnGUI`.) Only the developer debug overlay (key D) is
still drawn with `OnGUI` (`ShowcaseController.DrawDebugPanel`).

| Part | File | Role |
|---|---|---|
| Prefab | `Assets/Project/UI/ShowcaseUI.prefab` | Canvas (Screen Space Overlay, Canvas Scaler 1920 x 1080 matched on height) with one child per screen: Menu, Top bar, Arrive, Photo, Driving, Parking, Result, Busy, then Dim, the five dialogs and the Toast. The settings dialog has the "Parking model" row (`Model label`, `Button: Parking model`) |
| Sprites | `Assets/Project/UI/Sprites/*.png` | rounded panels (9-sliced, radius 14, 8, 4), circle, check and cross icons, menu gradient. Replace them with your own art if you like |
| View | `ShowcaseUIView.cs` | on the prefab root: colours (text, muted, accent, success, failure, warning, inactive, secondary button) and the references to every object the code fills in or switches. `SettingsDialog.model` (the Parking model button) is optional |
| Logic | `ShowcaseUI.cs` (partial `ShowcaseController`) | `UpdateUI` every frame (`LateUpdate`): which screen and dialog is shown, live values, photo dots, hover outline and tooltip, clicks on bays. `WireButtons` connects every button to its action. Keys (`HandleKeys`) are unchanged. Also the model choice (`ActiveModel`, `ApplyModel`) |

Editing the look:
- Open the prefab (double-click it, or edit the instance in `Showcase.unity` and apply). Screen roots are switched on
  and off by the game, so tick one to see it while editing (only Menu is on in the prefab).
- Move, resize, recolour, change fonts (the `Text` components use Unity's built-in font, any font asset can be
  assigned) and wording freely. Objects with "(code)" in their name get their text, colour, width or position from code.
- Objects referenced by `ShowcaseUIView` can be renamed (the reference stays). If you delete or replace one, assign the
  new one in the `ShowcaseUIView` component. A missing reference is skipped, it does not break the game.
- Button colours come from `ShowcaseUIView` (Accent for primary buttons, Secondary Button for the others). The
  result screen switches which of its two top buttons is primary.
- Bay dots are copies of Bay dot template (size, sprite), the hover outline uses Hover edge template and
  `hoverEdgeWidth`. Dots are placed relative to the Photo image, so it can be moved and resized.
- Running Create Showcase UI Prefab again replaces the prefab and loses your edits (it asks first).
- Update Showcase UI Prefab (add new controls) keeps your edits: it opens the existing prefab and, if the settings
  dialog has no `model` button yet, adds the "Parking model" row at 328 from the dialog's top, moves the top-anchored
  rows at or below it down by 76 and makes the dialog 76 taller. If the row is there already (or there is no settings
  dialog) it changes nothing.

Implementation notes:
- Buttons have keyboard navigation off and are deselected every frame, so Enter is only handled by `HandleKeys` (no
  double action through the UI module's submit).
- Mouse over the photo: screen point to the photo's local rectangle, then to photo pixels, then `BayAt`. The tooltip is
  placed beside the mouse and flipped near the screen edges.
- The pause dialog uses a vertical layout, so it shrinks when "Cancel this parking" is hidden.

## Known issues

- The drive-in only works for this car park (hard-coded aisle and corridor positions). A different layout needs a
  path planner.
- The drive-in has no collision checks. It relies on the aisles being free (parked cars never stand in aisles here).
- Fixed 2026-10-11: the Cancel message said "back at the entrance" (now "back at the start", the car returns to the
  town `StartPoint`), and the class header and drive-in status text described the old hand-over rule.
- A bay the CNN calls free but which is taken can be chosen (no ground-truth guard by design, the photo is the source).
- The CNN crop (a box lifted above the bay) was designed for bays whose open end faces the camera. Sideways bays are
  seen side-on, and their crop includes more of the neighbouring bays.
- Both models must have the same inputs: the agent's sensors are set up once from `model` in `Awake`, so an
  alternative model with other inputs is disabled rather than switched to.
