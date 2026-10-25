# System reference: plan and status

Purpose: a source-of-truth description of every part of ParkPilot for the team. Each document is written only after
reading the code it describes. Statements point to the code (`file:line` at the time of writing).

Rules used while writing:
- Read the whole source file(s) before writing. Quote defaults and constants from the code, not from memory.
- Where the code and an older document disagree, the code wins and the disagreement is noted.
- Mark anything not verifiable from code or logs as such.

## Documents

| # | Document | Covers (source files) | Last checked against the code |
|---|---|---|---|
| 0 | `00-Plan-and-Status.md` | this list | 2026-10-17 |
| 1 | `01-Overview.md` | system map, data flow, folders, conventions, glossary | 2026-10-17 |
| 2 | `02-Car.md` | `CarController.cs` | 2026-10-17 |
| 3 | `03-Bays.md` | `ParkingSpot.cs`, `ObstacleCar.prefab` | 2026-10-17 |
| 4 | `04-Environment.md` | `ParkingEnvironment.cs`, `DrivableMap.cs`, `TrainingArea.prefab` settings | 2026-10-17 |
| 5 | `05-Generated-Car-Parks.md` | `ProceduralLot.cs` | 2026-10-17 |
| 6 | `06-Agent.md` | `ParkingAgent.cs` (observations, actions, rewards, endings, statistics, switches) | 2026-10-17 |
| 7 | `07-Training.md` | `Config/*.yaml` (including `parking_ppo_full.yaml`), run procedure | 2026-10-17 |
| 8 | `08-Evaluation.md` | `EvaluationRunner.cs`, `Figures/` | 2026-10-17 |
| 9 | `09-Models.md` | `Assets/Project/Models/*` inventory and lineage, adopted models | 2026-10-17 |
| 10 | `10-Occupancy-CNN.md` | `Vision/occupancy_cnn.py`, `train_occupancy_cnn.py`, `finetune_occupancy_unity.py`, `export_occupancy_onnx.py`, CNN use in Unity | 2026-10-17 |
| 11 | `11-Assist-App.md` | `Vision/assist.py`, `Vision/geometry.py`, `ScenarioRunner.cs`, `Vision/layouts` | 2026-10-17 |
| 12 | `12-Showcase.md` | `ShowcaseController.cs`, `ShowcaseUI.cs`, `ShowcaseUIView.cs`, `Showcase.unity` | 2026-10-17 |
| 13 | `13-DQN-Baseline.md` | `DQN/*.py` (`train_dqn.py`, `rdqn.py`, `diagnose_rdqn.py` and the rest), `DQN/rdqn_curriculum.json`, `DQN/rdqn2_curriculum.json` | 2026-10-17 |

After the full run, the adoption of `full3_s1` and the statistics clean-up, every document was checked again on
2026-10-17. Code and document review updates on 2026-10-20, 2026-10-23 and 2026-10-25 corrected a few numbers and
statements.

## Open code to-dos

| # | Task | When | Status |
|---|---|---|---|
| T1 | Remove duplicated and constant TensorBoard statistics from `ParkingAgent.cs` (about 189 to 125 `Parking/...` cards) and describe the remaining cards in two groups in `06-Agent.md` (outcome monitoring, targeted diagnostics). Remove: the per-group diagnostic cards `Parking/ByGroup/<group>/` (`BestDepthOnFail`, `BestLateralOnFail`, `EntriesOnFail`, `MinHeadingAll4OnFail` and the four `FailReason` cards, 32 cards, duplicates of `Parking/Diag/*`), `Parking/ByNeighbours/*` (15, covered by `Parking/Situation/<lot>/Neighbours*`), `Parking/Approach/<kind>/EntrySpeed` (3, the same entry speed as `Parking/Finish/<kind>/EntrySpeed`), `Parking/CurriculumStage` (ML-Agents logs `Environment/Lesson Number/curriculum_stage`), `Parking/OutOfBoundsRate` (always 0, the boundary walls prevent it) and `Parking/Debug/BaseDrift*` (3, a one-time check of the round 5 fix). Logging only, so no model changes. Needs a rebuild of `Builds/ParkPilot`. | Not while a training run uses the build. Only when the project owner asks for it, not automatically when training ends. | done 2026-10-17 (189 to 117 cards in two steps). Needs the next build to take effect |
