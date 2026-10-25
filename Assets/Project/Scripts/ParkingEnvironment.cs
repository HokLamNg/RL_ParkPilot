using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using UnityEngine;

namespace ParkPilot
{
    /// <summary>Difficulty settings for one curriculum stage.</summary>
    [System.Serializable]
    public class CurriculumStage
    {
        public string name;
        [Tooltip("Min/max distance (m) from the bay centre back into the aisle where the car spawns.")]
        public Vector2 spawnDistance = new Vector2(5f, 7f);
        [Tooltip("Max sideways offset (m) of the spawn point along the aisle.")]
        public float lateralOffset = 0.5f;
        [Tooltip("Max heading noise (deg) relative to facing straight into the bay.")]
        public float headingNoise = 10f;
        [Tooltip("Min/max number of occupied bays (never the target).")]
        public Vector2Int occupiedBays = Vector2Int.zero;
        [Tooltip("Pick a random vacant target bay instead of fixedTargetIndex.")]
        public bool randomTarget;
        [Tooltip("With randomTarget: which bay groups may be chosen as the target.")]
        public BayGroup targetGroups = BayGroup.All;
        [Tooltip("Chance per episode that the target's direct neighbours (side by side, same orientation) are " +
                 "all occupied, on top of the random parked cars. Deliberate practice for tight bays.")]
        [Range(0f, 1f)] public float tightBayChance;
        [Tooltip("Training aid: low-speed touches of cars/walls are penalised but don't end the episode " +
                 "(see ParkingAgent soft-contact settings). Turn off for the final stages.")]
        public bool softContact;
    }

    /// <summary>
    /// Lives on the root of the TrainingArea prefab. Everything is placed relative to this
    /// transform, so the prefab can be duplicated 16-32 times in one scene.
    ///
    /// Reads ML-Agents environment parameters each episode:
    ///   curriculum_stage (int index into <see cref="stages"/>)
    ///   sparse_reward    (0 = shaped, 1 = sparse; Experiment B)
    /// </summary>
    public class ParkingEnvironment : MonoBehaviour
    {
        [Header("Scene references")]
        [Tooltip("Bays in this area. Auto-filled from children if left empty.")]
        public List<ParkingSpot> spots = new List<ParkingSpot>();
        [Tooltip("Spawned into bays that have no parkedCar assigned. Its collider must be tagged ObstacleCar.")]
        public GameObject obstacleCarPrefab;
        [Tooltip("Material for the runtime bay indicators (green/red/yellow). Any URP Lit/Unlit material works; " +
                 "its colour is overridden per bay. Leave empty to use Unity's default material.")]
        public Material indicatorMaterial;

        [Header("Area")]
        [Tooltip("Half size (m) of the drivable area in this transform's local X/Z. Leaving it = out of bounds.")]
        public Vector2 areaHalfExtents = new Vector2(15f, 15f);
        [Tooltip("Height (m) above this transform at which the car's pivot is placed on spawn.")]
        public float carSpawnHeight = 0.05f;

        [Header("Curriculum")]
        [Tooltip("Target bay when the stage does not randomise the target.")]
        public int fixedTargetIndex = 0;
        [Tooltip(">= 0 forces a stage (manual testing / evaluation). -1 = use the trainer's curriculum_stage.")]
        public int stageOverride = -1;
        [Tooltip("Used when not connected to the trainer.")]
        public bool sparseRewardDefault = false;
        public CurriculumStage[] stages = DefaultStages();

        [Header("Geometry randomisation (fine-tuning for real car parks, off by default)")]
        [Tooltip("1 = on, 0 = off, -1 = use the trainer's geometry_randomization parameter (default 0 = off).")]
        public int geometryRandomOverride = -1;
        [Tooltip("Target bay width (m). Training bays are 3.2 m; photos scaled to that width still vary with measurement error.")]
        public Vector2 targetWidthRange = new Vector2(2.8f, 3.7f);
        [Tooltip("Target bay length (m), with its open end kept in place. Training bays are 4.88 m.")]
        public Vector2 targetLengthRange = new Vector2(4.6f, 5.4f);
        [Tooltip("Parked cars move this far deeper (+) or out (-) in their bay (m). Real bays are longer, so cars sit deeper.")]
        public Vector2 parkedDepthShift = new Vector2(-0.3f, 1.0f);
        [Tooltip("Parked cars move up to this far sideways in their bay (m).")]
        public float parkedLateralJitter = 0.25f;
        [Tooltip("Parked cars turn up to this many degrees in their bay.")]
        public float parkedYawJitter = 3f;
        [Tooltip("Parked-car width and length scale.")]
        public Vector2 parkedWidthScale = new Vector2(0.9f, 1.05f);
        public Vector2 parkedLengthScale = new Vector2(0.9f, 1.1f);

        [Tooltip("Stuck / no-progress episode endings: 1 = on, 0 = off, -1 = use the trainer's early_endings parameter " +
                 "(default on). ScenarioRunner can switch them off so the agent gets the full time limit.")]
        public int earlyEndingsOverride = -1;

        [Header("Occupancy randomisation (fine-tuning for real car parks, off by default)")]
        [Tooltip("1 = on, 0 = off, -1 = use the trainer's occupancy_randomization parameter (default 0 = off).")]
        public int occupancyRandomOverride = -1;
        [Tooltip("Number of parked cars per episode when occupancy randomisation is on (min, max), instead of the stage's " +
                 "occupiedBays. Real car parks run from empty to nearly full; the full-task stage only had 2-6 of 30.")]
        public Vector2Int occupancyRange = new Vector2Int(0, 27);

        [Header("Procedural car parks (training on many layouts, off by default)")]
        [Tooltip("Share of episodes on a random generated car park (aisle width, bay size, pillars, walls; see ProceduralLot). " +
                 "-1 = the trainer's procedural_layout parameter (default 0 = never), 0..1 = fixed share.")]
        public float proceduralOverride = -1f;

        [Header("Round 6: far starts and route distance (off by default)")]
        [Tooltip("Share of episodes that start far from the bay: anywhere in the aisles (route distance Far Start Route), any " +
                 "heading, including the showcase's centre hand-over pose. -1 = the trainer's far_start_share parameter (default 0).")]
        public float farStartOverride = -1f;
        [Tooltip("Reward distance term: 1 = distance along drivable space (DrivableMap), 0 = straight line. " +
                 "-1 = the trainer's route_distance parameter (default 0).")]
        public int routeDistanceOverride = -1;
        [Tooltip("Route distance (m) of far starts from the target bay.")]
        public Vector2 farStartRoute = new Vector2(10f, 40f);
        [Tooltip("On the training car park, share of far starts at the showcase's centre hand-over pose (car park centre, facing east).")]
        [Range(0f, 1f)] public float showcaseStartShare = 0.15f;
        [Tooltip("DrivableMap grid cell (m).")]
        public float mapCellSize = 0.4f;
        [Tooltip("Half the car's width (m); obstacles are enlarged by this plus the route clearance.")]
        public float carHalfWidth = 0.95f;
        [Tooltip("Read-only check, updated at each reset: the route map and this episode's start.")]
        [TextArea(2, 4)] public string routeMapInfo = "";

        [Header("Randomness")]
        [Tooltip("-1 = random each run. Set a fixed value for reproducible evaluation layouts.")]
        public int randomSeed = -1;
        public int maxSpawnAttempts = 30;

        public ParkingSpot Target { get; private set; }
        public int CurrentStage { get; private set; }
        public CurriculumStage CurrentStageSettings => stages[CurrentStage];
        public bool SparseReward { get; private set; }
        public bool GeometryRandomisation { get; private set; }
        /// <summary>True while this episode's car park is a generated one (not the training car park).</summary>
        public bool IsProceduralLayout { get; private set; }
        public float ProceduralShare { get; private set; }
        public string LayoutDescription => IsProceduralLayout && proceduralLot != null ? proceduralLot.Description : "training car park";
        ProceduralLot proceduralLot;
        /// <summary>Generator difficulty (procedural_difficulty, default 2 = full); see ProceduralLot.ForDifficulty.</summary>
        public int ProceduralDifficulty { get; private set; } = 2;
        /// <summary>Round 5 (hard_case_share, default 0): share of generated-car-park episodes that target a hard bay
        /// (a pillar beside it, else a narrow aisle) and, half of the time, start close (under 7 m).</summary>
        public float HardCaseShare { get; private set; }
        public bool HardCase { get; private set; }
        /// <summary>Round 5 (geometry_share, default 1): share of training-car-park episodes with geometry randomisation
        /// when it is on (generated car parks always vary). Below 1, the rest are the standard layout.</summary>
        public float GeometryShare { get; private set; } = 1f;
        /// <summary>Round 5 (stuck_steps_near_bay, default 0 = off): stuck limit near or in the target bay.</summary>
        public int StuckStepsNearBay { get; private set; }
        List<ParkingSpot> baseSpots;
        Material obstacleMaterial;
        public bool OccupancyRandomisation { get; private set; }
        /// <summary>
        /// Environment parameter "early_endings" (default 1): whether the stuck / no-progress endings are active.
        /// They were added (v7-v8) to break local optima of the SHAPED reward; Experiment B's "sparse, no early
        /// endings" condition turns them off so episodes end only on park, crash, out of bounds or timeout.
        /// </summary>
        public bool EarlyEndings { get; private set; } = true;
        /// <summary>How many of the target's direct neighbour bays hold a parked car this episode (0-2).</summary>
        public int TargetOccupiedNeighbours { get; private set; }
        /// <summary>Spawn values used this episode (for logging): distance back from the bay (m), sideways offset (m),
        /// heading offset from facing straight into the bay (deg), and whether the fallback pose was used.</summary>
        public float SpawnDistance { get; private set; }
        public float SpawnLateral { get; private set; }
        public float SpawnYaw { get; private set; }
        public bool SpawnFallback { get; private set; }

        /// <summary>Round 6 (far_start_share, default 0): share of episodes starting far from the bay, in the aisles.</summary>
        public float FarStartShare { get; private set; }
        /// <summary>Round 6 (route_distance, default 0): the reward's distance term uses the route along drivable space.</summary>
        public bool UseRouteDistance { get; private set; }
        /// <summary>Round 6 (route_clearance, default 0.3 m): extra room kept from obstacles in the route map.</summary>
        public float RouteClearance { get; private set; } = 0.3f;
        /// <summary>Round 6 (empty_bay_cost, default 3): route cost factor for driving through an empty bay.</summary>
        public float EmptyBayCost { get; private set; } = 3f;
        /// <summary>This episode started far from the bay (a far start, or a scenario start more than 15 m away).</summary>
        public bool FarStart { get; private set; }
        /// <summary>Route distance (m) from the start to the bay centre, -1 if no route map was built this episode.</summary>
        public float SpawnRoute { get; private set; } = -1f;


        DrivableMap map;
        Vector2 mapExtents;

        /// <summary>The distance used by the reward: along drivable space when route_distance is on, else straight.</summary>
        public float RewardDistance(Vector3 worldPos)
        {
            if (!Target) return 0f;
            if (UseRouteDistance && map != null && map.Ready) return map.Distance(worldPos, Target.Center);
            Vector3 d = worldPos - Target.Center;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>Build this episode's route map when far starts or the route reward need it (no random draws).</summary>
        void BuildMap()
        {
            if (FarStartShare <= 0f && !UseRouteDistance)
            {
                map?.Invalidate();
                return;
            }
            if (map == null || mapExtents != areaHalfExtents)
            {
                map = new DrivableMap(transform, areaHalfExtents, mapCellSize);
                mapExtents = areaHalfExtents;
            }
            map.Build(spots, Target, carHalfWidth, RouteClearance, EmptyBayCost);
        }

        System.Random rng;
        readonly List<int> scratch = new List<int>();
        List<int>[] neighbours;
        Vector2[] baseSize;
        Vector3[] baseLocalPos;
        (Vector3 pos, Quaternion rot, Vector3 scale)[] carBase;

        public static CurriculumStage[] DefaultStages() => new[]
        {
            new CurriculumStage { name = "0 Fixed bay, near, aligned", spawnDistance = new Vector2(5f, 7f),  lateralOffset = 0.5f, headingNoise = 10f,  occupiedBays = new Vector2Int(0, 0), randomTarget = false },
            new CurriculumStage { name = "1 Moderate spawn + heading", spawnDistance = new Vector2(5f, 9f),  lateralOffset = 2f,   headingNoise = 45f,  occupiedBays = new Vector2Int(0, 0), randomTarget = false },
            new CurriculumStage { name = "2 Random spawn + heading",   spawnDistance = new Vector2(5f, 12f), lateralOffset = 4f,   headingNoise = 90f,  occupiedBays = new Vector2Int(0, 0), randomTarget = false },
            new CurriculumStage { name = "3 Static parked cars",       spawnDistance = new Vector2(5f, 12f), lateralOffset = 4f,   headingNoise = 90f,  occupiedBays = new Vector2Int(1, 3), randomTarget = false, tightBayChance = 0.3f, softContact = true },
            new CurriculumStage { name = "4 Random inner bay",         spawnDistance = new Vector2(5f, 12f), lateralOffset = 4f,   headingNoise = 90f,  occupiedBays = new Vector2Int(1, 3), randomTarget = true, targetGroups = BayGroup.Inner, tightBayChance = 0.5f, softContact = true },
            new CurriculumStage { name = "5 Outer-row practice",       spawnDistance = new Vector2(5f, 8f),  lateralOffset = 1.5f, headingNoise = 45f,  occupiedBays = new Vector2Int(1, 3), randomTarget = true, targetGroups = BayGroup.Outer, tightBayChance = 0.3f, softContact = true },
            new CurriculumStage { name = "6 Inner + outer rows",       spawnDistance = new Vector2(5f, 12f), lateralOffset = 4f,   headingNoise = 90f,  occupiedBays = new Vector2Int(1, 3), randomTarget = true, targetGroups = BayGroup.Inner | BayGroup.Outer, tightBayChance = 0.5f, softContact = true },
            new CurriculumStage { name = "7 + Corner bays",            spawnDistance = new Vector2(5f, 12f), lateralOffset = 4f,   headingNoise = 90f,  occupiedBays = new Vector2Int(1, 3), randomTarget = true, targetGroups = BayGroup.Inner | BayGroup.Outer | BayGroup.Corner, tightBayChance = 0.5f, softContact = true },
            new CurriculumStage { name = "8 + Sideways (all bays)",    spawnDistance = new Vector2(5f, 12f), lateralOffset = 4f,   headingNoise = 90f,  occupiedBays = new Vector2Int(1, 3), randomTarget = true, targetGroups = BayGroup.All, tightBayChance = 0.5f, softContact = true },
            new CurriculumStage { name = "9 Sharper turnarounds",      spawnDistance = new Vector2(5f, 13f), lateralOffset = 6f,   headingNoise = 135f, occupiedBays = new Vector2Int(1, 4), randomTarget = true, targetGroups = BayGroup.All, tightBayChance = 0.5f },
            new CurriculumStage { name = "10 Full task",               spawnDistance = new Vector2(5f, 14f), lateralOffset = 8f,   headingNoise = 180f, occupiedBays = new Vector2Int(2, 6), randomTarget = true, targetGroups = BayGroup.All, tightBayChance = 0.5f },
        };

        void Awake()
        {
            if (spots.Count == 0) GetComponentsInChildren(spots);
            rng = randomSeed >= 0 ? new System.Random(randomSeed) : new System.Random();
            InitialiseSpots();
            baseSpots = new List<ParkingSpot>(spots);
            foreach (Transform child in transform)
                if (child.CompareTag("Wall") && child.TryGetComponent<Renderer>(out var r)) { obstacleMaterial = r.sharedMaterial; break; }
        }

        /// <summary>Switch this episode to a new generated car park, or back to the training car park.</summary>
        void ChooseLayout()
        {
            bool procedural = ProceduralShare > 0f && rng.NextDouble() < ProceduralShare;   // no draw when off
            // Undo the last episode's geometry randomisation BEFORE switching: InitialiseSpots (via ReplaceSpots) records
            // the current geometry as the base. Without this, every switch between car parks locked one random change of
            // the parked cars and the target bay into the base, and the changes accumulated as a random walk over training
            // (parked cars drifting out of their bays, turning, shrinking or growing). Found in round 5.
            if ((procedural || IsProceduralLayout) && baseSize != null && baseSize.Length == spots.Count) RestoreGeometry();
            if (procedural)
            {
                if (!IsProceduralLayout)
                    foreach (var s in baseSpots) { s.SetState(false, false); s.gameObject.SetActive(false); }
                proceduralLot ??= new ProceduralLot(transform, obstacleMaterial);
                proceduralLot.SetActive(true);
                var bays = proceduralLot.Generate(rng, ProceduralLot.ForDifficulty(ProceduralDifficulty));
                foreach (var b in spots) if (!bays.Contains(b)) b.SetState(false, false);   // pooled bays left over
                ReplaceSpots(bays);
                IsProceduralLayout = true;
            }
            else if (IsProceduralLayout)
            {
                foreach (var b in spots) b.SetState(false, false);
                proceduralLot.SetActive(false);
                foreach (var s in baseSpots) s.gameObject.SetActive(true);
                ReplaceSpots(new List<ParkingSpot>(baseSpots));
                IsProceduralLayout = false;
            }
        }

        /// <summary>
        /// Replace this area's bays at runtime (ParkPilot Assist: a car park built from a photo's layout).
        /// The old bays are destroyed by the caller; the new ones get parked cars, indicators and neighbours.
        /// </summary>
        public void ReplaceSpots(List<ParkingSpot> newSpots)
        {
            spots = newSpots;
            InitialiseSpots();
        }

        /// <summary>
        /// Debug statistic: how far the recorded base geometry has drifted from a clean car park (should stay 0).
        /// Parked cars sit at their bay's centre with the prefab's rotation and scale, so any offset is drift.
        /// </summary>
        public (float position, float yaw, float scale) BaseGeometryDrift()
        {
            if (carBase == null || carBase.Length == 0) return (0f, 0f, 0f);
            Vector3 prefabScale = obstacleCarPrefab ? obstacleCarPrefab.transform.localScale : Vector3.one;
            float pos = 0f, yaw = 0f, scale = 0f;
            foreach (var c in carBase)
            {
                pos += new Vector2(c.pos.x, c.pos.z).magnitude;
                yaw += Quaternion.Angle(c.rot, Quaternion.identity);
                scale += Mathf.Abs(c.scale.x / Mathf.Max(1e-3f, prefabScale.x) - 1f) + Mathf.Abs(c.scale.z / Mathf.Max(1e-3f, prefabScale.z) - 1f);
            }
            return (pos / carBase.Length, yaw / carBase.Length, scale / (2f * carBase.Length));
        }

        void InitialiseSpots()
        {
            foreach (var spot in spots)
            {
                if (!spot.parkedCar && obstacleCarPrefab)
                {
                    spot.parkedCar = Instantiate(obstacleCarPrefab, spot.transform);
                    spot.parkedCar.transform.localPosition = Vector3.zero;
                    spot.parkedCar.transform.localRotation = Quaternion.identity;
                }
                spot.EnsureIndicator(indicatorMaterial);
                spot.SetState(false, false);
            }
            neighbours = FindNeighbours();

            // Base geometry, restored at every reset (geometry randomisation changes it per episode).
            baseSize = spots.Select(s => s.size).ToArray();
            baseLocalPos = spots.Select(s => s.transform.localPosition).ToArray();
            carBase = spots.Select(s => s.parkedCar
                ? (s.parkedCar.transform.localPosition, s.parkedCar.transform.localRotation, s.parkedCar.transform.localScale)
                : (Vector3.zero, Quaternion.identity, Vector3.one)).ToArray();
        }

        /// <summary>
        /// For each bay, the bays directly beside it: same orientation, about one bay width away
        /// across the bay, and level with it along the bay. (Top/bottom for the sideways bays.)
        /// </summary>
        /// <summary>For evaluation: parked cars in the whole car park, and whether the target's left / right neighbour
        /// (as seen driving into the bay) is occupied.</summary>
        public (int occupied, int total, bool left, bool right) TargetSurroundings()
        {
            int occupied = spots.Count(s => s.IsOccupied && s != Target);
            bool left = false, right = false;
            int t = Target ? spots.IndexOf(Target) : -1;
            if (t >= 0 && neighbours != null)
                foreach (int n in neighbours[t])
                {
                    if (!spots[n].IsOccupied) continue;
                    if (Target.transform.InverseTransformPoint(spots[n].Center).x < 0f) left = true;
                    else right = true;
                }
            return (occupied, spots.Count, left, right);
        }

        List<int>[] FindNeighbours()
        {
            var result = new List<int>[spots.Count];
            for (int i = 0; i < spots.Count; i++)
            {
                result[i] = new List<int>();
                var a = spots[i];
                for (int j = 0; j < spots.Count; j++)
                {
                    if (i == j) continue;
                    var b = spots[j];
                    if (Vector3.Dot(a.Forward, b.Forward) < 0.9f) continue;
                    Vector3 local = a.transform.InverseTransformPoint(b.Center);
                    float across = Mathf.Abs(local.x), along = Mathf.Abs(local.z);
                    if (across > a.size.x * 0.5f && across < a.size.x * 1.5f && along < a.size.y * 0.5f)
                        result[i].Add(j);
                }
            }
            return result;
        }

        /// <summary>Randomises the layout for a new episode and places the car.</summary>
        public void ResetArea(CarController car, BoxCollider carBody)
        {
            ReadParameters();
            LayoutIndex++;
            if (ActiveScenario != null)
            {
                ApplyScenario(car, carBody, ActiveScenario);
                return;
            }
            ChooseLayout();
            var stage = stages[CurrentStage];

            // 1. Target bay (round 5: hard cases on generated car parks; no random draw when hard_case_share is 0)
            HardCase = IsProceduralLayout && HardCaseShare > 0f && rng.NextDouble() < HardCaseShare;
            int targetIndex = HardCase ? HardTargetIndex()
                : stage.randomTarget ? RandomTargetIndex(stage.targetGroups) : Mathf.Clamp(fixedTargetIndex, 0, spots.Count - 1);
            Target = spots[targetIndex];

            // 2. Occupied bays (never the target)
            scratch.Clear();
            for (int i = 0; i < spots.Count; i++) if (i != targetIndex) scratch.Add(i);
            Shuffle(scratch);
            // One random draw either way, so evaluation layouts without occupancy randomisation are unchanged.
            Vector2Int range = OccupancyRandomisation ? occupancyRange : stage.occupiedBays;
            if (OccupancyRandomisation && IsProceduralLayout)   // same 0-90 % share on car parks of any size
                range = new Vector2Int(0, Mathf.CeilToInt(0.9f * spots.Count));
            int occupied = Mathf.Min(rng.Next(range.x, range.y + 1), scratch.Count);
            for (int i = 0; i < spots.Count; i++) spots[i].SetState(false, i == targetIndex);
            for (int k = 0; k < occupied; k++) spots[scratch[k]].SetState(true, false);

            // 2b. Tight-bay practice: sometimes box the target in on both sides
            if (stage.tightBayChance > 0f && rng.NextDouble() < stage.tightBayChance)
                foreach (int n in neighbours[targetIndex]) spots[n].SetState(true, false);

            TargetOccupiedNeighbours = 0;
            foreach (int n in neighbours[targetIndex]) if (spots[n].IsOccupied) TargetOccupiedNeighbours++;

            // 2c. Geometry: back to the base layout, then (fine-tuning only) vary the target bay and parked cars.
            //     No random draws when it is off, so evaluation layouts stay identical to earlier runs.
            RestoreGeometry();
            // Round 5: on the training car park only a share of episodes vary (geometry_share; no draw when it is 1).
            if (GeometryRandomisation && (IsProceduralLayout || GeometryShare >= 1f || rng.NextDouble() < GeometryShare))
                RandomiseGeometry(targetIndex);
            Physics.SyncTransforms();

            // 2d. Round 6: route map for far starts and the route reward (built only when one of them is on).
            BuildMap();

            // 3. Car pose
            PlaceCar(car, carBody, stage);
            SpawnRoute = map != null && map.Ready ? map.Distance(car.transform.position, Target.Center) : -1f;
            if (map != null && map.Ready)
                routeMapInfo = $"{map.Summary()}\nTarget {Target.name}, {(FarStart ? "FAR start" : "near start")}: straight {SpawnDistance:0.0} m, " +
                               $"route {SpawnRoute:0.0} m, route reward {(UseRouteDistance ? "on" : "off")}";
            else routeMapInfo = "Route map off (far starts and route distance both off).";
        }

        void PlaceCar(CarController car, BoxCollider carBody, CurriculumStage stage)
        {
            bool closeStart = HardCase && rng.NextDouble() < 0.5;          // only drawn in hard-case episodes
            FarStart = FarStartShare > 0f && rng.NextDouble() < FarStartShare;   // no draw when far starts are off
            if (FarStart && PlaceFar(car, carBody)) return;
            FarStart = false;
            Vector3 bayForward = Vector3.ProjectOnPlane(Target.Forward, Vector3.up).normalized;
            Vector3 bayRight = Vector3.Cross(Vector3.up, bayForward);

            for (int attempt = 0; attempt <= maxSpawnAttempts; attempt++)
            {
                // Last attempt falls back to the easiest pose so the episode can always start.
                bool fallback = attempt == maxSpawnAttempts;
                float maxDistance = closeStart ? Mathf.Min(7f, stage.spawnDistance.y) : stage.spawnDistance.y;
                float distance = fallback ? stages[0].spawnDistance.x : Range(stage.spawnDistance.x, maxDistance);
                float lateral = fallback ? 0f : Range(-stage.lateralOffset, stage.lateralOffset);
                float yaw = fallback ? 0f : Range(-stage.headingNoise, stage.headingNoise);

                Vector3 pos = Target.Center - bayForward * distance + bayRight * lateral;
                pos.y = transform.position.y + carSpawnHeight;
                Quaternion rot = Quaternion.LookRotation(bayForward) * Quaternion.Euler(0f, yaw, 0f);

                if (!fallback && (!IsInsideArea(pos, 2f) || Overlaps(pos, rot, carBody))) continue;

                car.Teleport(pos, rot);
                SpawnDistance = distance;
                SpawnLateral = lateral;
                SpawnYaw = yaw;
                SpawnFallback = fallback;
                if (fallback) Debug.LogWarning($"[{name}] No free spawn found for stage {CurrentStage}; used fallback pose.", this);
                return;
            }
        }

        /// <summary>
        /// Round 6: a start far from the bay. On the training car park, a share of them is the showcase's centre
        /// hand-over pose (car park centre, facing east along the central aisle). Otherwise a random free aisle cell at a
        /// route distance in farStartRoute, facing along one of the area's axes (half of the time, +-15 deg, as a car
        /// driving along an aisle) or any direction. False if no free pose was found (the caller uses a near start).
        /// </summary>
        bool PlaceFar(CarController car, BoxCollider carBody)
        {
            if (map == null || !map.Ready) return false;
            for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
            {
                Vector2 p;
                float yaw;
                if (!IsProceduralLayout && rng.NextDouble() < showcaseStartShare)
                {
                    p = new Vector2(Range(-1f, 0f), Range(-0.4f, 0.4f));
                    yaw = 90f + Range(-8f, 8f);
                }
                else
                {
                    if (!map.SampleAisleCell(rng, farStartRoute.x, farStartRoute.y, out p, out _)) return false;
                    yaw = rng.NextDouble() < 0.5 ? rng.Next(4) * 90f + Range(-15f, 15f) : Range(0f, 360f);
                }
                Vector3 pos = transform.TransformPoint(new Vector3(p.x, 0f, p.y));
                pos.y = transform.position.y + carSpawnHeight;
                Quaternion rot = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
                if (!IsInsideArea(pos, 2f) || Overlaps(pos, rot, carBody)) continue;
                car.Teleport(pos, rot);
                Vector3 toBay = Target.Center - pos;
                toBay.y = 0f;
                SpawnDistance = toBay.magnitude;
                SpawnLateral = 0f;
                SpawnYaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(Target.Forward, Vector3.up), rot * Vector3.forward, Vector3.up);
                SpawnFallback = false;
                return true;
            }
            return false;
        }

        /// <summary>Which layout of the current seed this episode uses (1 = first layout after Reseed).</summary>
        public int LayoutIndex { get; private set; }
        /// <summary>Increments on every Reseed, so episodes from an older seed can be told apart.</summary>
        public int SeedEpoch { get; private set; }

        /// <summary>
        /// A fixed parking situation (ScenarioRunner / ParkPilot Assist): exact target bay, exact parked cars,
        /// and either an exact start pose or one drawn from a stage's spawn rules with a fixed seed.
        /// </summary>
        public class Scenario
        {
            public int targetIndex;
            public List<int> occupied = new List<int>();
            public bool hasStartPose;
            public Vector3 startLocalPosition;   // in this area's local space (y ignored)
            public float startYaw;               // degrees, relative to this area
            public int spawnStage = 10;          // spawn rules used when there is no start pose
            public int spawnSeed = 1;
        }

        /// <summary>When set, every reset reproduces this scenario instead of the curriculum's random layout.</summary>
        public Scenario ActiveScenario { get; set; }

        /// <summary>
        /// ScenarioRunner: while true, the agent skips its automatic reset after an episode ends, so the final
        /// pose stays on screen. Not used in training.
        /// </summary>
        public bool HoldReset { get; set; }

        void ApplyScenario(CarController car, BoxCollider carBody, Scenario s)
        {
            int targetIndex = Mathf.Clamp(s.targetIndex, 0, spots.Count - 1);
            Target = spots[targetIndex];
            for (int i = 0; i < spots.Count; i++) spots[i].SetState(i != targetIndex && s.occupied.Contains(i), i == targetIndex);
            TargetOccupiedNeighbours = 0;
            foreach (int n in neighbours[targetIndex]) if (spots[n].IsOccupied) TargetOccupiedNeighbours++;
            Physics.SyncTransforms();
            FarStart = false;
            if (UseRouteDistance) BuildMap(); else map?.Invalidate();

            if (s.hasStartPose)
            {
                Vector3 pos = transform.TransformPoint(new Vector3(s.startLocalPosition.x, 0f, s.startLocalPosition.z));
                pos.y = transform.position.y + carSpawnHeight;
                car.Teleport(pos, transform.rotation * Quaternion.Euler(0f, s.startYaw, 0f));
                SpawnDistance = Vector3.Distance(pos, Target.Center);
                SpawnLateral = 0f;
                SpawnYaw = s.startYaw;
                SpawnFallback = false;
                FarStart = SpawnDistance > 15f;   // e.g. the showcase's centre hand-over: the agent gets the longer time limit
            }
            else
            {
                rng = new System.Random(s.spawnSeed);  // same start pose every replay
                PlaceCar(car, carBody, stages[Mathf.Clamp(s.spawnStage, 0, stages.Length - 1)]);
            }
        }

        void RestoreGeometry()
        {
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i].size != baseSize[i] || spots[i].transform.localPosition != baseLocalPos[i])
                    spots[i].Resize(baseSize[i], baseLocalPos[i]);
                var car = spots[i].parkedCar;
                if (!car) continue;
                car.transform.localPosition = carBase[i].pos;
                car.transform.localRotation = carBase[i].rot;
                car.transform.localScale = carBase[i].scale;
            }
        }

        /// <summary>
        /// Geometry randomisation for real car parks: the target bay gets a random width and length (its open end stays
        /// where it is, as in ParkPilot Assist), and every parked car is moved, turned and resized a little in its bay.
        /// The agent's observations are unchanged; it learns not to rely on the training car park's exact proportions.
        /// </summary>
        void RandomiseGeometry(int targetIndex)
        {
            var t = spots[targetIndex];
            Vector2 b = baseSize[targetIndex];
            float w = Range(targetWidthRange.x, targetWidthRange.y);
            float l = Range(targetLengthRange.x, targetLengthRange.y);
            Vector3 intoBay = t.transform.localRotation * Vector3.forward;     // open end -> back, in the parent's space
            t.Resize(new Vector2(w, l), baseLocalPos[targetIndex] + intoBay * ((l - b.y) * 0.5f));

            for (int i = 0; i < spots.Count; i++)
            {
                var car = spots[i].parkedCar;
                if (i == targetIndex || !car || !spots[i].IsOccupied) continue;
                var cb = carBase[i];
                car.transform.localPosition = cb.pos + new Vector3(Range(-parkedLateralJitter, parkedLateralJitter), 0f,
                                                                   Range(parkedDepthShift.x, parkedDepthShift.y));
                car.transform.localRotation = cb.rot * Quaternion.Euler(0f, Range(-parkedYawJitter, parkedYawJitter), 0f);
                car.transform.localScale = Vector3.Scale(cb.scale, new Vector3(Range(parkedWidthScale.x, parkedWidthScale.y), 1f,
                                                                               Range(parkedLengthScale.x, parkedLengthScale.y)));
            }
        }

        /// <summary>Restart the layout random sequence (evaluation: identical test layouts for every model).</summary>
        public void Reseed(int seed)
        {
            rng = new System.Random(seed);
            SeedEpoch++;
            LayoutIndex = 0;
        }

        /// <summary>Round 5: a bay with a pillar beside it if the car park has one, else one on a narrow aisle (under
        /// 8 m), else any bay.</summary>
        int HardTargetIndex()
        {
            scratch.Clear();
            for (int i = 0; i < spots.Count; i++) if (spots[i].layoutPillarBeside) scratch.Add(i);
            if (scratch.Count == 0)
                for (int i = 0; i < spots.Count; i++) if (spots[i].layoutAisleWidth > 0f && spots[i].layoutAisleWidth < 8f) scratch.Add(i);
            if (scratch.Count == 0) return rng.Next(spots.Count);
            return scratch[rng.Next(scratch.Count)];
        }

        int RandomTargetIndex(BayGroup groups)
        {
            scratch.Clear();
            for (int i = 0; i < spots.Count; i++)
                if ((spots[i].Group & groups) != 0) scratch.Add(i);
            if (scratch.Count == 0)
            {
                Debug.LogWarning($"[{name}] No bays in groups {groups}; picking from all bays.", this);
                return rng.Next(spots.Count);
            }
            return scratch[rng.Next(scratch.Count)];
        }

        void ReadParameters()
        {
            var parameters = Academy.Instance.EnvironmentParameters;
            int stage = stageOverride >= 0
                ? stageOverride
                : Mathf.RoundToInt(parameters.GetWithDefault("curriculum_stage", 0f));
            CurrentStage = Mathf.Clamp(stage, 0, stages.Length - 1);
            SparseReward = parameters.GetWithDefault("sparse_reward", sparseRewardDefault ? 1f : 0f) > 0.5f;
            EarlyEndings = earlyEndingsOverride >= 0
                ? earlyEndingsOverride == 1
                : parameters.GetWithDefault("early_endings", 1f) > 0.5f;
            ProceduralShare = proceduralOverride >= 0f
                ? Mathf.Clamp01(proceduralOverride)
                : Mathf.Clamp01(parameters.GetWithDefault("procedural_layout", 0f));
            ProceduralDifficulty = Mathf.RoundToInt(parameters.GetWithDefault("procedural_difficulty", 2f));
            HardCaseShare = Mathf.Clamp01(parameters.GetWithDefault("hard_case_share", 0f));
            GeometryShare = Mathf.Clamp01(parameters.GetWithDefault("geometry_share", 1f));
            StuckStepsNearBay = Mathf.RoundToInt(parameters.GetWithDefault("stuck_steps_near_bay", 0f));
            FarStartShare = farStartOverride >= 0f
                ? Mathf.Clamp01(farStartOverride)
                : Mathf.Clamp01(parameters.GetWithDefault("far_start_share", 0f));
            UseRouteDistance = routeDistanceOverride >= 0
                ? routeDistanceOverride == 1
                : parameters.GetWithDefault("route_distance", 0f) > 0.5f;
            RouteClearance = Mathf.Max(0f, parameters.GetWithDefault("route_clearance", 0.3f));
            EmptyBayCost = Mathf.Max(1f, parameters.GetWithDefault("empty_bay_cost", 3f));
            OccupancyRandomisation = occupancyRandomOverride >= 0
                ? occupancyRandomOverride == 1
                : parameters.GetWithDefault("occupancy_randomization", 0f) > 0.5f;
            GeometryRandomisation = geometryRandomOverride >= 0
                ? geometryRandomOverride == 1
                : parameters.GetWithDefault("geometry_randomization", 0f) > 0.5f;
        }

        public bool IsOutOfBounds(Vector3 worldPos) => !IsInsideArea(worldPos, 0f) || worldPos.y < transform.position.y - 2f;

        public bool IsInsideArea(Vector3 worldPos, float margin)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            return Mathf.Abs(local.x) <= areaHalfExtents.x - margin
                && Mathf.Abs(local.z) <= areaHalfExtents.y - margin;
        }

        /// <summary>Would the car's box collider at this pose overlap a wall or parked car?</summary>
        public static bool Overlaps(Vector3 pivotPos, Quaternion rot, BoxCollider body)
        {
            Vector3 scale = body.transform.lossyScale;
            Vector3 half = Vector3.Scale(body.size, scale) * 0.5f + Vector3.one * 0.2f; // small safety margin
            Vector3 center = pivotPos + rot * Vector3.Scale(body.center, scale);
            foreach (var hit in Physics.OverlapBox(center, half, rot, ~0, QueryTriggerInteraction.Ignore))
            {
                if (IsObstacle(hit)) return true;
            }
            return false;
        }

        public static bool IsObstacle(Collider c) => c.CompareTag("Wall") || c.CompareTag("ObstacleCar");

        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>Round 6 check: select a training area in Play mode (far starts or route distance on) to see its route map.</summary>
        void OnDrawGizmosSelected()
        {
            if (map != null && map.Ready) map.DrawGizmos();
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * 0.5f, new Vector3(areaHalfExtents.x * 2f, 1f, areaHalfExtents.y * 2f));
        }
    }
}
