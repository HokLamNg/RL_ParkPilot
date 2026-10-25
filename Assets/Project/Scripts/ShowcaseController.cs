using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkPilot
{
    /// <summary>
    /// ParkPilot showcase (Showcase scene):
    /// 1. the car waits outside the car park; C captures the car park with the CCTV camera;
    /// 2. the photo is shown with every bay outlined, free (green) or occupied (red): from the occupancy CNN when one is
    ///    assigned (run in Unity with the Inference Engine), otherwise the scene's ground truth (labelled as such);
    /// 3. click a free bay: a scripted drive-in (fixed route for this car park, not learned) follows the aisles towards
    ///    it and hands over on the last leg once the car is lined up with the aisle (handoverMaxAngle), at least
    ///    minHandoverLateral to the side of the bay and within handoverDistance of it (or at the car park centre with
    ///    handoverAtCentre);
    /// 4. the trained RL policy parks; the drive-in (orange) and the RL manoeuvre (blue) are drawn.
    /// R restarts with a new random occupancy. The interface is in ShowcaseUI.cs; docs/System-Reference/12-Showcase.md.
    /// </summary>
    [DefaultExecutionOrder(-1000)]   // Awake before the agent is enabled (its input size depends on the model)
    public partial class ShowcaseController : MonoBehaviour
    {
        [Header("Policy")]
        [Tooltip("Trained parking policy, e.g. Assets/Project/Models/ExpFull/full3_s1.onnx (adopted). Its inputs (bay size, near rays) are detected automatically.")]
        public ModelAsset model;
        [Tooltip("Second parking policy the driver can choose in Settings (round 6: the DQN model, e.g. " +
                 "Assets/Project/Models/ExpR6/rdqn2_s2.onnx). Must have the same inputs as Model. Empty = no choice.")]
        public ModelAsset alternativeModel;

        [Header("Occupancy CNN (optional)")]
        [Tooltip("Vision/export_occupancy_onnx.py output. Empty = use the scene's ground truth occupancy.")]
        public ModelAsset occupancyCnn;
        public float occupiedThreshold = 0.5f;

        [Header("Scene")]
        public Transform startPoint;
        public Transform entrancePoint;
        [Tooltip("Optional waypoints along the town roads, driven in order between the start and the entrance.")]
        public Transform[] approachPoints = new Transform[0];
        [Tooltip("Fixed camera that 'photographs' the car park.")]
        public Camera cctvCamera;
        [Tooltip("Camera the player sees (follows the car).")]
        public Camera viewCamera;
        public int photoWidth = 1000, photoHeight = 750;

        [Header("Car park")]
        [Range(0f, 1f)] public float occupiedShare = 0.6f;
        [Tooltip("-1 = new random occupancy every run.")]
        public int randomSeed = -1;

        [Header("Drive-in")]
        public float driveSpeed = 4f;
        [Tooltip("Radius of the turns on the scripted route (the car's tightest is about 3.7 m).")]
        public float cornerRadius = 5f;
        [Tooltip("Hand over to the RL policy once the car is this close (m) to the chosen bay's centre.")]
        public float handoverDistance = 10f;
        [Tooltip("Drive to the centre of the car park and hand over there (instead of driving up the target's aisle).")]
        public bool handoverAtCentre;
        [Tooltip("Hand over only once the car's heading is within this angle of the aisle (the last leg of the route).")]
        public float handoverMaxAngle = 20f;
        [Tooltip("Aisle hand-over: the car must be at least this far to the side of the bay (along the aisle); if it enters " +
                 "the aisle closer than that, it drives past the bay and hands over on the other side.")]
        public float minHandoverLateral = 5f;

        [Header("Synthetic CNN training data (key G)")]
        [Tooltip("Random car parks rendered by the CCTV camera; every bay is cropped and saved as free / occupied " +
                 "(exact labels from the scene) for fine-tuning the occupancy CNN on Unity images.")]
        public int syntheticLayouts = 200;
        [Tooltip("Folder relative to the project folder (git-ignored Data/).")]
        public string syntheticFolder = "Data/unity_patches";
        public int syntheticPatchSize = 96;

        [Header("Run log")]
        [Tooltip("Write every run to <project>/Scenarios/showcase: results.csv (one row per run), a per-step trace and an " +
                 "end screenshot per run.")]
        public bool logRuns = true;
        public string logFolder = "Scenarios/showcase";

        [Header("Batch test (key B)")]
        [Tooltip("Runs in a row, unattended: random car park, CCTV photo + CNN, a random bay the CNN calls free, drive-in, RL parks.")]
        public int batchRuns = 30;
        [Tooltip("Game speed during the batch (physics and decisions are unchanged per step, only faster in real time).")]
        [Range(1f, 8f)] public float batchTimeScale = 4f;

        [Header("Debug overlay (key D)")]
        [Tooltip("Show the agent's ray sensor in the world and a panel with its inputs, actions, parked check, rewards and endings.")]
        public bool debugMode;

        enum Phase { Menu, Arrive, Photo, Driving, Parking, Done, Generating }
        const string ArriveText = "You are outside the car park. Press C to capture the car park with the CCTV camera.   (B = batch test, G = generate CNN training data)";
        Phase phase;
        ParkingEnvironment env;
        ParkingAgent agent;
        CarController car;
        System.Random rng;
        string status = "";

        // photo
        Texture2D photo, photoShown;
        readonly List<Vector2[]> quads = new List<Vector2[]>();   // per bay, photo pixels, top-left origin; null = not visible
        float[] occupiedProb;
        bool usedCnn;
        int target = -1;

        // drive-in
        readonly List<Vector3> route = new List<Vector3>();
        int routeIndex;
        float speedNow;
        LineRenderer drivePath, parkPath;

        // debug overlay
        RayPerceptionSensorComponent3D[] raySensors;
        readonly List<LineRenderer> rayLines = new List<LineRenderer>();
        readonly List<(string tag, float distance, float angle)> rayHits = new List<(string, float, float)>();
        int rayCount;
        float lastCumulative, stepReward;
        readonly List<float> speedHistory = new List<float>(), rewardHistory = new List<float>();
        const int HistoryLength = 600;
        float[] lastObs = new float[0];                 // kept: the agent clears its observations when the episode ends

        // batch test
        bool batchRunning;
        string batchId = "";
        readonly List<(string bay, string group, string outcome, string hit, float seconds, float handover)> batchResults
            = new List<(string, string, string, string, float, float)>();

        // run log
        System.Text.StringBuilder trace;
        string runId, handoverInfo = "", cnnInfo = "";
        int runNumber;
        float lastHandoverDistance;

        void Awake()
        {
            if (!Academy.Instance.IsCommunicatorOn) ParkingAgent.ConfigureFor(model);   // never override a training build's inputs
        }

        void Start()
        {
            if (Academy.Instance.IsCommunicatorOn) { enabled = false; return; }
            StartCoroutine(MoveToLandscapeDisplay());
            env = FindFirstObjectByType<ParkingEnvironment>();
            if (!env)
            {
                Debug.LogError("[Showcase] No car park (ParkingEnvironment) in the scene: the ShowcaseArea object is missing or " +
                               "inactive. Restore Showcase.unity from git.", this);
                enabled = false;
                return;
            }
            agent = env.GetComponentInChildren<ParkingAgent>();
            car = agent.GetComponent<CarController>();
            var bp = agent.GetComponent<BehaviorParameters>();
            bp.Model = model;
            bp.DeterministicInference = true;
            bp.BehaviorType = BehaviorType.InferenceOnly;
            agent.EpisodeFinished += OnEpisodeFinished;
            raySensors = agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>();
            rng = randomSeed >= 0 ? new System.Random(randomSeed) : new System.Random();
            drivePath = MakeLine("DriveInPath", new Color(1f, 0.55f, 0f));
            parkPath = MakeLine("ParkPath", new Color(0.1f, 0.45f, 1f));
            Restart();
            phase = Phase.Menu;                          // the driver starts on the main menu (ShowcaseUI)
            InitUI();
        }

        /// <summary>
        /// Built app only: the interface is laid out for landscape screens. If the window opens on a portrait monitor
        /// (e.g. a vertical second screen, which Unity may use as the main display or remember from the last run), move it
        /// to the largest landscape monitor. Skipped when the app is started with "-monitor N" (an explicit choice).
        /// </summary>
        System.Collections.IEnumerator MoveToLandscapeDisplay()
        {
            if (Application.isEditor) yield break;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-monitor") >= 0) yield break;
            var current = Screen.mainWindowDisplayInfo;
            if (current.width >= current.height) yield break;
            var displays = new List<DisplayInfo>();
            Screen.GetDisplayLayout(displays);
            int best = -1;
            long bestArea = 0;
            for (int i = 0; i < displays.Count; i++)
            {
                long area = (long)displays[i].width * displays[i].height;
                if (displays[i].width > displays[i].height && area > bestArea) { best = i; bestArea = area; }
            }
            if (best < 0) yield break;                   // no landscape monitor: stay
            var target = displays[best];
            var move = Screen.MoveMainWindowTo(target, Vector2Int.zero);
            while (move != null && !move.isDone) yield return null;
            Debug.Log($"[Showcase] Moved the window from a portrait display ({current.width} x {current.height}) to {target.name} ({target.width} x {target.height}).");
        }

        /// <summary>New random car park and the car back outside (R; also the first setup).</summary>
        void Restart()
        {
            // Parked cars: each of the 30 bays (sideways bays included) occupied with probability occupiedShare; outlines hidden (the photo must not give occupancy away).
            agent.Paused = true;
            env.HoldReset = true;
            env.ActiveScenario = null;
            foreach (var s in env.spots)
            {
                s.SetState(rng.NextDouble() < occupiedShare, false);
                s.SetIndicatorVisible(false);
            }
            Physics.SyncTransforms();
            ResetCarToStart();
            photo = photoShown = null;
            phase = Phase.Arrive;
            status = ArriveText;
        }

        bool startedAtEntrance;
        float routeLength;

        /// <summary>Share of the drive-in route already driven (0-1), by distance.</summary>
        float RouteProgress()
        {
            if (route.Count < 2 || routeLength <= 0f) return 0f;
            float left = Vector3.Distance(car.transform.position, route[routeIndex]);
            for (int i = routeIndex + 1; i < route.Count; i++) left += Vector3.Distance(route[i - 1], route[i]);
            return Mathf.Clamp01(1f - left / routeLength);
        }
        bool snapCamera;                // put the chase camera straight behind the car (after a reset)
        float driveHoldUntil;           // the car waits at the start until this game time (Time.time)

        /// <summary>Car back outside the car park, same parked cars (cancel, try again, choose another bay).
        /// atEntrance: at the car park's entrance gap instead of out in town (Try again).</summary>
        void ResetCarToStart(bool atEntrance = false)
        {
            agent.Paused = true;
            env.HoldReset = true;
            env.ActiveScenario = null;
            foreach (var s in env.spots)
            {
                s.SetIndicatorVisible(false);
                s.SetIndicatorOverride(null);
            }
            car.SetExternalControl(true);
            startedAtEntrance = atEntrance;
            snapCamera = true;
            driveHoldUntil = Time.time + (atEntrance ? 1.2f : 0f);   // a moment at the entrance, so the restart is visible
            Vector3 p = atEntrance ? env.transform.TransformPoint(new Vector3(-env.areaHalfExtents.x - 2f, 0f, 0f))   // in the west wall gap
                : startPoint ? startPoint.position : env.transform.TransformPoint(new Vector3(-34f, 0f, 0f));
            p.y = env.transform.position.y + env.carSpawnHeight;
            Quaternion r = atEntrance ? env.transform.rotation * Quaternion.Euler(0f, 90f, 0f)                          // facing into the car park
                : startPoint ? Quaternion.Euler(0f, startPoint.eulerAngles.y, 0f) : env.transform.rotation * Quaternion.Euler(0f, 90f, 0f);
            // Teleport sets the Rigidbody too: setting only the transform let the interpolated body report its old
            // position for a frame, which put the first drive-in path point back at the previous bay.
            car.Teleport(p, r);
            car.MoveExternally(p, r, 0f, 0f);
            Physics.SyncTransforms();

            drivePath.positionCount = 0;
            parkPath.positionCount = 0;
            target = -1;
            hasResult = false;
        }

        // ------------------------------------------------------------------ phases

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.dKey.wasPressedThisFrame && batchRunning) debugMode = !debugMode;
            if (batchRunning)
            {
                if (kb != null && (kb.bKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)) batchRunning = false;   // stops after this run
                return;
            }
            HandleKeys(kb);                              // ShowcaseUI: menus, steps, shortcuts
        }

        void FixedUpdate()
        {
            if (phase == Phase.Driving && Time.time >= driveHoldUntil) DriveStep(Time.fixedDeltaTime);
            if (phase == Phase.Driving) AddPoint(drivePath);
            if (phase == Phase.Parking) AddPoint(parkPath);
            if (phase == Phase.Parking)
            {
                TraceStep();
                float cum = agent.GetCumulativeReward();
                stepReward = cum - lastCumulative;
                lastCumulative = cum;
                Push(speedHistory, car.Speed);
                Push(rewardHistory, cum);
            }
        }

        static void Push(List<float> h, float v)
        {
            h.Add(v);
            if (h.Count > HistoryLength) h.RemoveAt(0);
        }

        void LateUpdate()
        {
            UpdateRays();
            UpdateUI();                                  // ShowcaseUI: screens, live values, photo hover and clicks
            if (!viewCamera) return;
            if (phase == Phase.Menu)
            {
                float a = Time.unscaledTime * 6f * Mathf.Deg2Rad;
                Vector3 c = env.transform.position;
                viewCamera.transform.position = c + new Vector3(Mathf.Cos(a) * 55f, 32f, Mathf.Sin(a) * 55f);
                viewCamera.transform.LookAt(c);
            }
            else if (phase == Phase.Arrive || phase == Phase.Driving)
            {
                // Chase camera behind and above the car.
                Vector3 back = -car.transform.forward;
                Vector3 goal = car.transform.position + back * 9f + Vector3.up * 5f;
                viewCamera.transform.position = snapCamera ? goal
                    : Vector3.Lerp(viewCamera.transform.position, goal, 1f - Mathf.Exp(-4f * Time.deltaTime));
                snapCamera = false;
                viewCamera.transform.LookAt(car.transform.position + Vector3.up * 1f);
            }
            else if ((phase == Phase.Parking || phase == Phase.Done) && target >= 0)
            {
                // Elevated view of the target bay and the manoeuvre.
                var t = env.spots[target];
                Vector3 goal = t.Center - t.Forward * 12f + Vector3.up * 14f;
                viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, goal, 1f - Mathf.Exp(-2f * Time.deltaTime));
                viewCamera.transform.LookAt(t.Center - t.Forward * 3f);
            }
        }

        // ------------------------------------------------------------------ 1-2: capture and occupancy

        void Capture()
        {
            RenderPhotoAndQuads();
            occupiedProb = ClassifyBays();
            DrawPhoto();
            phase = Phase.Photo;
            int free = Enumerable.Range(0, quads.Count).Count(i => quads[i] != null && occupiedProb[i] < occupiedThreshold);
            int seen = quads.Count(q => q != null);
            status = $"CCTV photo: {seen} bays, {free} free ({(usedCnn ? "occupancy CNN" : "ground truth, no CNN assigned")}). Click a free (green) bay, or Esc / C to cancel.";
        }

        /// <summary>Render the CCTV photo and project every bay's ground corners into it.</summary>
        void RenderPhotoAndQuads()
        {
            foreach (var l in rayLines) l.positionCount = 0;      // debug rays must not appear in the photo
            var rt = new RenderTexture(photoWidth, photoHeight, 24);
            var old = cctvCamera.targetTexture;
            cctvCamera.targetTexture = rt;
            cctvCamera.Render();
            RenderTexture.active = rt;
            photo = new Texture2D(photoWidth, photoHeight, TextureFormat.RGB24, false);
            photo.ReadPixels(new Rect(0, 0, photoWidth, photoHeight), 0, 0);
            photo.Apply();
            RenderTexture.active = null;

            // Bay outlines in the photo: project each bay's ground corners (open end first, then back).
            quads.Clear();
            foreach (var s in env.spots)
            {
                var c = new Vector2[4];
                bool visible = true;
                var local = new[]
                {
                    new Vector3(-s.size.x / 2f, 0f, -s.size.y / 2f), new Vector3(s.size.x / 2f, 0f, -s.size.y / 2f),
                    new Vector3(s.size.x / 2f, 0f, s.size.y / 2f), new Vector3(-s.size.x / 2f, 0f, s.size.y / 2f),
                };
                for (int i = 0; i < 4; i++)
                {
                    Vector3 sp = cctvCamera.WorldToScreenPoint(s.transform.TransformPoint(local[i]));
                    if (sp.z <= 0f || sp.x < 0 || sp.y < 0 || sp.x >= photoWidth || sp.y >= photoHeight) visible = false;
                    c[i] = new Vector2(sp.x, photoHeight - sp.y);
                }
                quads.Add(visible ? c : null);
            }
            cctvCamera.targetTexture = old;
            rt.Release();
        }

        /// <summary>Crop box of a bay in the photo, as Vision/geometry.py crop_box (lifted 35 %, padded 10 %).</summary>
        static Rect CropBox(Vector2[] q)
        {
            float x0 = q.Min(v => v.x), x1 = q.Max(v => v.x), y0 = q.Min(v => v.y), y1 = q.Max(v => v.y);
            float w = x1 - x0, h = y1 - y0;
            return Rect.MinMaxRect(x0 - 0.1f * w, y0 - 0.45f * h, x1 + 0.1f * w, y1 + 0.1f * h);
        }

        // ------------------------------------------------------------------ synthetic CNN data (G)

        System.Collections.IEnumerator GenerateSynthetic()
        {
            phase = Phase.Generating;
            string root = OutputPath(syntheticFolder);
            foreach (var cls in new[] { "free", "occupied" }) System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, cls));
            var light = FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
            Vector3 camPos = cctvCamera.transform.position;
            Quaternion camRot = cctvCamera.transform.rotation;
            float camFov = cctvCamera.fieldOfView, lightIntensity = light ? light.intensity : 1f;
            Quaternion lightRot = light ? light.transform.rotation : Quaternion.identity;
            var block = new MaterialPropertyBlock();
            Vector3 centre = env.transform.position;
            int saved = 0, free = 0;
            string run = System.DateTime.Now.ToString("MMdd-HHmmss");
            var labels = new System.Text.StringBuilder();

            for (int n = 0; n < syntheticLayouts; n++)
            {
                // Random occupancy share, car colours, camera pose and light.
                float share = Mathf.Lerp(0.1f, 0.9f, (float)rng.NextDouble());
                foreach (var spot in env.spots)
                {
                    spot.SetState(rng.NextDouble() < share, false);
                    if (spot.parkedCar && spot.IsOccupied)
                    {
                        Color c = Color.HSVToRGB((float)rng.NextDouble(), Mathf.Lerp(0f, 0.8f, (float)rng.NextDouble()), Mathf.Lerp(0.15f, 0.95f, (float)rng.NextDouble()));
                        foreach (var r in spot.parkedCar.GetComponentsInChildren<Renderer>())
                        {
                            r.GetPropertyBlock(block);
                            block.SetColor("_BaseColor", c);
                            block.SetColor("_Color", c);
                            r.SetPropertyBlock(block);
                        }
                    }
                }
                Vector3 jitter = new Vector3(R(-8f, 8f), R(-8f, 6f), R(-6f, 6f));
                cctvCamera.transform.position = camPos + jitter;
                cctvCamera.transform.LookAt(centre + new Vector3(R(-6f, 6f), 0f, R(-4f, 8f)));
                cctvCamera.fieldOfView = camFov + R(-10f, 10f);
                if (light)
                {
                    light.intensity = lightIntensity * R(0.5f, 1.3f);
                    light.transform.rotation = lightRot * Quaternion.Euler(R(-20f, 20f), R(-60f, 60f), 0f);
                }
                Physics.SyncTransforms();
                yield return null;                       // let renderers pick up the changes

                RenderPhotoAndQuads();
                for (int i = 0; i < quads.Count; i++)
                {
                    if (quads[i] == null) continue;
                    Rect box = CropBox(quads[i]);
                    var patch = new Texture2D(syntheticPatchSize, syntheticPatchSize, TextureFormat.RGB24, false);
                    for (int yy = 0; yy < syntheticPatchSize; yy++)
                        for (int xx = 0; xx < syntheticPatchSize; xx++)
                        {
                            float px = box.xMin + (xx + 0.5f) / syntheticPatchSize * box.width;
                            float py = box.yMin + (yy + 0.5f) / syntheticPatchSize * box.height;
                            patch.SetPixel(xx, syntheticPatchSize - 1 - yy, photo.GetPixelBilinear(px / photoWidth, 1f - py / photoHeight));
                        }
                    patch.Apply();
                    bool occ = env.spots[i].IsOccupied;
                    string rel = $"{(occ ? "occupied" : "free")}/{run}_{n:000}_{i:00}.png";
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(root, rel), patch.EncodeToPNG());
                    Destroy(patch);
                    labels.Append(rel).Append(' ').Append(occ ? 1 : 0).Append(' ').Append(n).Append('\n');
                    saved++;
                    if (!occ) free++;
                }
                status = $"Generating CNN training data: layout {n + 1}/{syntheticLayouts}, {saved} patches ({free} free).";
            }
            System.IO.File.AppendAllText(System.IO.Path.Combine(root, "labels.txt"), labels.ToString());

            cctvCamera.transform.SetPositionAndRotation(camPos, camRot);
            cctvCamera.fieldOfView = camFov;
            if (light) { light.intensity = lightIntensity; light.transform.rotation = lightRot; }
            Debug.Log($"[Showcase] Saved {saved} synthetic patches ({free} free, {saved - free} occupied) to {root}", this);
            Restart();
            status = $"Saved {saved} CNN training patches ({free} free) to {syntheticFolder}. " + status;
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        float[] ClassifyBays()
        {
            var prob = new float[quads.Count];
            usedCnn = false;
            if (!occupancyCnn)
            {
                for (int i = 0; i < prob.Length; i++) prob[i] = env.spots[i].IsOccupied ? 1f : 0f;
                return prob;
            }
            var visible = Enumerable.Range(0, quads.Count).Where(i => quads[i] != null).ToList();
            if (visible.Count == 0) return prob;
            const int S = 64;
            var data = new float[visible.Count * 3 * S * S];
            for (int k = 0; k < visible.Count; k++)
            {
                // Crop as in Vision/geometry.py crop_box: box around the bay, lifted 35 % (cars stand above their footprint).
                Rect box = CropBox(quads[visible[k]]);
                float x0 = box.xMin, x1 = box.xMax, y0 = box.yMin, y1 = box.yMax;
                for (int yy = 0; yy < S; yy++)
                    for (int xx = 0; xx < S; xx++)
                    {
                        float px = x0 + (xx + 0.5f) / S * (x1 - x0), py = y0 + (yy + 0.5f) / S * (y1 - y0);
                        Color col = photo.GetPixelBilinear(px / photoWidth, 1f - py / photoHeight);
                        int o = k * 3 * S * S + yy * S + xx;
                        data[o] = (col.r - 0.5f) / 0.5f;
                        data[o + S * S] = (col.g - 0.5f) / 0.5f;
                        data[o + 2 * S * S] = (col.b - 0.5f) / 0.5f;
                    }
            }
            using (var worker = new Worker(ModelLoader.Load(occupancyCnn), BackendType.CPU))
            using (var input = new Tensor<float>(new TensorShape(visible.Count, 3, S, S), data))
            {
                worker.Schedule(input);
                var output = worker.PeekOutput() as Tensor<float>;
                var logits = output.DownloadToArray();
                for (int k = 0; k < visible.Count; k++)
                {
                    float a = logits[2 * k], b = logits[2 * k + 1], m = Mathf.Max(a, b);
                    float ea = Mathf.Exp(a - m), eb = Mathf.Exp(b - m);
                    prob[visible[k]] = eb / (ea + eb);
                }
            }
            usedCnn = true;
            return prob;
        }

        void DrawPhoto()
        {
            photoShown = new Texture2D(photoWidth, photoHeight, TextureFormat.RGB24, false);
            photoShown.SetPixels(photo.GetPixels());
            for (int i = 0; i < quads.Count; i++)
            {
                if (quads[i] == null) continue;
                Color c = i == target ? Color.yellow : occupiedProb[i] >= occupiedThreshold ? new Color(0.9f, 0.15f, 0.15f) : new Color(0.2f, 0.85f, 0.25f);
                for (int e = 0; e < 4; e++) Line(photoShown, quads[i][e], quads[i][(e + 1) % 4], c, e == 0 ? 3 : 2);
            }
            photoShown.Apply();
        }

        static void Line(Texture2D tex, Vector2 a, Vector2 b, Color c, int thickness)
        {
            int n = Mathf.CeilToInt(Vector2.Distance(a, b));
            for (int i = 0; i <= n; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, n == 0 ? 0f : (float)i / n);
                for (int dx = -thickness / 2; dx <= thickness / 2; dx++)
                    for (int dy = -thickness / 2; dy <= thickness / 2; dy++)
                    {
                        int x = Mathf.RoundToInt(p.x) + dx, y = tex.height - 1 - (Mathf.RoundToInt(p.y) + dy);
                        if (x >= 0 && y >= 0 && x < tex.width && y < tex.height) tex.SetPixel(x, y, c);
                    }
            }
        }

        /// <summary>Close the photo without choosing a bay: back outside, same car park.</summary>
        void CancelPhoto()
        {
            photoShown = null;
            target = -1;
            phase = Phase.Arrive;
            status = ArriveText;
        }

        /// <summary>Index of the bay under a point of the photo (photo pixels), or -1.</summary>
        int BayAt(Vector2 px)
        {
            for (int i = 0; i < quads.Count; i++)
                if (quads[i] != null && Inside(quads[i], px)) return i;
            return -1;
        }

        /// <summary>Why a bay cannot be chosen, or null if it can.</summary>
        string RejectReason(int i)
        {
            if (occupiedProb[i] >= occupiedThreshold) return "That bay is taken. Choose a green bay.";
            return null;
        }

        /// <summary>Start parking in a chosen (free) bay: drive in, then the RL agent parks.</summary>
        void ParkIn(int i)
        {
            ApplyModel();
            target = i;
            cnnInfo = CnnCheck();
            DrawPhoto();
            StartDrive();
        }

        static bool Inside(Vector2[] q, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = q.Length - 1; i < q.Length; j = i++)
                if ((q[i].y > p.y) != (q[j].y > p.y) && p.x < (q[j].x - q[i].x) * (p.y - q[i].y) / (q[j].y - q[i].y) + q[i].x)
                    inside = !inside;
            return inside;
        }

        // ------------------------------------------------------------------ 3: drive-in

        void StartDrive()
        {
            // Route in the area's local frame: entrance, along the central aisle, up a side corridor if the bay is on a
            // north / south aisle, then along that aisle towards the bay. Hand-over happens on the way (handoverDistance).
            var t = env.spots[target];
            Vector3 L(Vector3 world) => env.transform.InverseTransformPoint(world);
            Vector3 W(float x, float z) => env.transform.TransformPoint(new Vector3(x, 0f, z));
            Vector3 tl = L(t.Center);
            Vector3 fwd = env.transform.InverseTransformDirection(t.Forward);
            float aisleZ = (tl - fwd * (t.size.y / 2f + 3.4f)).z;             // the target's aisle centre-line
            float corridorX = tl.x < 0f ? -5.8f : 5.8f;                         // between the side bays and the middle bays

            route.Clear();
            route.Add(car.transform.position);
            if (!startedAtEntrance) foreach (var a in approachPoints) if (a) route.Add(a.position);   // town roads
            route.Add(entrancePoint ? entrancePoint.position : W(-21f, 0f));
            if (handoverAtCentre)
                route.Add(W(0f, 0f));                                           // the agent parks from the centre
            else if (Mathf.Abs(fwd.x) > Mathf.Abs(fwd.z))
            {
                // Sideways bay (the middle column, opening east or west): its aisle is the north-south corridor in front of
                // its open end. Turn into that corridor from the central aisle and drive along it towards the bay (and past
                // it if needed); the hand-over rules below are in the bay's own frame, so they apply unchanged.
                float corridor = (tl - fwd * (t.size.y / 2f + 3.4f)).x;
                float dirZ = tl.z >= 0f ? 1f : -1f;
                float endZ = Mathf.Clamp(tl.z + dirZ * (minHandoverLateral + 3f), -env.areaHalfExtents.y + 3f, env.areaHalfExtents.y - 3f);
                route.Add(W(corridor, 0f));
                route.Add(W(corridor, endZ));
            }
            else
            {
                // Along the target's aisle and on past the bay, so a bay close to where the car enters the aisle still
                // gets a start at least minHandoverLateral to its side (the car hands over beyond it instead).
                bool central = Mathf.Abs(aisleZ) < 4f;
                float entryX = central ? -env.areaHalfExtents.x : corridorX;
                float dir = tl.x >= entryX ? 1f : -1f;
                float endX = Mathf.Clamp(tl.x + dir * (minHandoverLateral + 3f), -env.areaHalfExtents.x + 3f, env.areaHalfExtents.x - 3f);
                if (!central)
                {
                    route.Add(W(corridorX, 0f));
                    route.Add(W(corridorX, aisleZ));
                }
                route.Add(W(endX, central ? 0f : aisleZ));
            }
            for (int i = 0; i < route.Count; i++) route[i] = new Vector3(route[i].x, car.transform.position.y, route[i].z);
            RoundCorners(route, cornerRadius);
            routeLength = 0f;
            for (int i = 1; i < route.Count; i++) routeLength += Vector3.Distance(route[i - 1], route[i]);
            routeIndex = 1;
            speedNow = 0f;
            phase = Phase.Driving;
            status = handoverAtCentre
                ? $"Driving to the centre of the car park (scripted route); the RL agent then parks in bay {target}."
                : $"Driving to bay {target} (scripted route); the RL agent takes over in the bay's aisle, lined up and at least {minHandoverLateral:F0} m to its side.";
        }

        /// <summary>
        /// Replace every corner of the route by a circular arc of the given radius (smaller if the legs are short), so the
        /// car follows real turns instead of cutting corners: it enters the car park straight, along the central aisle.
        /// </summary>
        static void RoundCorners(List<Vector3> pts, float radius)
        {
            // Drop points on a straight line first, so a waypoint just after a corner does not shrink its turn.
            for (int i = pts.Count - 2; i >= 1; i--)
                if (Vector3.Angle(pts[i] - pts[i - 1], pts[i + 1] - pts[i]) < 3f) pts.RemoveAt(i);
            var result = new List<Vector3> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector3 a = result[result.Count - 1], b = pts[i], c = pts[i + 1];
                Vector3 d1 = a - b, d2 = c - b;
                float l1 = d1.magnitude, l2 = d2.magnitude;
                float phi = Vector3.Angle(d1, d2) * Mathf.Deg2Rad;          // interior angle at the corner
                if (l1 < 0.1f || l2 < 0.1f || phi > Mathf.PI * 0.97f) { result.Add(b); continue; }
                d1 /= l1; d2 /= l2;
                float t = Mathf.Min(radius / Mathf.Tan(phi / 2f), 0.45f * l1, 0.45f * l2);
                float r = t * Mathf.Tan(phi / 2f);
                Vector3 p1 = b + d1 * t, p2 = b + d2 * t;
                Vector3 centre = b + (d1 + d2).normalized * (r / Mathf.Sin(phi / 2f));
                Vector3 from = p1 - centre, to = p2 - centre;
                float sweep = Vector3.SignedAngle(from, to, Vector3.up);
                int n = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(sweep) * Mathf.Deg2Rad * r / 1f));
                for (int k = 0; k <= n; k++) result.Add(centre + Quaternion.Euler(0f, sweep * k / n, 0f) * from);
            }
            result.Add(pts[pts.Count - 1]);
            pts.Clear();
            pts.AddRange(result);
        }

        void DriveStep(float dt)
        {
            Vector3 pos = car.transform.position;
            // Pure pursuit: aim at a point 3 m ahead along the route.
            while (routeIndex < route.Count - 1 && Vector3.Distance(pos, route[routeIndex]) < 3f) routeIndex++;
            Vector3 aim = route[routeIndex];
            Vector3 to = Vector3.ProjectOnPlane(aim - pos, Vector3.up);
            float angle = Vector3.SignedAngle(car.transform.forward, to, Vector3.up);
            float targetSpeed = driveSpeed * Mathf.Clamp(1f - Mathf.Abs(angle) / 120f, 0.35f, 1f);
            if (routeIndex == route.Count - 1)                                  // ease to a stop at the end of the route
                targetSpeed = Mathf.Min(targetSpeed, 0.3f + 0.6f * Vector3.Distance(pos, route[routeIndex]));
            speedNow = Mathf.MoveTowards(speedNow, targetSpeed, 3f * dt);
            // Bicycle model: the yaw rate is limited by the maximum steering angle.
            float maxYawRate = speedNow / car.wheelBase * Mathf.Tan(car.maxSteerAngle * Mathf.Deg2Rad) * Mathf.Rad2Deg;
            float yaw = Mathf.Clamp(angle * 2f, -maxYawRate, maxYawRate) * dt;
            Quaternion rot = Quaternion.Euler(0f, car.transform.eulerAngles.y + yaw, 0f);
            Vector3 next = pos + rot * Vector3.forward * speedNow * dt;
            float steer = Mathf.Atan(yaw / dt * Mathf.Deg2Rad * car.wheelBase / Mathf.Max(speedNow, 0.1f)) * Mathf.Rad2Deg;
            car.MoveExternally(next, rot, speedNow, steer);

            // Hand over within handoverDistance, but only on the last leg and once the car is lined up with the aisle:
            // a car still turning in from a side corridor would hand the agent a diagonal start right against the row.
            bool inside = env.IsInsideArea(next, 2.5f);
            bool lastLeg = routeIndex == route.Count - 1;
            Vector3 leg = Vector3.ProjectOnPlane(route[route.Count - 1] - route[route.Count - 2], Vector3.up);
            bool lined = leg.sqrMagnitude < 0.01f || Vector3.Angle(rot * Vector3.forward, leg) <= handoverMaxAngle;
            // ...and at least minHandoverLateral to the side of the bay: closer, the agent tries to swing straight in and
            // has no room (an early showcase batch: 3.6 m to the side of a bay next to the entrance, 2 collisions in 3 runs).
            bool wide = Mathf.Abs(env.spots[target].transform.InverseTransformPoint(next).x) >= minHandoverLateral;
            if (!handoverAtCentre && inside && lastLeg && lined && wide && Vector3.Distance(next, env.spots[target].Center) <= handoverDistance) HandOver();
            else if (routeIndex == route.Count - 1 && Vector3.Distance(next, route[routeIndex]) < 0.5f) HandOver();
        }

        // ------------------------------------------------------------------ 4: RL parks

        void HandOver()
        {
            car.SetExternalControl(false);
            var scenario = new ParkingEnvironment.Scenario
            {
                targetIndex = target,
                hasStartPose = true,
                startLocalPosition = env.transform.InverseTransformPoint(car.transform.position),
                startYaw = car.transform.eulerAngles.y - env.transform.eulerAngles.y,
            };
            for (int i = 0; i < env.spots.Count; i++)
                if (i != target && env.spots[i].IsOccupied) scenario.occupied.Add(i);
            env.ActiveScenario = scenario;
            speedHistory.Clear();
            rewardHistory.Clear();
            StartTrace(scenario);
            lastCumulative = stepReward = 0f;
            env.HoldReset = false;
            agent.Paused = false;
            agent.EndEpisode();                         // -> OnEpisodeBegin -> the scenario (same pose, same parked cars);
            phase = Phase.Parking;                      // set after: EndEpisode reports the paused episode as a timeout
            env.spots[target].SetIndicatorVisible(true); // the target outline (yellow) from now on
            status = $"RL agent parking in bay {target}...";
        }

        void OnEpisodeFinished(ParkingAgent a, ParkingAgent.EpisodeResult r)
        {
            if (phase != Phase.Parking || r.steps == 0) return;   // 0 steps: ML-Agents' own first-episode report
            phase = Phase.Done;
            env.HoldReset = true;
            agent.Paused = true;
            car.SetExternalControl(true);               // hold the car: with no reset, it would keep the policy's last throttle and steering
            AddPoint(parkPath);
            float seconds = r.steps * Time.fixedDeltaTime;
            status = $"{r.outcome} in {seconds:F1} s  |  {r.finalDistance:F2} m from the bay centre  |  heading error {r.finalHeadingError:F1} deg.   R = new car park";
            ShowResult(r, seconds);                      // ShowcaseUI: result screen, outline colour, flash
            LogRun(r, seconds);
            if (batchRunning)
                batchResults.Add((env.spots[target].name, env.spots[target].Group.ToString(), r.outcome.ToString(), r.collidedWith ?? "",
                                  seconds, lastHandoverDistance));
        }

        // ------------------------------------------------------------------ batch test (B)

        System.Collections.IEnumerator BatchTest()
        {
            batchRunning = true;
            handoverAtCentre = settingCentre;            // the hand-over chosen in Settings, not the one Try again last switched to
            ApplyModel();                                // the model chosen in Settings
            batchResults.Clear();
            batchId = System.DateTime.Now.ToString("MMdd-HHmmss");
            float oldScale = Time.timeScale;
            Time.timeScale = batchTimeScale;
            int n = 0;
            while (batchRunning && n < batchRuns)
            {
                Restart();
                yield return null;
                RenderPhotoAndQuads();
                occupiedProb = ClassifyBays();
                // Bays the CNN calls free (rows and sideways bays), as a user would pick from the photo.
                var free = Enumerable.Range(0, quads.Count)
                    .Where(i => quads[i] != null && occupiedProb[i] < occupiedThreshold).ToList();
                if (free.Count == 0) continue;
                target = free[rng.Next(free.Count)];
                cnnInfo = CnnCheck();
                n++;
                StartDrive();
                status = $"Batch {batchId}: run {n}/{batchRuns}, bay {env.spots[target].name}   (B or Esc = stop after this run)";
                float limit = Time.realtimeSinceStartup + 180f;
                while (phase != Phase.Done && Time.realtimeSinceStartup < limit) yield return null;
                if (phase != Phase.Done) { Debug.LogWarning($"[Showcase] Batch run {n} did not finish; skipped.", this); continue; }
                yield return null;                       // let the end screenshot be taken
                yield return null;
            }
            Time.timeScale = oldScale;
            batchRunning = false;
            WriteBatchSummary();
            Restart();
            status = batchSummary;
        }

        string batchSummary = "";

        void WriteBatchSummary()
        {
            int total = batchResults.Count;
            if (total == 0) { batchSummary = "Batch: no runs."; return; }
            int parked = batchResults.Count(b => b.outcome == "Parked");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Batch {batchId}: {parked}/{total} parked ({100f * parked / total:F0}%), policy {(ActiveModel ? ActiveModel.name : "")}, hand-over {(handoverAtCentre ? "centre" : "aisle")}");
            foreach (var g in batchResults.GroupBy(b => b.group))
                sb.AppendLine($"  {g.Key}: {g.Count(b => b.outcome == "Parked")}/{g.Count()} parked");
            foreach (var g in batchResults.Where(b => b.outcome != "Parked").GroupBy(b => b.outcome + (b.hit != "" ? " (" + b.hit + ")" : "")))
                sb.AppendLine($"  {g.Key}: {g.Count()}  bays {string.Join(" ", g.Select(b => b.bay))}");
            var ok = batchResults.Where(b => b.outcome == "Parked").ToList();
            if (ok.Count > 0) sb.AppendLine($"  parked in {ok.Average(b => b.seconds):F1} s on average");
            sb.AppendLine($"  hand-over distance: parked {(ok.Count > 0 ? ok.Average(b => b.handover) : 0f):F1} m, failed {(total > parked ? batchResults.Where(b => b.outcome != "Parked").Average(b => b.handover) : 0f):F1} m on average");
            batchSummary = sb.ToString().Split('\n')[0] + "   (summary in " + logFolder + "/batch_" + batchId + ".txt)";
            try
            {
                System.IO.Directory.CreateDirectory(LogRoot);
                System.IO.File.WriteAllText(System.IO.Path.Combine(LogRoot, $"batch_{batchId}.txt"), sb.ToString());
            }
            catch (System.Exception e) { Debug.LogWarning($"[Showcase] Could not write the batch summary: {e.Message}", this); }
            Debug.Log("[Showcase] " + sb, this);
        }

        // ------------------------------------------------------------------ run log (Scenarios/showcase)

        string LogRoot => OutputPath(logFolder);

        /// <summary>
        /// Where files are written: the project folder in the editor, the folder next to the .exe in a Windows build. A macOS
        /// build uses Application.persistentDataPath (~/Library/Application Support/&lt;company&gt;/&lt;product&gt;), because the .app bundle
        /// is often read-only (Gatekeeper moves downloaded apps to a read-only location) and writing into it breaks its signature.
        /// </summary>
        static string OutputPath(string relative)
        {
            string root = Application.platform == RuntimePlatform.OSXPlayer
                ? Application.persistentDataPath
                : System.IO.Path.Combine(Application.dataPath, "..");
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
        }

        /// <summary>CNN vs the scene's true occupancy over the visible bays: "visible correct falseFree falseOccupied pTarget".</summary>
        string CnnCheck()
        {
            int seen = 0, correct = 0, falseFree = 0, falseOcc = 0;
            for (int i = 0; i < quads.Count; i++)
            {
                if (quads[i] == null) continue;
                bool predOcc = occupiedProb[i] >= occupiedThreshold, occ = env.spots[i].IsOccupied;
                seen++;
                if (predOcc == occ) correct++;
                else if (occ) falseFree++;
                else falseOcc++;
            }
            return $"{seen},{correct},{falseFree},{falseOcc},{occupiedProb[target]:F3}";
        }

        void StartTrace(ParkingEnvironment.Scenario scenario)
        {
            lastHandoverDistance = Vector3.Distance(car.transform.position, env.spots[target].Center);
            if (!logRuns) return;
            runNumber++;
            runId = $"{System.DateTime.Now:MMdd-HHmmss}_bay{target}";
            var t = env.spots[target];
            Vector3 local = scenario.startLocalPosition;
            handoverInfo = $"{local.x:F2},{local.z:F2},{Mathf.DeltaAngle(0f, scenario.startYaw):F1},{lastHandoverDistance:F2}";
            trace = new System.Text.StringBuilder();
            trace.Append("step,time,x,z,yaw,speed,steer_cmd,throttle_cmd,steer_angle,");
            for (int i = 0; i < ObservationNames.Length; i++) trace.Append($"obs{i},");
            trace.Append("distance,heading_error,depth,lateral,corners,slow,aligned,parked_hold,potential,cumulative_reward,nearest_car_ray,nearest_wall_ray\n");
        }

        void TraceStep()
        {
            if (trace == null) return;
            var tr = agent.GetTrace();
            var k = agent.GetDebugCounters();
            Vector3 lp = env.transform.InverseTransformPoint(car.transform.position);
            float yaw = Mathf.DeltaAngle(0f, car.transform.eulerAngles.y - env.transform.eulerAngles.y);
            NearestRays(out float nearCar, out float nearWall);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            trace.Append(string.Format(inv, "{0},{1:F2},{2:F3},{3:F3},{4:F1},{5:F3},{6:F3},{7:F3},{8:F1},",
                k.steps, Time.time, lp.x, lp.z, yaw, car.Speed, car.SteerInput, car.ThrottleInput, car.SteerAngle));
            var obs = agent.GetObservations();
            for (int i = 0; i < ObservationNames.Length; i++) trace.Append((i < obs.Count ? obs[i] : 0f).ToString("F4", inv)).Append(',');
            trace.Append(string.Format(inv, "{0:F3},{1:F2},{2:F3},{3:F3},{4},{5},{6},{7},{8:F4},{9:F4},{10:F2},{11:F2}\n",
                tr.distance, tr.headingError, tr.depth, tr.lateral, tr.corners, tr.slowEnough ? 1 : 0, tr.alignedEnough ? 1 : 0,
                k.parkedHold, tr.potential, agent.GetCumulativeReward(), nearCar, nearWall));
        }

        /// <summary>Nearest parked car and wall seen by the ray sensor (m, -1 = none in range).</summary>
        void NearestRays(out float nearCar, out float nearWall)
        {
            nearCar = nearWall = -1f;
            if (raySensors == null) return;
            foreach (var sensor in raySensors)
            {
                var input = sensor.GetRayPerceptionInput();
                foreach (var ray in RayPerceptionSensor.Perceive(input, false).RayOutputs)
                {
                    if (!ray.HasHit || ray.HitTagIndex < 0) continue;
                    float d = ray.HitFraction * input.RayLength;
                    string tag = input.DetectableTags[ray.HitTagIndex];
                    if (tag == "ObstacleCar" && (nearCar < 0f || d < nearCar)) nearCar = d;
                    if (tag == "Wall" && (nearWall < 0f || d < nearWall)) nearWall = d;
                }
            }
        }

        void LogRun(ParkingAgent.EpisodeResult r, float seconds)
        {
            if (!logRuns || trace == null) return;
            try
            {
                string root = LogRoot;
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "runs"));
                System.IO.File.WriteAllText(System.IO.Path.Combine(root, "runs", runId + ".csv"), trace.ToString());
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(root, "runs", runId + ".png"));

                string results = System.IO.Path.Combine(root, "results.csv");
                if (System.IO.File.Exists(results) && !System.IO.File.ReadLines(results).First().EndsWith(",batch"))
                    System.IO.File.Move(results, System.IO.Path.Combine(root, $"results_old_{System.DateTime.Now:MMdd-HHmmss}.csv"));   // older column layout
                if (!System.IO.File.Exists(results))
                    System.IO.File.WriteAllText(results,
                        "run,time,policy,handover_mode,target,bay,group,occupied_neighbours,occupied_bays," +
                        "handover_x,handover_z,handover_yaw,handover_distance," +
                        "outcome,collided_with,seconds,final_distance,final_heading_error,nose_in,approach,entries,soft_contacts,cumulative_reward," +
                        "cnn_used,cnn_visible,cnn_correct,cnn_false_free,cnn_false_occupied,cnn_p_target,batch\n");
                var t = env.spots[target];
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                string row = string.Join(",", new[]
                {
                    runId, System.DateTime.Now.ToString("s"), ActiveModel ? ActiveModel.name : "", handoverAtCentre ? "centre" : "aisle",
                    target.ToString(), t.name, t.Group.ToString(), env.TargetOccupiedNeighbours.ToString(),
                    env.spots.Count(sp => sp.IsOccupied).ToString(),
                    handoverInfo,
                    r.outcome.ToString(), r.collidedWith ?? "", seconds.ToString("F2", inv), r.finalDistance.ToString("F3", inv),
                    r.finalHeadingError.ToString("F2", inv), r.noseIn ? "1" : "0", r.approach ?? "", r.entries.ToString(),
                    r.softContacts.ToString(), r.cumulativeReward.ToString("F3", inv),
                    usedCnn ? "1" : "0", cnnInfo, batchRunning ? batchId : "",
                });
                System.IO.File.AppendAllText(results, row + "\n");
                status += $"   (logged {runId})";
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Showcase] Could not write the run log: {e.Message}", this);
            }
            trace = null;
        }

        // ------------------------------------------------------------------ visuals

        LineRenderer MakeLine(string name, Color c)
        {
            var lr = new GameObject(name).AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.25f;
            lr.numCornerVertices = 4;
            lr.positionCount = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = env.indicatorMaterial ? new Material(env.indicatorMaterial) : shader ? new Material(shader) : null;
            if (mat) lr.sharedMaterial = mat;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            lr.SetPropertyBlock(block);
            return lr;
        }

        void AddPoint(LineRenderer lr)
        {
            Vector3 p = car.transform.position + Vector3.up * 0.3f;
            int n = lr.positionCount;
            if (n > 0 && (lr.GetPosition(n - 1) - p).sqrMagnitude < 0.04f) return;
            lr.positionCount = n + 1;
            lr.SetPosition(n, p);
        }

        // ------------------------------------------------------------------ debug overlay (D)

        /// <summary>Re-cast the agent's ray sensor (the same input the policy gets) and draw each ray, coloured by what it hits.</summary>
        void UpdateRays()
        {
            rayHits.Clear();
            rayCount = 0;
            bool show = debugMode && raySensors != null && phase != Phase.Photo && phase != Phase.Generating;
            if (show)
            {
                foreach (var sensor in raySensors)
                {
                    var input = sensor.GetRayPerceptionInput();
                    var output = RayPerceptionSensor.Perceive(input, false);
                    for (int i = 0; i < output.RayOutputs.Length; i++)
                    {
                        var ray = output.RayOutputs[i];
                        if (rayLines.Count <= rayCount)
                        {
                            var lr = MakeLine($"DebugRay {rayLines.Count}", Color.green);
                            lr.widthMultiplier = 0.06f;
                            lr.numCornerVertices = 0;
                            rayLines.Add(lr);
                        }
                        var line = rayLines[rayCount++];
                        Vector3 end = ray.HasHit ? Vector3.Lerp(ray.StartPositionWorld, ray.EndPositionWorld, ray.HitFraction) : ray.EndPositionWorld;
                        string tag = ray.HasHit && ray.HitTagIndex >= 0 ? input.DetectableTags[ray.HitTagIndex] : ray.HasHit ? "other" : "";
                        Color c = tag == "ObstacleCar" ? new Color(1f, 0.15f, 0.15f) : tag == "Wall" ? new Color(1f, 0.6f, 0f)
                                : tag != "" ? Color.yellow : new Color(0.3f, 0.9f, 0.4f);
                        line.positionCount = 2;
                        line.SetPosition(0, ray.StartPositionWorld);
                        line.SetPosition(1, end);
                        SetLineColor(line, c);
                        if (ray.HasHit)
                        {
                            float angle = i < input.Angles.Count ? 90f - input.Angles[i] : 0f;   // 0 = straight ahead
                            rayHits.Add((tag, ray.HitFraction * input.RayLength, angle));
                        }
                    }
                }
            }
            for (int i = rayCount; i < rayLines.Count; i++) rayLines[i].positionCount = 0;
        }

        static void SetLineColor(LineRenderer lr, Color c)
        {
            var block = new MaterialPropertyBlock();
            lr.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            lr.SetPropertyBlock(block);
        }

        static readonly string[] ObservationNames =
        {
            "bay to the right", "bay ahead", "heading sin", "heading cos", "depth in bay",
            "forward speed", "yaw rate", "steering angle", "offset from centre-line",
        };

        void DrawDebugPanel()
        {
            const float w = 390f;
            var area = new Rect(Screen.width - w - 10f, 80f, w, Screen.height - 90f);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            var head = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            var text = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            head.normal.textColor = new Color(0.55f, 0.85f, 1f);
            text.normal.textColor = Color.white;
            float x = area.x + 10f, y = area.y + 6f, lw = 150f, bw = w - lw - 30f;

            void Head(string h) { y += 4f; GUI.Label(new Rect(x, y, w, 20f), h, head); y += 20f; }
            void Line(string t) { GUI.Label(new Rect(x, y, w - 20f, 18f), t, text); y += 17f; }
            void Bar(string label, float v, float min, float max, string value)
            {
                GUI.Label(new Rect(x, y, lw, 18f), label, text);
                var r = new Rect(x + lw, y + 4f, bw - 60f, 10f);
                GUI.color = new Color(1f, 1f, 1f, 0.15f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                float zero = Mathf.InverseLerp(min, max, Mathf.Clamp(0f, min, max));
                float at = Mathf.InverseLerp(min, max, Mathf.Clamp(v, min, max));
                GUI.color = v >= 0f ? new Color(0.35f, 0.8f, 1f) : new Color(1f, 0.55f, 0.3f);
                GUI.DrawTexture(new Rect(r.x + r.width * Mathf.Min(zero, at), r.y, Mathf.Max(1f, r.width * Mathf.Abs(at - zero)), r.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(r.xMax + 6f, y - 1f, 70f, 18f), value, text);
                y += 17f;
            }
            string Ok(bool b) => b ? "<color=#7CFF7C>yes</color>" : "<color=#FF7C7C>no</color>";

            var dr = agent.GetComponent<DecisionRequester>();
            Head("Policy");
            Line($"{(ActiveModel ? ActiveModel.name : "no model")}, deterministic, decides every {(dr ? dr.DecisionPeriod : 1)} physics steps");
            Line(agent.Paused ? "<color=#FFD27C>agent paused (scripted drive-in or result held)</color>" : "<color=#7CFF7C>agent in control</color>");

            Head("Decision (last action)");
            Bar("steering command", car.SteerInput, -1f, 1f, $"{car.SteerInput:+0.00;-0.00}");
            Bar("throttle command", car.ThrottleInput, -1f, 1f, $"{car.ThrottleInput:+0.00;-0.00}");
            Line($"wheel angle {car.SteerAngle:+0.0;-0.0} deg   speed {car.Speed:+0.00;-0.00} m/s ({(car.Speed < -0.05f ? "reversing" : car.Speed > 0.05f ? "forward" : "stopped")})");

            Head("Observations (vector inputs)");
            var live = agent.GetObservations();
            if (live.Count > 0 && !agent.Paused) lastObs = live.ToArray();
            var obs = live.Count > 0 && !agent.Paused ? (IReadOnlyList<float>)live : lastObs;
            if (obs.Count == 0) Line("(none yet)");
            for (int i = 0; i < obs.Count && i < ObservationNames.Length; i++) Bar(ObservationNames[i], obs[i], -1f, 1f, $"{obs[i]:+0.00;-0.00}");

            Head($"Ray sensor ({rayCount} rays)");
            Line("<color=#FF4D4D>parked car</color>   <color=#FF9A00>wall</color>   <color=#FFFF00>other</color>   <color=#4DE666>clear</color>");
            foreach (var tag in new[] { "ObstacleCar", "Wall" })
            {
                string name = tag == "ObstacleCar" ? "parked cars" : "walls";
                var hits = rayHits.Where(h => h.tag == tag).ToList();
                if (hits.Count == 0) { Line($"{name}: none in range"); continue; }
                var near = hits.OrderBy(h => h.distance).First();
                Line($"{name}: {hits.Count} rays, nearest {near.distance:F1} m at {near.angle:+0;-0;0} deg");
            }

            var t = agent.GetTrace();
            var k = agent.GetDebugCounters();
            Head("Parked check (all must hold)");
            Line($"corners in bay {t.corners}/4 {Ok(t.corners == 4)}   heading {t.headingError:F1} deg {Ok(t.alignedEnough)}   stopped {Ok(t.slowEnough)}");
            Bar("held for", k.holdSteps > 0 ? (float)k.parkedHold / k.holdSteps : 0f, 0f, 1f, $"{k.parkedHold}/{k.holdSteps}");
            Line($"distance {t.distance:F2} m   depth {t.depth:+0.00;-0.00} m   lateral {t.lateral:+0.00;-0.00} m");

            Head("Reward");
            Line($"cumulative {agent.GetCumulativeReward():F3}   last step {stepReward:+0.0000;-0.0000}");
            Bar("potential", t.potential, -1f, 2f, $"{t.potential:F2}");
            Bar("in-bay score", t.inBay, 0f, 1f, $"{t.inBay:F2}");
            Bar("line-up score", t.lineUp, 0f, 1f, $"{t.lineUp:F2}");
            Bar("stop score", t.stop, 0f, 1f, $"{t.stop:F2}");
            Bar("slow approach", t.approach, 0f, 1f, $"{t.approach:F2}");

            Head("Endings to watch");
            Bar("episode steps", (float)k.steps / Mathf.Max(1, agent.MaxStep), 0f, 1f, $"{k.steps}/{agent.MaxStep}");
            Bar(k.noProgressOn ? "no progress for" : "no progress (off)", (float)k.sinceProgress / Mathf.Max(1, k.noProgressSteps), 0f, 1f, $"{k.sinceProgress}/{k.noProgressSteps}");
            Bar(k.stuckOn ? "not moved for" : "not moved (off)", (float)k.sinceMoved / Mathf.Max(1, k.stuckSteps), 0f, 1f, $"{k.sinceMoved}/{k.stuckSteps}");
            Line($"soft contacts {k.softContacts}");

            Head("History (this parking)");
            Spark(new Rect(x, y, w - 20f, 36f), speedHistory, "speed (m/s)", new Color(0.35f, 0.8f, 1f));
            y += 40f;
            Spark(new Rect(x, y, w - 20f, 36f), rewardHistory, "cumulative reward", new Color(0.5f, 1f, 0.5f));

            void Spark(Rect r, List<float> h, string label, Color c)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.08f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = c;
                if (h.Count > 1)
                {
                    float lo = Mathf.Min(0f, h.Min()), hi = Mathf.Max(0.01f, h.Max());
                    float zeroY = r.yMax - Mathf.InverseLerp(lo, hi, 0f) * r.height;
                    float colW = Mathf.Max(1f, r.width / HistoryLength);
                    for (int i = 0; i < h.Count; i++)
                    {
                        float px = r.x + r.width * i / HistoryLength;
                        float py = r.yMax - Mathf.InverseLerp(lo, hi, h[i]) * r.height;
                        GUI.DrawTexture(new Rect(px, Mathf.Min(py, zeroY), colW, Mathf.Max(1f, Mathf.Abs(zeroY - py))), Texture2D.whiteTexture);
                    }
                }
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 4f, r.y, r.width, 18f), h.Count > 0 ? $"{label}  {h[h.Count - 1]:F2}" : label, text);
            }
        }
    }
}
