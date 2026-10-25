using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkPilot
{
    /// <summary>
    /// ParkPilot Assist, Unity side: loads a parking situation written by the Python app (Vision/assist.py),
    /// lets the trained policy park in it, draws the path it drove and saves a top-down image plus a result file.
    ///
    /// Two kinds of scenario file (project folder / scenarioFile):
    /// * Layout (any car park): "bays" lists every bay in metres, as measured from a photo. The first TrainingArea is
    ///   rebuilt around them at runtime: bays, parked cars where "occupied" is true, floor and boundary walls. The layout
    ///   is scaled so its bays are as wide as the training bays.
    ///     { "bays": [ { "cx": 0, "cz": 0, "yaw": 0, "width": 2.5, "length": 5, "occupied": true }, ... ],
    ///       "target": 3, "start_mode": "aisle" | "facing" | "pose", "start_x": 0, "start_z": 0, "start_yaw": 0 }
    ///   Yaw: degrees, 0 = a car parked nose-in faces +z, positive turns towards +x (Unity convention).
    /// * Named (the training car park): "target" / "occupied" are bay names of the training scene, e.g. "Bay_N2_W2".
    ///
    /// Put it on an empty GameObject in the Training scene (disabled while training), assign the model and press Play.
    /// Keys: R = replay, L = reload the scenario file.
    /// Output (outputFolder): result.png (top-down view with the path) and result.json (outcome, time, path).
    /// </summary>
    [DefaultExecutionOrder(-1000)]   // Awake before the agents are enabled (their input size depends on the model)
    public class ScenarioRunner : MonoBehaviour
    {
        [Header("Policy")]
        [Tooltip("Trained .onnx: the adopted policy is Assets/Project/Models/ExpFull/full3_s1.onnx. Its inputs are detected automatically.")]
        public ModelAsset model;
        public bool deterministic = true;

        [Header("Files (relative to the project folder)")]
        public string scenarioFile = "Scenarios/scenario.json";
        public string outputFolder = "Scenarios";

        [Header("Layout scenarios")]
        [Tooltip("Bay width the policy was trained on (m). Layouts are scaled so their bay width matches it.")]
        public float trainingBayWidth = 3.2f;
        [Tooltip("Bay length the policy was trained on (m). A longer TARGET bay is trimmed to it, keeping its open end where " +
                 "the photo puts it, so it has the shape the agent learned (real bays are longer than the asset's).")]
        public float trainingBayLength = 4.88f;
        [Tooltip("A TARGET bay measured wider than the training bay width is narrowed to it, centred on the measured bay. " +
                 "Real bays are a standard width; the extra width is marking error, and a too-wide bay lets the agent stop " +
                 "inside it while crooked (camera 6: 11-15 deg).")]
        public bool clampTargetWidth = true;
        [Tooltip("Scale the layout so the target row's bay spacing (centre to centre) equals the training bay width. The " +
                 "agent judges position partly from the parked cars its rays see; at a wider spacing it lines up off-centre. " +
                 "Falls back to the reference bay width (metric_bay_width) when the target has no neighbour in its row.")]
        public bool scaleByBayPitch = true;
        [Tooltip("Park every car at the trained depth from its bay's open end (as in the 4.88 m training bays), like the " +
                 "trimmed target. With cars centred in longer real bays, the rays put the end of the bay about 0.9 m deeper " +
                 "than the target's own observations, and the agent entered too fast and overshot.")]
        public bool parkedCarsAtTrainedDepth = true;
        [Tooltip("Free space (m, after scaling) between the outermost bays and the boundary walls.")]
        public float boundaryMargin = 8f;
        [Tooltip("Distance (m, after scaling) from a bay's open end to the aisle start pose's line.")]
        public float aisleOffset = 3.4f;

        [Header("View")]
        public int imageSize = 1400;
        public float pathWidth = 0.25f;
        public Color pathColor = new Color(0.1f, 0.45f, 1f);
        public Color startColor = new Color(1f, 0.55f, 0f);
        [Tooltip("1 = real time, so the manoeuvre can be watched.")]
        public float timeScale = 1f;

        [Header("Batch test (key B): the same scenario from many starts")]
        [Tooltip("Sideways distances (m, simulation scale) of the aisle starts, tried on both sides of the bay.")]
        public float[] batchLateral = { 5f, 6f, 7f, 8f, 9f };
        [Tooltip("Offsets (m) added to the preferred aisle start's distance from the bays' open ends. Negative = closer to " +
                 "the target row. Positive values push the car towards the far row, which in narrow aisles leaves no room " +
                 "to swing (the first batch test crashed every such start within a second).")]
        public float[] batchStartOffsets = { 0f, -0.5f };
        [Tooltip("Starts facing the bay, this far (m) in front of its open end.")]
        public float[] batchFacing = { 2.5f, 3.5f, 4.5f, 5.5f };
        public float batchTimeScale = 20f;

        [System.Serializable]
        public class BayData
        {
            public string id = "";
            public float cx, cz, yaw, width = 2.5f, length = 5f;
            public bool occupied;
        }

        [System.Serializable]
        class ScenarioFile
        {
            // layout mode
            public BayData[] bays = new BayData[0];
            public int target_index = -1;
            public string start_mode = "aisle";
            public float metric_bay_width;   // real width of the reference bay (m); 0 = use the median bay width
            // named mode
            public string target = "";
            public string[] occupied = new string[0];
            public bool use_start_pose;
            public int spawn_stage = 10;
            public int spawn_seed = 1;
            // both
            public float start_x, start_z, start_yaw;
            public string source_image = "";
            public bool batch;               // run the batch test instead of a single manoeuvre
            // diagnostics (assist.py --no-trim / --back-barriers)
            public bool trim_target = true;  // false: the target bay keeps its full measured length
            public bool back_barriers;       // a 1 m barrier (tagged Wall) along the back edge of every bay
            public bool early_endings = true; // false: no stuck / no-progress endings, only the 30 s time limit
            public bool trace;               // write one CSV row per decision (Scenarios/trace.csv, traces/run_XX.csv)
        }

        ParkingEnvironment env;
        ParkingAgent agent;
        Camera topCam;
        LineRenderer path;
        GameObject startMarker;
        GameObject generated;            // floor + walls of a layout car park
        readonly List<Vector3> points = new List<Vector3>();
        bool running;
        string status = "";
        ScenarioFile loaded;
        string targetName = "";
        string aisleNote = "";
        int occupiedCount;
        // layout frame <-> area local: local = (layout * scale) - centre
        float layoutScale = 1f;
        Vector2 layoutCentre;
        bool layoutMode;
        // batch test
        readonly List<(Vector3 localPos, float localYaw, string label)> batchStarts = new List<(Vector3, float, string)>();
        readonly List<string> batchRows = new List<string>();
        readonly List<(bool parked, string outcome, float seconds, float dist, float heading)> batchResults =
            new List<(bool, string, float, float, float)>();
        int batchIndex = -1;
        bool batchNext, batchPendingStart;
        GameObject batchPaths;
        (Vector3 pos, float yaw) singleStart;
        // trace
        StringBuilder trace;
        Unity.MLAgents.Sensors.RayPerceptionSensorComponent3D rays;

        void Awake()
        {
            if (!Academy.Instance.IsCommunicatorOn) ParkingAgent.ConfigureFor(model);   // never override a training build's inputs
        }

        void Start()
        {
            if (Academy.Instance.IsCommunicatorOn)
            {
                Debug.LogWarning("[ScenarioRunner] A trainer is connected - scenario mode disabled.", this);
                enabled = false;
                return;
            }
            if (!model)
            {
                Debug.LogError("[ScenarioRunner] Assign a trained model (.onnx).", this);
                enabled = false;
                return;
            }

            var envs = FindObjectsByType<ParkingEnvironment>()
                .OrderBy(e => Mathf.Round(e.transform.position.z)).ThenBy(e => Mathf.Round(e.transform.position.x)).ToList();
            env = envs[0];
            foreach (var other in envs.Skip(1)) other.gameObject.SetActive(false);
            agent = env.GetComponentInChildren<ParkingAgent>();
            agent.EpisodeFinished += OnEpisodeFinished;

            var bp = agent.GetComponent<BehaviorParameters>();
            bp.Model = model;
            bp.DeterministicInference = deterministic;
            bp.BehaviorType = BehaviorType.InferenceOnly;

            CreateCamera();
            CreatePath();
            LoadAndRun();
        }

        // ------------------------------------------------------------------ scenario

        void LoadAndRun()
        {
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", scenarioFile));
            if (!File.Exists(file))
            {
                Fail($"Scenario file not found: {file}");
                return;
            }
            loaded = JsonUtility.FromJson<ScenarioFile>(File.ReadAllText(file));
            layoutMode = loaded.bays != null && loaded.bays.Length > 0;
            env.earlyEndingsOverride = loaded.early_endings ? -1 : 0;
            var scenario = layoutMode ? BuildLayout(loaded) : NamedScenario(loaded);
            if (scenario == null) return;
            env.ActiveScenario = scenario;
            singleStart = (scenario.startLocalPosition, scenario.startYaw);
            batchIndex = -1;
            FitCamera();
            Restart();
            batchPendingStart = loaded.batch;   // started next frame, once the scenario's parked cars are in place
        }

        ParkingEnvironment.Scenario NamedScenario(ScenarioFile f)
        {
            int target = env.spots.FindIndex(s => s.name == f.target);
            if (target < 0)
            {
                Fail($"Unknown target bay '{f.target}'.");
                return null;
            }
            var scenario = new ParkingEnvironment.Scenario
            {
                targetIndex = target,
                hasStartPose = f.use_start_pose,
                startLocalPosition = new Vector3(f.start_x, 0f, f.start_z),
                startYaw = f.start_yaw,
                spawnStage = f.spawn_stage,
                spawnSeed = f.spawn_seed,
            };
            foreach (string name in f.occupied ?? new string[0])
            {
                int i = env.spots.FindIndex(s => s.name == name);
                if (i >= 0 && i != target) scenario.occupied.Add(i);
                else if (i < 0) Debug.LogWarning($"[ScenarioRunner] Unknown bay '{name}' ignored.", this);
            }
            targetName = f.target;
            occupiedCount = scenario.occupied.Count;
            return scenario;
        }

        /// <summary>Rebuild the area as the car park described by a layout (bays in metres, from a photo).</summary>
        ParkingEnvironment.Scenario BuildLayout(ScenarioFile f)
        {
            var bays = f.bays;
            if (f.target_index < 0 || f.target_index >= bays.Length)
            {
                Fail($"target_index {f.target_index} is not a bay of the layout ({bays.Length} bays).");
                return null;
            }

            // 1. Scale to the training bay width and centre the layout on the area.
            var widths = bays.Select(b => b.width).OrderBy(w => w).ToArray();
            float bayWidth = f.metric_bay_width > 0f ? f.metric_bay_width : widths[widths.Length / 2];
            float pitch = scaleByBayPitch ? RowPitch(bays, f.target_index) : 0f;
            layoutScale = trainingBayWidth / Mathf.Max(0.5f, pitch > 0f ? pitch : bayWidth);
            scaleNote = pitch > 0f ? $"bay spacing {pitch:F2} m" : $"bay width {bayWidth:F2} m";
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var b in bays)
                foreach (var c in Corners(b, layoutScale))
                {
                    minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                    minZ = Mathf.Min(minZ, c.y); maxZ = Mathf.Max(maxZ, c.y);
                }
            layoutCentre = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            env.areaHalfExtents = new Vector2((maxX - minX) * 0.5f + boundaryMargin, (maxZ - minZ) * 0.5f + boundaryMargin);

            // 2. Replace the bays (the old parked cars are children of the old bays and go with them).
            var bayParent = env.transform.Find("Bays");
            if (bayParent)
            {
                bayParent.gameObject.SetActive(false);   // Destroy is deferred to the end of the frame
                Destroy(bayParent.gameObject);
            }
            var parent = new GameObject("Bays").transform;
            parent.SetParent(env.transform, false);
            var spots = new List<ParkingSpot>();
            for (int i = 0; i < bays.Length; i++)
            {
                var b = bays[i];
                var go = new GameObject($"Bay_L{i:00}");
                go.transform.SetParent(parent, false);
                Vector2 p = ToLocal(new Vector2(b.cx, b.cz));
                // Trim the TARGET bay to the trained length, keeping its open end (aisle side) fixed, so its shape is
                // the one the agent learned. Other bays keep their full length: their parked cars stay where the photo
                // puts them (centred in the real bay), so the aisle keeps its real width.
                float length = b.length * layoutScale;
                float trimmed = i == f.target_index && f.trim_target ? Mathf.Min(length, trainingBayLength) : length;
                float width = b.width * layoutScale;
                if (i == f.target_index && clampTargetWidth) width = Mathf.Min(width, trainingBayWidth);
                Vector2 fwd = new Vector2(Mathf.Sin(b.yaw * Mathf.Deg2Rad), Mathf.Cos(b.yaw * Mathf.Deg2Rad));
                p -= fwd * ((length - trimmed) * 0.5f);
                go.transform.localPosition = new Vector3(p.x, 0f, p.y);
                go.transform.localRotation = Quaternion.Euler(0f, b.yaw, 0f);
                var spot = go.AddComponent<ParkingSpot>();
                spot.size = new Vector2(width, trimmed);
                spots.Add(spot);
            }
            env.ReplaceSpots(spots);
            if (parkedCarsAtTrainedDepth)
            {
                for (int i = 0; i < spots.Count; i++)
                {
                    var car = spots[i].parkedCar;
                    if (i == f.target_index || !car) continue;
                    float len = spots[i].size.y;
                    var lp = car.transform.localPosition;
                    // Centred in the first 4.88 m from the open end: deeper than the centre of a long bay, and deeper
                    // than the centre of a bay marked shallow (far rows), so its car never sticks into the aisle.
                    lp.z = -(len - trainingBayLength) * 0.5f;
                    car.transform.localPosition = lp;
                }
            }

            // 3. Floor and boundary walls sized to the layout; hide the training car park's painted art and walls.
            foreach (Transform child in env.transform)
            {
                if (child.name == "AssestGround" || child.CompareTag("Wall")) child.gameObject.SetActive(false);
            }
            if (generated)
            {
                generated.SetActive(false);
                Destroy(generated);
            }
            generated = new GameObject("GeneratedLot");
            generated.transform.SetParent(env.transform, false);
            Vector2 h = env.areaHalfExtents;
            var floorMat = MakeMaterial(new Color(0.32f, 0.33f, 0.35f));
            var wallMat = MakeMaterial(new Color(0.75f, 0.72f, 0.65f));
            Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(2f * h.x + 20f, 1f, 2f * h.y + 20f), floorMat, null);
            const float t = 0.4f, height = 1.2f;
            Box("Wall N", new Vector3(0f, height / 2, h.y + t / 2), new Vector3(2f * h.x + 2 * t, height, t), wallMat, "Wall");
            Box("Wall S", new Vector3(0f, height / 2, -h.y - t / 2), new Vector3(2f * h.x + 2 * t, height, t), wallMat, "Wall");
            Box("Wall E", new Vector3(h.x + t / 2, height / 2, 0f), new Vector3(t, height, 2f * h.y), wallMat, "Wall");
            Box("Wall W", new Vector3(-h.x - t / 2, height / 2, 0f), new Vector3(t, height, 2f * h.y), wallMat, "Wall");
            if (f.back_barriers)
            {
                // Diagnostic: something solid behind every bay, at its full real length (like a kerb with a fence, or the
                // boundary wall behind the training car park's outer rows). 1 m high so the ray sensors can see it.
                const float bt = 0.2f, bh = 1f;
                foreach (var b in bays)
                {
                    Vector2 c = ToLocal(new Vector2(b.cx, b.cz));
                    Vector2 fw = new Vector2(Mathf.Sin(b.yaw * Mathf.Deg2Rad), Mathf.Cos(b.yaw * Mathf.Deg2Rad));
                    Vector2 back = c + fw * (b.length * layoutScale * 0.5f + bt * 0.5f + 0.05f);
                    var go = Box("Back barrier", new Vector3(back.x, bh / 2, back.y), new Vector3(b.width * layoutScale, bh, bt), wallMat, "Wall");
                    go.transform.localRotation = Quaternion.Euler(0f, b.yaw, 0f);
                }
            }
            Physics.SyncTransforms();

            // 4. Scenario: occupancy from the layout, start pose in the aisle in front of the target.
            lastAisleGap = 0f;
            var scenario = new ParkingEnvironment.Scenario { targetIndex = f.target_index, hasStartPose = true };
            for (int i = 0; i < bays.Length; i++)
                if (bays[i].occupied && i != f.target_index) scenario.occupied.Add(i);
            // Park the occupied bays now so the start-pose overlap test sees their cars.
            for (int i = 0; i < spots.Count; i++) spots[i].SetState(scenario.occupied.Contains(i), i == f.target_index);
            Physics.SyncTransforms();
            if (!ChooseStart(f, spots[f.target_index], scenario)) return null;

            targetName = $"bay {f.target_index}";
            occupiedCount = scenario.occupied.Count;
            // Training aisles leave 6.6-7.3 m between parked cars (simulation scale). Narrower ones were not trained.
            aisleNote = lastAisleGap > 0f && lastAisleGap < 5f
                ? $"  |  aisle {lastAisleGap / layoutScale:F1} m real ({lastAisleGap:F1} m sim), narrower than trained (6.6-7.3 m sim)" : "";
            if (!f.trim_target || f.back_barriers || !f.early_endings)
                Debug.Log($"[ScenarioRunner] Diagnostics: trim target {(f.trim_target ? "on" : "OFF")}, back barriers " +
                          $"{(f.back_barriers ? "ON" : "off")}, early endings {(f.early_endings ? "on" : "OFF (30 s limit only)")}.", this);
            Debug.Log($"[ScenarioRunner] Layout: {bays.Length} bays, scale x{layoutScale:F2} (from {scaleNote}), " +
                      $"parked cars at {(parkedCarsAtTrainedDepth ? "trained depth" : "bay centres")}, area {2 * h.x:F0} x {2 * h.y:F0} m, " +
                      $"target {f.target_index}, {occupiedCount} parked cars, start {f.start_mode}, " +
                      $"aisle gap {lastAisleGap:F1} m (sim).", this);
            return scenario;
        }

        /// <summary>Start pose for a layout: along the aisle (either side), facing the bay, or given in the file.</summary>
        bool ChooseStart(ScenarioFile f, ParkingSpot target, ParkingEnvironment.Scenario scenario)
        {
            var body = agent.GetComponent<BoxCollider>();
            Vector3 fwd = Vector3.ProjectOnPlane(target.Forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float front = target.size.y * 0.5f;
            var candidates = new List<(Vector3 pos, float yaw)>();

            if (f.start_mode == "pose")
            {
                Vector2 p = ToLocal(new Vector2(f.start_x, f.start_z));
                candidates.Add((env.transform.TransformPoint(new Vector3(p.x, 0f, p.y)), f.start_yaw));
            }
            // "facing" uses only the facing starts below. (Before 2026-10-11 the aisle starts were also added for "facing"
            // and, coming first, won whenever one fitted.) Unknown modes fall back to the aisle.
            if (f.start_mode != "facing" && f.start_mode != "pose")
            {
                // Driving along the aisle towards the bay, 7 m to one side. Preferred: the trained distance from the
                // bays' open ends (aisleOffset, the middle of a training aisle), as long as the far side of the aisle
                // leaves room; otherwise the middle of the measured free aisle. Both sides are tried.
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector3 edge = target.Center - fwd * front + right * (7f * side);   // on the bay row's open-end line
                    float gap = AisleGap(edge, fwd, out float middle, out float ahead);
                    lastAisleGap = Mathf.Max(lastAisleGap, gap);
                    float yaw = Quaternion.LookRotation(-right * side).eulerAngles.y;
                    candidates.Add((edge - fwd * StartOffset(middle, ahead), yaw));
                    candidates.Add((edge - fwd * middle, yaw));
                }
            }
            // Facing the bay from the aisle (always tried last as a fallback).
            foreach (float off in new[] { 4.5f, 3.5f, 2.5f })
                candidates.Add((target.Center - fwd * (front + off), Quaternion.LookRotation(fwd).eulerAngles.y));

            foreach (var (pos, yaw) in candidates)
            {
                if (!ValidStart(pos, yaw, out var local, out float localYaw)) continue;
                scenario.startLocalPosition = local;
                scenario.startYaw = localYaw;
                return true;
            }
            Fail("No free start position in front of the target bay (the aisle looks blocked).");
            return false;
        }

        /// <summary>A start pose is valid if the car fits there: inside the area and not touching a wall or parked car.</summary>
        bool ValidStart(Vector3 pos, float yaw, out Vector3 localPos, out float localYaw)
        {
            var body = agent.GetComponent<BoxCollider>();
            Vector3 p = new Vector3(pos.x, env.transform.position.y + env.carSpawnHeight, pos.z);
            localPos = env.transform.InverseTransformPoint(p);
            localYaw = yaw - env.transform.eulerAngles.y;
            return env.IsInsideArea(p, 2f) && !ParkingEnvironment.Overlaps(p, Quaternion.Euler(0f, yaw, 0f), body);
        }

        // ------------------------------------------------------------------ batch test

        /// <summary>Starts around the target bay: along the aisle on both sides at several distances, and facing the bay.</summary>
        void BuildBatchStarts()
        {
            batchStarts.Clear();
            var target = env.spots[env.ActiveScenario.targetIndex];
            Vector3 fwd = Vector3.ProjectOnPlane(target.Forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float front = target.size.y * 0.5f;
            foreach (float side in new[] { 1f, -1f })
                foreach (float lateral in batchLateral)
                {
                    Vector3 edge = target.Center - fwd * front + right * (lateral * side);
                    AisleGap(edge, fwd, out float middle, out float ahead);
                    float preferred = StartOffset(middle, ahead);
                    float yaw = Quaternion.LookRotation(-right * side).eulerAngles.y;
                    foreach (float extra in batchStartOffsets)
                        if (ValidStart(edge - fwd * (preferred + extra), yaw, out var lp, out float ly))
                            batchStarts.Add((lp, ly, $"aisle {(side > 0 ? "right" : "left")} {lateral:F0} m {extra:+0.0;-0.0}"));
                }
            foreach (float off in batchFacing)
                if (ValidStart(target.Center - fwd * (front + off), Quaternion.LookRotation(fwd).eulerAngles.y, out var lp, out float ly))
                    batchStarts.Add((lp, ly, $"facing {off:F1} m"));
        }

        void StartBatch()
        {
            if (env.ActiveScenario == null) return;
            BuildBatchStarts();
            if (batchStarts.Count == 0)
            {
                Fail("Batch test: no free start positions around the target bay.");
                return;
            }
            batchRows.Clear();
            batchResults.Clear();
            if (loaded != null && loaded.trace)
            {
                // Remove traces of an earlier batch so every run_XX.csv belongs to this one.
                string traces = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputFolder)), "traces");
                if (Directory.Exists(traces))
                    foreach (var file in Directory.GetFiles(traces, "run_*.csv")) File.Delete(file);
            }
            if (batchPaths) Destroy(batchPaths);
            batchPaths = new GameObject("BatchPaths");
            batchIndex = 0;
            RunBatchItem();
        }

        void RunBatchItem()
        {
            var (lp, ly, label) = batchStarts[batchIndex];
            env.ActiveScenario.hasStartPose = true;
            env.ActiveScenario.startLocalPosition = lp;
            env.ActiveScenario.startYaw = ly;
            Restart(batchTimeScale);
            status = $"Batch test {batchIndex + 1}/{batchStarts.Count}: {label}";
        }

        void RecordBatch(ParkingAgent.EpisodeResult r, float seconds)
        {
            var ci = CultureInfo.InvariantCulture;
            float d = layoutMode ? r.finalDistance / layoutScale : r.finalDistance;
            bool parked = r.outcome == ParkingAgent.Outcome.Parked;
            var (lp, ly, label) = batchStarts[batchIndex];
            batchResults.Add((parked, r.outcome.ToString(), seconds, d, r.finalHeadingError));
            batchRows.Add(string.Join(",", batchIndex.ToString(ci), label, lp.x.ToString("F2", ci), lp.z.ToString("F2", ci),
                ly.ToString("F1", ci), r.outcome.ToString(), parked ? "1" : "0", seconds.ToString("F2", ci), d.ToString("F3", ci),
                r.finalHeadingError.ToString("F2", ci), r.noseIn ? "nose-in" : "reverse-in"));

            // Keep this run's path: green = parked, orange = stuck / no progress / timeout, red = collision / out of bounds.
            var line = new GameObject($"Run {batchIndex}").AddComponent<LineRenderer>();
            line.transform.SetParent(batchPaths.transform, false);
            line.useWorldSpace = true;
            line.widthMultiplier = pathWidth * 0.6f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.sharedMaterial = path.sharedMaterial;
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            bool crash = r.outcome == ParkingAgent.Outcome.Collision || r.outcome == ParkingAgent.Outcome.OutOfBounds;
            Tint(line, parked ? new Color(0.2f, 0.85f, 0.3f) : crash ? new Color(0.95f, 0.2f, 0.2f) : new Color(1f, 0.6f, 0.1f));
        }

        void FinishBatch()
        {
            int n = batchResults.Count, ok = batchResults.Count(b => b.parked);
            var parked = batchResults.Where(b => b.parked).ToList();
            float meanHeading = parked.Count > 0 ? parked.Average(b => b.heading) : float.NaN;
            float meanDist = parked.Count > 0 ? parked.Average(b => b.dist) : float.NaN;
            float meanTime = parked.Count > 0 ? parked.Average(b => b.seconds) : float.NaN;
            var outcomes = batchResults.GroupBy(b => b.outcome).ToDictionary(g => g.Key, g => g.Count());
            status = $"Batch test: parked {ok}/{n} ({100f * ok / Mathf.Max(1, n):F0}%)  |  parked: {meanTime:F1} s, " +
                     $"{meanDist:F2} m from centre, {meanHeading:F1} deg  |  " + string.Join(", ", outcomes.Select(o => $"{o.Key} {o.Value}"));
            Debug.Log("[ScenarioRunner] " + status, this);
            batchIndex = -1;

            // Back to the single-run start, so R replays the scenario's own manoeuvre.
            env.ActiveScenario.startLocalPosition = singleStart.pos;
            env.ActiveScenario.startYaw = singleStart.yaw;

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputFolder));
            Directory.CreateDirectory(folder);
            var ci = CultureInfo.InvariantCulture;
            var csv = new StringBuilder("run,start,start_x,start_z,start_yaw,outcome,success,seconds,final_distance_m,final_heading_error_deg,finish\n");
            foreach (var row in batchRows) csv.Append(row).Append('\n');
            File.WriteAllText(Path.Combine(folder, "batch_result.csv"), csv.ToString(), new UTF8Encoding(false));
            var js = new StringBuilder("{\n");
            js.Append($"  \"model\": \"{model.name}\",\n  \"target\": \"{targetName}\",\n  \"runs\": {n},\n  \"parked\": {ok},\n");
            js.Append($"  \"success_rate\": {(n > 0 ? (float)ok / n : 0f).ToString("F3", ci)},\n");
            js.Append($"  \"parked_mean_seconds\": {Num(meanTime)},\n  \"parked_mean_distance_m\": {Num(meanDist)},\n");
            js.Append($"  \"parked_mean_heading_error_deg\": {Num(meanHeading)},\n");
            js.Append("  \"outcomes\": {" + string.Join(", ", outcomes.Select(o => $"\"{o.Key}\": {o.Value}")) + "},\n");
            js.Append($"  \"time_written\": \"{System.DateTime.Now:yyyy-MM-ddTHH:mm:ss}\"\n}}\n");
            File.WriteAllText(Path.Combine(folder, "batch_summary.json"), js.ToString(), new UTF8Encoding(false));

            // Overview image: every path, without the single-run line and start marker.
            path.gameObject.SetActive(false);
            startMarker.SetActive(false);
            StartCoroutine(RenderTopDown(Path.Combine(folder, "result_batch.png"), () =>
            {
                path.gameObject.SetActive(true);
                startMarker.SetActive(true);
            }));
        }

        static string Num(float v) => float.IsNaN(v) ? "null" : v.ToString("F3", CultureInfo.InvariantCulture);

        float lastAisleGap;
        string scaleNote = "";

        /// <summary>Mean centre-to-centre distance (photo metres) from the target to its direct neighbours in its row
        /// (same facing, beside it); 0 if it has none.</summary>
        static float RowPitch(BayData[] bays, int target)
        {
            var t = bays[target];
            Vector2 fwd = new Vector2(Mathf.Sin(t.yaw * Mathf.Deg2Rad), Mathf.Cos(t.yaw * Mathf.Deg2Rad));
            Vector2 right = new Vector2(fwd.y, -fwd.x);
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < bays.Length; i++)
            {
                if (i == target) continue;
                var b = bays[i];
                if (Mathf.Abs(Mathf.DeltaAngle(b.yaw, t.yaw)) > 20f) continue;
                Vector2 d = new Vector2(b.cx - t.cx, b.cz - t.cz);
                float across = Mathf.Abs(Vector2.Dot(d, right)), along = Mathf.Abs(Vector2.Dot(d, fwd));
                if (across > 0.5f * t.width && across < 1.6f * t.width && along < 0.5f * t.length)
                {
                    sum += across;
                    n++;
                }
            }
            return n > 0 ? sum / n : 0f;
        }

        /// <summary>
        /// Distance of an aisle start from the bays' open ends: the trained aisleOffset (3.4 m), closer only when the far
        /// side of the aisle is nearer than aisleOffset + clearance, and never closer than the middle of the free aisle.
        /// (Earlier version took the larger of the trained distance and the middle, so in a wide or empty aisle the car
        /// started in the middle of open space, about 9 m out on camera 6 with the back row empty.)
        /// </summary>
        float StartOffset(float middle, float ahead)
        {
            const float clearance = 1.6f;   // car half-width plus margin
            return Mathf.Min(aisleOffset, Mathf.Max(middle, ahead - clearance));
        }

        /// <summary>Free distance across the aisle at a point on the bay row's open-end line: to the nearest obstacle
        /// (parked car or wall) in front of the row, plus back to the row's own parked cars. Returns the gap and,
        /// in middle, the offset from the open-end line to the middle of the free space.</summary>
        float AisleGap(Vector3 edge, Vector3 fwd, out float middle, out float ahead)
        {
            Vector3 origin = edge + Vector3.up * 0.6f;
            ahead = NearestObstacle(origin, -fwd, 30f);           // across the aisle
            float behind = NearestObstacle(origin, fwd, 3f);      // towards our own row's parked cars
            if (ahead >= 30f) ahead = aisleOffset * 2f;
            if (behind >= 3f) behind = 0f;                         // no car there: the open-end line itself
            middle = (ahead - behind) * 0.5f;
            return ahead + behind;
        }

        static float NearestObstacle(Vector3 origin, Vector3 dir, float range)
        {
            float best = range;
            foreach (var hit in Physics.RaycastAll(origin, dir, range, ~0, QueryTriggerInteraction.Ignore))
                if (ParkingEnvironment.IsObstacle(hit.collider)) best = Mathf.Min(best, hit.distance);
            return best;
        }

        static IEnumerable<Vector2> Corners(BayData b, float s)
        {
            var rot = Quaternion.Euler(0f, b.yaw, 0f);
            for (int i = 0; i < 4; i++)
            {
                var c = rot * new Vector3((i % 2 == 0 ? -0.5f : 0.5f) * b.width, 0f, (i < 2 ? -0.5f : 0.5f) * b.length);
                yield return new Vector2((b.cx + c.x) * s, (b.cz + c.z) * s);
            }
        }

        Vector2 ToLocal(Vector2 layout) => layout * layoutScale - layoutCentre;
        Vector2 ToLayout(Vector2 local) => (local + layoutCentre) / layoutScale;

        GameObject Box(string name, Vector3 localPos, Vector3 scale, Material mat, string tag)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(generated.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (tag != null) go.tag = tag;
            return go;
        }

        static Material MakeMaterial(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) return null;
            var m = new Material(shader);
            m.SetColor("_BaseColor", c);
            return m;
        }

        void Fail(string message)
        {
            status = message;
            Debug.LogError("[ScenarioRunner] " + message, this);
        }

        void Restart() => Restart(timeScale);

        void Restart(float scale)
        {
            if (env.ActiveScenario == null) return;
            running = false;                 // ignore the episode-end report of the interrupted run
            env.HoldReset = false;
            Time.timeScale = scale;
            agent.EndEpisode();              // -> OnEpisodeBegin -> ResetArea -> the scenario
            points.Clear();
            points.Add(Ground(agent.transform.position));
            path.positionCount = 1;
            path.SetPosition(0, points[0]);
            startMarker.transform.position = points[0];
            startMarker.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(agent.transform.forward, Vector3.up));
            status = "Parking...";
            StartTrace();
            running = true;
        }

        void OnEpisodeFinished(ParkingAgent a, ParkingAgent.EpisodeResult r)
        {
            // ML-Agents starts the agent's first episode itself, just after our Restart(); that reports a
            // 0-step "timeout" for the episode it replaces. A real run always takes at least one step.
            if (!running || r.steps == 0) return;
            running = false;
            env.HoldReset = true;            // keep the final pose on screen
            Time.timeScale = 0f;
            AddPoint(true);
            float seconds = r.steps * Time.fixedDeltaTime;
            if (trace != null) TraceRow();   // final state
            SaveTrace(batchIndex >= 0 ? $"run_{batchIndex:00}.csv" : "trace.csv");
            if (batchIndex >= 0)
            {
                RecordBatch(r, seconds);
                batchNext = true;            // the next run starts from Update (not inside the agent's episode end)
                return;
            }
            status = $"{r.outcome} in {seconds:F1} s  |  distance to bay centre {r.finalDistance:F2} m  |  heading error {r.finalHeadingError:F1} deg";
            StartCoroutine(SaveOutputs(r, seconds));
        }

        // ------------------------------------------------------------------ per frame

        void FixedUpdate()
        {
            if (!running) return;
            AddPoint(false);
            if (trace != null && agent.StepCount % 5 == 0) TraceRow();
        }

        void StartTrace()
        {
            trace = null;
            if (loaded == null || !loaded.trace) return;
            if (!rays) rays = agent.GetComponentInChildren<Unity.MLAgents.Sensors.RayPerceptionSensorComponent3D>();
            trace = new StringBuilder();
            trace.Append("step,time,x,z,yaw,speed,steer_angle,act_steer,act_throttle,");
            for (int i = 0; i < 9; i++) trace.Append($"obs{i},");
            trace.Append("distance,heading_err,depth,lateral,corners,slow_ok,aligned_ok,parked_now,potential,in_bay,line_up,stop,approach");
            int n = rays && rays.RaySensor != null ? rays.RaySensor.RayPerceptionOutput.RayOutputs.Length : 0;
            for (int i = 0; i < n; i++) trace.Append($",ray{i}_tag,ray{i}_dist");
            trace.Append('\n');
        }

        void TraceRow()
        {
            var ci = CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("F4", ci);
            var info = agent.GetTrace();
            var obs = agent.GetObservations();
            var act = agent.GetStoredActionBuffers().ContinuousActions;
            Vector3 l = env.transform.InverseTransformPoint(agent.transform.position);
            float yaw = agent.transform.eulerAngles.y - env.transform.eulerAngles.y;
            var car = agent.GetComponent<CarController>();
            trace.Append(agent.StepCount).Append(',').Append(F(agent.StepCount * Time.fixedDeltaTime)).Append(',')
                 .Append(F(l.x)).Append(',').Append(F(l.z)).Append(',').Append(F(yaw)).Append(',')
                 .Append(F(car.Speed)).Append(',').Append(F(car.SteerAngle)).Append(',')
                 .Append(F(act.Length > 0 ? act[0] : 0f)).Append(',').Append(F(act.Length > 1 ? act[1] : 0f));
            for (int i = 0; i < 9; i++) trace.Append(',').Append(F(i < obs.Count ? obs[i] : 0f));
            trace.Append(',').Append(F(info.distance)).Append(',').Append(F(info.headingError)).Append(',')
                 .Append(F(info.depth)).Append(',').Append(F(info.lateral)).Append(',').Append(info.corners).Append(',')
                 .Append(info.slowEnough ? 1 : 0).Append(',').Append(info.alignedEnough ? 1 : 0).Append(',')
                 .Append(info.parkedNow ? 1 : 0).Append(',').Append(F(info.potential)).Append(',').Append(F(info.inBay)).Append(',')
                 .Append(F(info.lineUp)).Append(',').Append(F(info.stop)).Append(',').Append(F(info.approach));
            if (rays && rays.RaySensor != null)
            {
                float len = rays.RayLength;
                foreach (var r in rays.RaySensor.RayPerceptionOutput.RayOutputs)
                    trace.Append(',').Append(r.HasHit ? r.HitTagIndex : -1).Append(',').Append(F(r.HasHit ? r.HitFraction * len : len));
            }
            trace.Append('\n');
        }

        void SaveTrace(string fileName)
        {
            if (trace == null) return;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputFolder));
            if (batchIndex >= 0) folder = Path.Combine(folder, "traces");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, fileName), trace.ToString(), new UTF8Encoding(false));
            trace = null;
        }

        void Update()
        {
            if (env == null) return;
            if (batchPendingStart)
            {
                batchPendingStart = false;
                StartBatch();
            }
            if (batchNext)
            {
                batchNext = false;
                if (++batchIndex < batchStarts.Count) RunBatchItem();
                else FinishBatch();
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.rKey.wasPressedThisFrame) { batchIndex = -1; Restart(); }
            if (kb.lKey.wasPressedThisFrame) LoadAndRun();
            if (kb.bKey.wasPressedThisFrame && batchIndex < 0) StartBatch();
        }

        void AddPoint(bool force)
        {
            Vector3 p = Ground(agent.transform.position);
            if (!force && (p - points[points.Count - 1]).sqrMagnitude < 0.01f) return;
            points.Add(p);
            path.positionCount = points.Count;
            path.SetPosition(points.Count - 1, p);
        }

        Vector3 Ground(Vector3 p) => new Vector3(p.x, env.transform.position.y + 0.3f, p.z);

        // ------------------------------------------------------------------ visuals

        void CreateCamera()
        {
            var go = new GameObject("ScenarioTopCamera");
            topCam = go.AddComponent<Camera>();
            topCam.orthographic = true;
            topCam.depth = 100f;             // draws over the scene's main camera in the Game view
            topCam.clearFlags = CameraClearFlags.SolidColor;
            topCam.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            topCam.farClipPlane = 200f;
        }

        void FitCamera()
        {
            topCam.orthographicSize = Mathf.Max(env.areaHalfExtents.x, env.areaHalfExtents.y) * 1.05f;
            topCam.transform.position = env.transform.position + Vector3.up * 60f;
            topCam.transform.rotation = Quaternion.Euler(90f, env.transform.eulerAngles.y, 0f);
        }

        void CreatePath()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = env.indicatorMaterial ? new Material(env.indicatorMaterial) : shader ? new Material(shader) : null;

            path = new GameObject("ScenarioPath").AddComponent<LineRenderer>();
            path.useWorldSpace = true;
            path.widthMultiplier = pathWidth;
            path.numCapVertices = 4;
            path.numCornerVertices = 4;
            path.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (mat) path.sharedMaterial = mat;
            Tint(path, pathColor);

            // Start marker: a triangle showing the start position and heading.
            startMarker = new GameObject("ScenarioStart");
            var arrow = startMarker.AddComponent<LineRenderer>();
            arrow.useWorldSpace = false;
            arrow.loop = true;
            arrow.widthMultiplier = 0.2f;
            arrow.positionCount = 3;
            arrow.SetPositions(new[] { new Vector3(-0.9f, 0f, -1.2f), new Vector3(0f, 0f, 1.5f), new Vector3(0.9f, 0f, -1.2f) });
            arrow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (mat) arrow.sharedMaterial = mat;
            Tint(arrow, startColor);
        }

        static void Tint(Renderer r, Color c)
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }

        void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            string title = loaded != null ? $"ParkPilot Assist  |  target {targetName}  |  {occupiedCount} parked cars" : "ParkPilot Assist";
            GUI.Box(new Rect(10, 10, Screen.width - 20, 64), $"{title}{aisleNote}\n{status}", style);
            GUI.Label(new Rect(14, Screen.height - 30, 700, 24), "R = replay    L = reload scenario file    B = batch test (many starts)");
        }

        // ------------------------------------------------------------------ output

        IEnumerator RenderTopDown(string file, System.Action after = null)
        {
            yield return new WaitForEndOfFrame();   // works while time is paused
            var rt = new RenderTexture(imageSize, imageSize, 24);
            topCam.targetTexture = rt;
            topCam.aspect = 1f;
            topCam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(imageSize, imageSize, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, imageSize, imageSize), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            topCam.targetTexture = null;
            topCam.ResetAspect();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Destroy(tex);
            rt.Release();
            after?.Invoke();
        }

        IEnumerator SaveOutputs(ParkingAgent.EpisodeResult r, float seconds)
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputFolder));
            Directory.CreateDirectory(folder);
            yield return RenderTopDown(Path.Combine(folder, "result.png"));

            // result.json, written by hand to keep floats in invariant culture
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"outcome\": \"{r.outcome}\",\n");
            sb.Append($"  \"success\": {(r.outcome == ParkingAgent.Outcome.Parked ? "true" : "false")},\n");
            sb.Append($"  \"seconds\": {seconds.ToString("F2", ci)},\n");
            // distances in the photo's metres for layouts (the simulation was scaled by layout_scale)
            float d = layoutMode ? r.finalDistance / layoutScale : r.finalDistance;
            sb.Append($"  \"final_distance_m\": {d.ToString("F3", ci)},\n");
            sb.Append($"  \"final_heading_error_deg\": {r.finalHeadingError.ToString("F2", ci)},\n");
            sb.Append($"  \"finish_direction\": \"{(r.noseIn ? "nose-in" : "reverse-in")}\",\n");
            sb.Append($"  \"target\": \"{targetName}\",\n");
            sb.Append($"  \"layout_mode\": {(layoutMode ? "true" : "false")},\n");
            sb.Append($"  \"layout_scale\": {layoutScale.ToString("F4", ci)},\n");
            sb.Append($"  \"model\": \"{model.name}\",\n");
            sb.Append($"  \"source_image\": \"{(loaded.source_image ?? "").Replace("\\", "/")}\",\n");
            sb.Append($"  \"time_written\": \"{System.DateTime.Now:yyyy-MM-ddTHH:mm:ss}\",\n");
            // Path in the scenario's own frame: layout metres (layout mode) or area-local metres (named mode).
            sb.Append("  \"path_xz\": [");
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 l = env.transform.InverseTransformPoint(points[i]);
                Vector2 q = layoutMode ? ToLayout(new Vector2(l.x, l.z)) : new Vector2(l.x, l.z);
                sb.Append($"{(i > 0 ? ", " : "")}[{q.x.ToString("F3", ci)}, {q.y.ToString("F3", ci)}]");
            }
            sb.Append("]\n}\n");
            File.WriteAllText(Path.Combine(folder, "result.json"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log($"[ScenarioRunner] {status}. Saved {Path.Combine(folder, "result.png")}", this);
        }
    }
}
