using System;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ParkPilot
{
    /// <summary>
    /// RL parking agent.
    ///
    /// Behavior Parameters: name "ParkingAgent", Vector Observation Space Size = 9 (set to 9 or 11 at runtime, see
    /// ConfigureFor), Continuous Actions = 2 (0: steer, 1: throttle), Discrete Branches = 0.
    /// Add a Decision Requester (period 5) and a RayPerceptionSensorComponent3D
    /// (detectable tags: Wall, ObstacleCar). Max Step 1500 (= 30 s at 0.02 s physics step).
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class ParkingAgent : Agent
    {
        public enum Outcome { None, Parked, Collision, OutOfBounds, Timeout, Stuck, NoProgress }

        public struct EpisodeResult
        {
            public Outcome outcome;
            public int steps;
            public int stage;
            public int targetIndex;
            public float finalDistance;
            public float finalHeadingError;
            public float cumulativeReward;
            // Evaluation details (filled from the episode's spawn and diagnostics)
            public string targetName;
            public BayGroup group;
            public int occupiedNeighbours;
            public float spawnHeading, spawnLateral, spawnDistance;
            public bool spawnFallback;
            public string approach;          // Straight / Curved / UTurn (turning before first full entry)
            public float turnBeforeEntry;    // deg
            public bool noseIn;              // finish direction (parked) or at the closest-to-inside moment
            public int entries;              // times the car centre entered the bay
            public float entrySpeed;         // |speed| at first full entry, -1 if never fully inside
            public int softContacts;
            public int layoutIndex;          // ParkingEnvironment.LayoutIndex of this episode
            public int seedEpoch;            // ParkingEnvironment.SeedEpoch when this episode started
            public string collidedWith;      // collision only: car / pillar / row wall / boundary wall
            public bool spawnFar;            // round 6: started far from the bay (far start or a scenario start over 15 m)
            public float spawnRoute;         // round 6: route distance from the start to the bay (m), -1 if not computed
        }

        /// <summary>What Heuristic() does when the behaviour type is Heuristic Only (keyboard, or a baseline).</summary>
        public enum HeuristicMode { Keyboard, Random, RuleBased }

        [Header("References")]
        [Tooltip("Auto-found in parents if empty.")]
        public ParkingEnvironment environment;
        [Tooltip("The car's footprint collider. Auto-found on this GameObject if empty.")]
        public BoxCollider body;

        [Header("Parked condition")]
        [Tooltip("How far (m) the car's corners may stick out of the bay.")]
        public float positionMargin = 0.15f;
        [Tooltip("Max heading error (deg) to count as aligned.")]
        public float headingTolerance = 10f;
        [Tooltip("Max speed (m/s) to count as stopped.")]
        public float stopSpeed = 0.3f;
        [Tooltip("Physics steps the parked condition must hold (25 = 0.5 s).")]
        public int holdSteps = 25;
        [Tooltip("Reversing in (facing out of the bay) also counts as parked.")]
        public bool allowReverseParking = true;

        [Header("Rewards")]
        public float successReward = 2f;
        public float collisionPenalty = -1f;
        [Tooltip("Applied every physics step (shaped mode only).")]
        public float stepPenalty = -0.001f;
        [Tooltip("Scale of the potential-based progress + alignment shaping (shaped mode only).")]
        public float shapingScale = 1f;
        [Tooltip("Weight of heading alignment in the potential.")]
        public float alignmentWeight = 0.5f;
        [Tooltip("Alignment only starts to matter within this distance (m) of the bay.")]
        public float alignmentRadius = 6f;
        [Tooltip("Weight of the in-bay score (how far the car's footprint is inside the bay) in the potential.")]
        public float inBayWeight = 0.5f;
        [Tooltip("Reward v3: a corner this far (m) outside the bay scores 0, inside scores 1, linear in between. " +
                 "Gives a signal for the last bit of adjustment instead of jumps when a corner crosses the line.")]
        public float inBayFalloff = 1.5f;
        [Tooltip("Reward v5: weight of being on the bay's centre-line AND pointing along it as the car gets close. " +
                 "Teaches 'line up, then drive straight in', which tight bays between cars require.")]
        public float lineUpWeight = 0.5f;
        [Tooltip("Sideways offset (m) from the bay centre-line at which the line-up score reaches 0.")]
        public float lineUpTolerance = 0.75f;
        [Tooltip("Heading error (deg) from the bay axis at which the line-up score reaches 0.")]
        public float lineUpHeadingTolerance = 30f;
        [Tooltip("The line-up term starts to matter within this distance (m) of the bay.")]
        public float lineUpRadius = 8f;
        [Tooltip("Reward v9: weight of being slow while inside the bay and aligned. Teaches 'brake once you're in " +
                 "the right spot' (diagnostics showed cars passing through the parked pose at ~0.6 m/s).")]
        public float stopWeight = 0.5f;
        [Tooltip("Speed (m/s) at which the stop score reaches 0.")]
        public float stopSpeedScale = 1f;
        [Tooltip("Reward v10: weight of approaching slowly in the last metres before/at the bay, so nose-in entries " +
                 "arrive slow enough to stop instead of overshooting towards the wall.")]
        public float approachWeight = 0.3f;
        [Tooltip("Slow approach starts to matter within this distance (m) of the bay centre (bay half-length ~2.5 m + ~3.5 m).")]
        public float approachRadius = 6f;
        [Tooltip("Speed (m/s) at which the slow-approach score reaches 0.")]
        public float approachSpeedScale = 2f;

        [Header("Soft contact (stages with softContact enabled)")]
        [Tooltip("Impacts slower than this (m/s) are 'soft': penalised but the episode continues.")]
        public float softContactMaxSpeed = 1f;
        [Tooltip("Penalty per soft contact.")]
        public float softContactPenalty = -0.3f;
        [Tooltip("Penalty per physics step while staying in contact (discourages leaning on cars/walls).")]
        public float contactStayPenalty = -0.005f;
        [Tooltip("Soft contacts allowed per episode; the next one counts as a crash.")]
        public int maxSoftContacts = 3;

        [Header("Stuck ending (reward v6)")]
        [Tooltip("End the episode if the car hasn't moved meaningfully for a while and isn't parked " +
                 "(Part B plan: 'episodes terminate early if the agent becomes stuck').")]
        public bool enableStuckEnding = true;
        [Tooltip("Physics steps without meaningful movement before the episode ends as Stuck (150 = 3 s).")]
        public int stuckSteps = 150;
        [Tooltip("Moving further than this (m) from where the car last 'settled' counts as meaningful movement.")]
        public float stuckDistance = 0.3f;
        [Tooltip("Turning more than this (deg) since the car last 'settled' counts as meaningful movement.")]
        public float stuckAngle = 10f;
        [Tooltip("Penalty when the episode ends as Stuck or NoProgress (shaped mode; same as a crash).")]
        public float stuckPenalty = -1f;

        [Header("No-progress ending (reward v7)")]
        [Tooltip("End the episode if the best shaping potential reached hasn't improved for a while. Catches " +
                 "loops (in-out-in at the same wrong spot) and sideways shuffles that keep moving but get nowhere.")]
        public bool enableNoProgressEnding = true;
        [Tooltip("Physics steps without beating the best potential before the episode ends (500 = 10 s; v8 used 300 = 6 s).")]
        public int noProgressSteps = 500;
        [Tooltip("The potential must beat its previous best by at least this much to count as progress.")]
        public float progressEpsilon = 0.02f;

        [Header("Observations")]
        [Tooltip("Distances are divided by this to keep observations roughly in [-1, 1].")]
        public float maxObservationDistance = 30f;
        [Tooltip("Forward velocity is divided by this (m/s). Fixed at the old top speed (5) so the input keeps the same " +
                 "meaning when the car's speed cap changes (v11 lowered it to 3).")]
        public float speedObservationScale = 5f;
        [Tooltip("v9: depth of the car centre along the bay axis is divided by this (m), clipped to [-1, 1].")]
        public float bayDepthScale = 3f;
        [Tooltip("v9: sideways offset of the car centre from the bay centre-line is divided by this (m), clipped to [-1, 1].")]
        public float bayLateralScale = 2f;

        [Header("Debug")]
        public bool logEpisodes;

        [Header("Heuristic / baselines")]
        [Tooltip("Keyboard = manual driving. Random / RuleBased = evaluation baselines (EvaluationRunner sets this).")]
        public HeuristicMode heuristicMode = HeuristicMode.Keyboard;

        /// <summary>Raised once per finished episode (for evaluation logging).</summary>
        public event Action<ParkingAgent, EpisodeResult> EpisodeFinished;

        public const int ObservationSize = 9;
        public const int ObservationSizeWithBay = 11;

        /// <summary>
        /// Bay-size observations (round 4): two extra inputs, the target bay's width and length. Off for every model trained
        /// before round 4 (9 inputs). On in a build started with "--bay-size-obs" (mlagents-learn --env-args --bay-size-obs),
        /// or when a runner sets <see cref="BaySizeObservationOverride"/> (from its model's input size) before the agents
        /// are enabled. Decided once, when the agent is enabled: the policy's input size cannot change afterwards.
        /// </summary>
        public static bool? BaySizeObservationOverride;
        public static bool BaySizeObservation => BaySizeObservationOverride ?? CommandLineBaySize;
        static readonly bool CommandLineBaySize = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--bay-size-obs") >= 0;
        public int CurrentObservationSize { get; private set; } = ObservationSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            BaySizeObservationOverride = null;
            NearRaysOverride = null;
            EvaluationActionNoise = Vector2.zero;
            noiseSeedCounter = 0;
        }

        /// <summary>
        /// Near-range ray sensor (round 5): a second, dense, short ray fan so thin obstacles close to the car (pillars,
        /// 0.4-0.7 m) cannot slip between the 22.5-degree gaps of the main sensor. 31 rays over +-150 degrees (10 degrees
        /// apart), 4 m, sphere radius 0.1 m, same tags as the main sensor: 124 inputs. Named so that it sorts after the
        /// vector sensor (ML-Agents orders sensors by name), i.e. its inputs come last. On with "--near-rays", or set by a
        /// runner from its model's inputs; off for every older model.
        /// </summary>
        public static bool? NearRaysOverride;
        public static bool NearRays => NearRaysOverride ?? CommandLineNearRays;
        static readonly bool CommandLineNearRays = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--near-rays") >= 0;
        public const string NearRaySensorName = "ZNearRays";
        public const int NearRaysPerDirection = 15, NearRayInputs = (2 * NearRaysPerDirection + 1) * 4;

        /// <summary>Input sizes of a trained model (one per sensor), e.g. 68, 11, 124. Empty if it cannot be read.</summary>
        public static System.Collections.Generic.List<int> ModelInputSizes(Unity.InferenceEngine.ModelAsset asset)
        {
            var sizes = new System.Collections.Generic.List<int>();
            if (!asset) return sizes;
            foreach (var input in Unity.InferenceEngine.ModelLoader.Load(asset).inputs)
            {
                string s = input.shape.ToString().Replace(" ", "").TrimEnd(')');   // e.g. "(?,11"
                if (int.TryParse(s.Substring(s.LastIndexOf(',') + 1), out int n)) sizes.Add(n);
            }
            return sizes;
        }

        /// <summary>Input size of a trained model's vector observation (9 or 11), or -1 if it cannot be read.</summary>
        public static int ModelVectorSize(Unity.InferenceEngine.ModelAsset asset)
        {
            var sizes = ModelInputSizes(asset);
            return sizes.Contains(ObservationSizeWithBay) ? ObservationSizeWithBay : sizes.Contains(ObservationSize) ? ObservationSize : -1;
        }

        /// <summary>For runners: set the optional observations from the model they will run (call in Awake).</summary>
        public static void ConfigureFor(Unity.InferenceEngine.ModelAsset asset)
        {
            var sizes = ModelInputSizes(asset);
            if (sizes.Count == 0) return;
            BaySizeObservationOverride = sizes.Contains(ObservationSizeWithBay);
            NearRaysOverride = sizes.Contains(NearRayInputs);
        }

        protected override void OnEnable()
        {
            CurrentObservationSize = BaySizeObservation ? ObservationSizeWithBay : ObservationSize;
            var bp = GetComponent<BehaviorParameters>();
            if (bp) bp.BrainParameters.VectorObservationSize = CurrentObservationSize;
            if (NearRays) AddNearRaySensor();
            base.OnEnable();
        }

        void AddNearRaySensor()
        {
            var sensors = GetComponents<RayPerceptionSensorComponent3D>();
            if (sensors.Any(s => s.SensorName == NearRaySensorName)) return;
            var main = sensors.FirstOrDefault();
            var near = gameObject.AddComponent<RayPerceptionSensorComponent3D>();
            near.SensorName = NearRaySensorName;
            near.DetectableTags = main ? new System.Collections.Generic.List<string>(main.DetectableTags) : new System.Collections.Generic.List<string> { "Wall", "ObstacleCar" };
            near.RaysPerDirection = NearRaysPerDirection;
            near.MaxRayDegrees = 150f;
            near.SphereCastRadius = 0.1f;
            near.RayLength = 4f;
            if (main)
            {
                near.RayLayerMask = main.RayLayerMask;
                near.StartVerticalOffset = main.StartVerticalOffset;
                near.EndVerticalOffset = main.EndVerticalOffset;
            }
            near.ObservationStacks = 1;
        }

        CarController car;
        int baseMaxStep;
        [Tooltip("Round 6: Max Step for episodes that start far from the bay (physics steps, 3000 = 60 s).")]
        public int farStartMaxStep = 3000;
        float previousPotential;
        int parkedCounter;
        int episodeSteps;
        int softContacts;
        float lastStepReward;
        bool episodeRunning;
        Outcome outcome;

        public CarController Car => car;
        public ParkingSpot Target => environment ? environment.Target : null;

        public override void Initialize()
        {
            baseMaxStep = MaxStep;
            car = GetComponent<CarController>();
            if (!body) body = GetComponent<BoxCollider>();
            if (!environment) environment = GetComponentInParent<ParkingEnvironment>();
            if (!body || !environment)
                Debug.LogError($"[{name}] ParkingAgent needs a BoxCollider and a ParkingEnvironment in its parents.", this);
        }

        public override void OnEpisodeBegin()
        {
            // Episode hit Max Step without ending itself -> timeout (no extra penalty; the
            // trainer treats it as a truncation and bootstraps the value).
            if (episodeRunning && outcome == Outcome.None) Report(Outcome.Timeout);

            // ScenarioRunner keeps the finished scene on screen (time is paused) until it asks for a replay.
            if (environment.HoldReset) return;

            environment.ResetArea(car, body);
            // Round 6: far starts need time to drive to the bay before the manoeuvre.
            MaxStep = environment.FarStart ? Mathf.Max(baseMaxStep, farStartMaxStep) : baseMaxStep;
            previousPotential = Potential();
            parkedCounter = 0;
            episodeSteps = 0;
            softContacts = 0;
            lastCollision = "";
            lastStepReward = 0f;
            ResetStuckAnchor();
            ResetDiagnostics();
            ruleEntering = false;
            ruleReverseDecisions = 0;
            bestPotential = previousPotential;
            bestPotentialStep = 0;
            outcome = Outcome.None;
            episodeRunning = true;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            var target = Target;
            if (!target)
            {
                for (int i = 0; i < CurrentObservationSize; i++) sensor.AddObservation(0f);
                return;
            }

            // Target position in the car's local frame (x = right, z = forward)
            Vector3 toTarget = transform.InverseTransformPoint(target.Center);
            sensor.AddObservation(Mathf.Clamp(toTarget.x / maxObservationDistance, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(toTarget.z / maxObservationDistance, -1f, 1f));

            // Heading difference to the bay axis as sin/cos (no wrap-around discontinuity)
            float headingRad = SignedHeadingToBay(target) * Mathf.Deg2Rad;
            sensor.AddObservation(Mathf.Sin(headingRad));
            sensor.AddObservation(Mathf.Cos(headingRad));

            // Car centre in the BAY's frame, at fine scale (v9). The coarse target position above
            // changes by only ~0.017 per 0.5 m, too little for the final positioning; these say
            // exactly how far off the centre-line and how deep the car is.
            Vector3 inBay = target.transform.InverseTransformPoint(CarCenter());
            // Slot 5 (was sideways velocity, always ~0 for this car model): depth along the bay, + = further in
            sensor.AddObservation(Mathf.Clamp(inBay.z / bayDepthScale, -1f, 1f));

            Vector3 localVelocity = transform.InverseTransformDirection(car.Body.linearVelocity);
            sensor.AddObservation(localVelocity.z / speedObservationScale);
            sensor.AddObservation(Mathf.Clamp(car.Body.angularVelocity.y / 2f, -1f, 1f));
            sensor.AddObservation(car.SteerAngle / car.maxSteerAngle);

            // Slot 9 (was distance / 30 m, redundant with slots 1-2): sideways offset from the bay centre-line
            sensor.AddObservation(Mathf.Clamp(inBay.x / bayLateralScale, -1f, 1f));

            // Round 4: the bay's size (width, length), about -1..1 over the generated range (2.9-3.6 x 4.6-5.4 m).
            // Without it, the final insertion learned on varied bays is a compromise.
            if (CurrentObservationSize == ObservationSizeWithBay)
            {
                sensor.AddObservation(Mathf.Clamp((target.size.x - 3.25f) / 0.35f, -1.5f, 1.5f));
                sensor.AddObservation(Mathf.Clamp((target.size.y - 5.0f) / 0.4f, -1.5f, 1.5f));
            }
        }

        /// <summary>Showcase: while paused the agent ignores its actions and does not end episodes (scripted drive-in).</summary>
        public bool Paused { get; set; }

        /// <summary>
        /// Evaluation only (EvaluationRunner "Action Noise"): Gaussian noise (std per action: steering, throttle) added to
        /// the action and redrawn at every new decision, i.e. sampling as in training. Unity's stochastic inference does
        /// not resample its noise, so this is how a sampled policy is evaluated in Unity. Zero = off.
        /// </summary>
        public static Vector2 EvaluationActionNoise;
        System.Random noiseRng;
        static int noiseSeedCounter;
        float lastRawSteer = float.NaN, lastRawThrottle = float.NaN, noiseSteer, noiseThrottle;

        float Gaussian()
        {
            double u1 = 1.0 - noiseRng.NextDouble(), u2 = noiseRng.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (Paused) return;
            var a = actions.ContinuousActions;
            float steer = a[0], throttle = a[1];
            if (EvaluationActionNoise != Vector2.zero)
            {
                // The same action is repeated between decisions; a changed action means a new decision: new noise.
                if (steer != lastRawSteer || throttle != lastRawThrottle)
                {
                    noiseRng ??= new System.Random(1234 + noiseSeedCounter++);
                    lastRawSteer = steer;
                    lastRawThrottle = throttle;
                    noiseSteer = Gaussian() * EvaluationActionNoise.x;
                    noiseThrottle = Gaussian() * EvaluationActionNoise.y;
                }
                steer = Mathf.Clamp(steer + noiseSteer, -1f, 1f);
                throttle = Mathf.Clamp(throttle + noiseThrottle, -1f, 1f);
            }
            car.SetInputs(steer, throttle);
            episodeSteps++;

            if (!environment.SparseReward)
            {
                AddReward(stepPenalty);
                float potential = Potential();
                AddReward(shapingScale * (potential - previousPotential));
                previousPotential = potential;
            }
            lastStepReward = GetCumulativeReward();
            UpdateDiagnostics();

            if (environment.IsOutOfBounds(transform.position))
            {
                EndWith(Outcome.OutOfBounds, collisionPenalty);
                return;
            }

            if (IsParked())
            {
                if (++parkedCounter >= holdSteps) EndWith(Outcome.Parked, successReward);
            }
            else
            {
                parkedCounter = 0;
            }

            if (enableStuckEnding && environment.EarlyEndings && outcome == Outcome.None && parkedCounter == 0 && IsStuck())
                EndWith(Outcome.Stuck, environment.SparseReward ? 0f : stuckPenalty);

            if (enableNoProgressEnding && environment.EarlyEndings && outcome == Outcome.None && parkedCounter == 0 && NoProgress())
                EndWith(Outcome.NoProgress, environment.SparseReward ? 0f : stuckPenalty);
        }

        // ---- no-progress detection: has the best potential stopped improving?

        float bestPotential;
        int bestPotentialStep;

        bool NoProgress()
        {
            float p = Potential();
            if (p > bestPotential + progressEpsilon)
            {
                bestPotential = p;
                bestPotentialStep = episodeSteps;
                return false;
            }
            return episodeSteps - bestPotentialStep >= noProgressSteps;
        }

        // ---- stuck detection: has the car stayed (almost) in one place for stuckSteps?

        Vector3 stuckAnchorPos;
        float stuckAnchorYaw;
        int stuckAnchorStep;

        void ResetStuckAnchor()
        {
            stuckAnchorPos = transform.position;
            stuckAnchorYaw = transform.eulerAngles.y;
            stuckAnchorStep = episodeSteps;
        }

        bool NearBay() => Target && (DistanceToTarget() < 3f || CornersInside(Target) > 0);

        bool IsStuck()
        {
            Vector3 moved = transform.position - stuckAnchorPos;
            moved.y = 0f;
            if (moved.magnitude > stuckDistance || Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, stuckAnchorYaw)) > stuckAngle)
            {
                ResetStuckAnchor();
                return false;
            }
            // Round 5: near or in the bay the car may stand still longer (environment parameter stuck_steps_near_bay),
            // so the agent experiences "stopped just short" and can learn to finish instead of the episode ending.
            int limit = environment.StuckStepsNearBay > 0 && NearBay() ? environment.StuckStepsNearBay : stuckSteps;
            return episodeSteps - stuckAnchorStep >= limit;
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var c = actionsOut.ContinuousActions;
            if (heuristicMode == HeuristicMode.Random)
            {
                // Baseline: uniformly random steering and throttle every decision
                c[0] = UnityEngine.Random.Range(-1f, 1f);
                c[1] = UnityEngine.Random.Range(-1f, 1f);
                return;
            }
            if (heuristicMode == HeuristicMode.RuleBased)
            {
                RuleBasedDrive(out float s, out float t);
                c[0] = s;
                c[1] = t;
                return;
            }

            // Keyboard. Project uses the new Input System only, so the legacy Input class would throw.
            float steer = 0f, throttle = 0f;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
                if (kb.spaceKey.isPressed && Mathf.Abs(car.Speed) > 0.1f) throttle = -Mathf.Sign(car.Speed); // brake
            }
            c[0] = steer;
            c[1] = throttle;
        }

        // Rule-based baseline: a deliberately simple hand-written driver (nose-in only, no obstacle avoidance).
        //  Phase 1: drive forwards to a staging point 6 m in front of the bay on its centre-line.
        //  Phase 2: follow the bay centre-line into the bay (pure pursuit on a look-ahead point 3 m ahead
        //           ON the line, so the car straightens out), creep at ~0.8 m/s, brake at the bay centre.
        //           If badly misaligned (> 30 deg) while still outside, reverse ~1.5 s to re-line up, then retry.
        // v1 of this baseline aimed at the bay centre POINT and entered at full speed: it reached the bay
        // but always stopped ~15 deg crooked (0% success).
        bool ruleEntering;
        int ruleReverseDecisions;

        const float RuleLookAhead = 3f;      // m ahead of the car, along the centre-line
        const float RuleCreepSpeed = 0.8f;   // m/s inside phase 2
        const float RuleBrakeDepth = -0.3f;  // brake once the car centre is within 0.3 m of the bay centre
        const float RuleRealignAngle = 30f;  // deg; beyond this, back up and retry
        const int RuleReverseDecisions = 15; // decisions (0.1 s each) to reverse when re-lining up

        void RuleBasedDrive(out float steer, out float throttle)
        {
            steer = 0f; throttle = 0f;
            var target = Target;
            if (!target) return;
            Vector3 bayFwd = Vector3.ProjectOnPlane(target.Forward, Vector3.up).normalized;
            Vector3 carFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Vector3 carPos = CarCenter();
            Vector3 inBay = target.transform.InverseTransformPoint(carPos); // x: sideways, z: depth (+ = further in)
            float headingErr = Vector3.Angle(carFwd, bayFwd);

            // Phase 1: reach the staging point
            Vector3 staging = target.Center - bayFwd * 6f;
            if (!ruleEntering && Vector3.Distance(Flat(carPos), Flat(staging)) < 1.5f && headingErr < RuleRealignAngle) ruleEntering = true;
            if (!ruleEntering)
            {
                Vector3 toStaging = transform.InverseTransformPoint(staging);
                steer = Mathf.Clamp(Mathf.Atan2(toStaging.x, toStaging.z) * Mathf.Rad2Deg / MaxSteerAngle(), -1f, 1f);
                throttle = toStaging.z < 0f ? 0.3f : 0.6f; // keep moving forwards; turns round if the point is behind
                return;
            }

            // Phase 2, correction: reverse to re-line up. When reversing, steering turns the car the other way,
            // so steer opposite to the side the bay axis is on.
            if (ruleReverseDecisions == 0 && headingErr > RuleRealignAngle && inBay.z < -1f) ruleReverseDecisions = RuleReverseDecisions;
            if (ruleReverseDecisions > 0)
            {
                ruleReverseDecisions--;
                float signedErr = Vector3.SignedAngle(carFwd, bayFwd, Vector3.up); // + = bay axis is clockwise of the car
                steer = -Mathf.Clamp(signedErr / RuleRealignAngle, -1f, 1f);
                throttle = -0.5f;
                return;
            }

            // Phase 2: follow the centre-line using a look-ahead point on it
            float aheadZ = Mathf.Min(inBay.z + RuleLookAhead, 2f);
            Vector3 aim = transform.InverseTransformPoint(target.transform.TransformPoint(new Vector3(0f, 0f, aheadZ)));
            steer = Mathf.Clamp(Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg / MaxSteerAngle(), -1f, 1f);

            if (inBay.z >= RuleBrakeDepth) throttle = 0f;                              // deadzone -> auto-brake
            else throttle = Mathf.Abs(car.Speed) < RuleCreepSpeed ? 0.35f : 0f;       // creep in
        }

        float MaxSteerAngle() => car ? car.maxSteerAngle : 35f;
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        void OnCollisionEnter(Collision collision)
        {
            if (Paused || !episodeRunning || outcome != Outcome.None) return;
            if (!ParkingEnvironment.IsObstacle(collision.collider)) return;

            // Soft contact (training aid, per stage): a gentle touch is penalised but survivable,
            // so the agent can learn to edge into tight bays and correct after a scrape.
            bool soft = environment.CurrentStageSettings.softContact
                        && collision.relativeVelocity.magnitude < softContactMaxSpeed
                        && softContacts < maxSoftContacts;
            if (soft)
            {
                softContacts++;
                if (!environment.SparseReward) AddReward(softContactPenalty); // sparse = success / crash only
                return;
            }
            lastCollision = CollisionKind(collision.collider);
            EndWith(Outcome.Collision, collisionPenalty);
        }

        string lastCollision = "";

        /// <summary>What was hit, for statistics: parked car, pillar, row wall (generated car parks) or boundary wall.</summary>
        static string CollisionKind(Collider c)
        {
            if (c.CompareTag("ObstacleCar")) return "car";
            string n = c.gameObject.name;
            if (n.StartsWith("Pillar")) return "pillar";
            if (n.StartsWith("Row wall")) return "row wall";
            return "boundary wall";
        }

        void OnCollisionStay(Collision collision)
        {
            if (!episodeRunning || outcome != Outcome.None) return;
            if (!environment.SparseReward && ParkingEnvironment.IsObstacle(collision.collider)) AddReward(contactStayPenalty);
        }

        // ---------------------------------------------------------------- helpers

        void EndWith(Outcome result, float reward)
        {
            outcome = result;
            AddReward(reward);
            Report(result);
            EndEpisode();
        }

        void Report(Outcome result)
        {
            var r = new EpisodeResult
            {
                outcome = result,
                steps = episodeSteps,
                stage = environment.CurrentStage,
                targetIndex = Target ? environment.spots.IndexOf(Target) : -1,
                finalDistance = DistanceToTarget(),
                finalHeadingError = Target ? Target.HeadingError(transform.forward, allowReverseParking) : 0f,
                // On timeout, ML-Agents has already zeroed the cumulative reward before
                // OnEpisodeBegin runs, so use the value saved on the last step.
                cumulativeReward = result == Outcome.Timeout ? lastStepReward : GetCumulativeReward(),
                targetName = Target ? Target.name : "",
                group = Target ? Target.Group : BayGroup.None,
                occupiedNeighbours = environment.TargetOccupiedNeighbours,
                spawnHeading = environment.SpawnYaw,
                spawnLateral = environment.SpawnLateral,
                spawnDistance = environment.SpawnDistance,
                spawnFallback = environment.SpawnFallback,
                approach = Bucket(diagTurnBeforeEntry, TurnEdges, TurnNames),
                turnBeforeEntry = diagTurnBeforeEntry,
                noseIn = result == Outcome.Parked && Target ? IsNoseIn(Target) : diagBestNoseIn,
                entries = diagEntries,
                entrySpeed = diagEntered ? diagEntrySpeed : -1f,
                softContacts = softContacts,
                layoutIndex = environment.LayoutIndex,
                seedEpoch = environment.SeedEpoch,
                collidedWith = result == Outcome.Collision ? lastCollision : "",
                spawnFar = environment.FarStart,
                spawnRoute = environment.SpawnRoute,
            };

            // Shows up in TensorBoard under "Parking/..."
            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Parking/SuccessRate", result == Outcome.Parked ? 1f : 0f);
            stats.Add(environment.IsProceduralLayout ? "Parking/SuccessRateProcedural" : "Parking/SuccessRateTrainingLot",
                      result == Outcome.Parked ? 1f : 0f);
            if (result == Outcome.Collision)   // share of collisions by what was hit
                foreach (var kind in new[] { "car", "pillar", "row wall", "boundary wall" })
                    stats.Add($"Parking/CollisionWith/{kind}", lastCollision == kind ? 1f : 0f);
            stats.Add("Parking/CollisionRate", result == Outcome.Collision ? 1f : 0f);
            stats.Add("Parking/TimeoutRate", result == Outcome.Timeout ? 1f : 0f);
            stats.Add("Parking/StuckRate", result == Outcome.Stuck ? 1f : 0f);
            AddSituationStats(stats, result);
            stats.Add("Parking/NoProgressRate", result == Outcome.NoProgress ? 1f : 0f);
            stats.Add("Parking/FinalDistance", r.finalDistance);
            stats.Add("Parking/FinalHeadingError", r.finalHeadingError);
            stats.Add("Parking/SoftContactsPerEpisode", softContacts);
            if (result == Outcome.Parked) stats.Add("Parking/StepsToPark", r.steps);
            if (Target)
            {
                // Per bay group (Inner / Outer / Corner / Sideways), to see which bays the policy struggles with
                string g = $"Parking/ByGroup/{Target.Group}/";
                stats.Add(g + "SuccessRate", result == Outcome.Parked ? 1f : 0f);
                stats.Add(g + "CollisionRate", result == Outcome.Collision ? 1f : 0f);
                stats.Add(g + "StuckRate", result == Outcome.Stuck ? 1f : 0f);
                // (by occupied neighbours: Parking/Situation/<lot>/Neighbours*, AddSituationStats)
            }

            ReportDiagnostics(result);

            if (logEpisodes)
                Debug.Log($"[{name}] {result} | steps {r.steps} | dist {r.finalDistance:F2} m | heading err {r.finalHeadingError:F1} deg | speed {car.Speed:F2} m/s | reward {r.cumulativeReward:F2}" +
                          $" | entries {diagEntries} | max corners {diagMaxCorners} | reason {FailReason()}", this);

            EpisodeFinished?.Invoke(this, r);
        }

        /// <summary>
        /// Failure patterns over training time (round 5 monitoring): outcome rates per situation
        /// under Parking/Situation/..., and what kind of stuck it was under Parking/StuckType/...
        /// </summary>
        void AddSituationStats(StatsRecorder stats, Outcome result)
        {
            var t = Target;
            if (!t) return;
            void Rates(string key)
            {
                stats.Add($"Parking/Situation/{key}/SuccessRate", result == Outcome.Parked ? 1f : 0f);
                stats.Add($"Parking/Situation/{key}/CollisionRate", result == Outcome.Collision ? 1f : 0f);
                stats.Add($"Parking/Situation/{key}/StuckRate", result == Outcome.Stuck ? 1f : 0f);
            }
            string lot = environment.IsProceduralLayout ? "Generated" : "TrainingLot";
            // One start split (2026-10-17): close (under 7 m), normal (7 m or more) or a far start. Until then
            // StartClose/StartFar (under / over 7 m) and FarStartYes/FarStartNo were logged side by side and overlapped.
            Rates($"{lot}/{(environment.FarStart ? "FarStart" : environment.SpawnDistance < 7f ? "StartClose" : "StartNormal")}");
            Rates($"{lot}/Neighbours{environment.TargetOccupiedNeighbours}");
            if (environment.IsProceduralLayout)
            {
                Rates($"Generated/Pillar{(t.layoutPillarBeside ? "Beside" : "None")}");
                Rates($"Generated/Aisle{(t.layoutAisleWidth > 0f && t.layoutAisleWidth < 8f ? "Narrow" : "Wide")}");
                Rates($"Generated/HardCase{(environment.HardCase ? "Yes" : "No")}");
            }
            if (result == Outcome.Stuck)
            {
                string kind = episodeSteps <= stuckSteps + 5 ? "Frozen" : NearBay() ? "NearBay" : "Elsewhere";
                foreach (var k in new[] { "Frozen", "NearBay", "Elsewhere" })
                    stats.Add($"Parking/StuckType/{k}", kind == k ? 1f : 0f);
            }
        }

        bool IsParked()
        {
            var target = Target;
            if (!target) return false;
            if (Mathf.Abs(car.Speed) > stopSpeed) return false;
            if (target.HeadingError(transform.forward, allowReverseParking) > headingTolerance) return false;

            return CornersInside(target) == 4;
        }

        // ---------------------------------------------------------------- diagnostics
        // Records, per episode, how close the car came to "parked" and which condition was missing.
        // Statistics only: no effect on rewards, actions or observations.

        int diagEntries;            // times the car centre entered the bay rectangle
        bool diagWasCentreIn;
        int diagMaxCorners;         // most footprint corners ever inside the bay (0-4)
        float diagBestOutside;      // smallest summed distance of the corners outside the bay
        float diagBestDepth;        // car-centre depth along the bay axis at that best moment (+ = further in)
        float diagBestLateral;      // |sideways offset| from the centre-line at that best moment
        bool diagAll4;              // all 4 corners inside at some point
        float diagMinHeadingAll4;   // smallest heading error while all 4 corners were inside
        bool diagAll4Aligned;       // all 4 inside AND heading within tolerance at some point
        float diagMinSpeedAligned;  // lowest |speed| while all 4 inside and aligned
        int diagStepsAligned;       // physics steps spent all-in and aligned
        float diagTurnBeforeEntry;  // total |yaw change| (deg) before the car first got all 4 corners in
        float diagPrevYaw;
        bool diagEntered;           // all 4 corners in at least once
        float diagEntrySpeed;       // |speed| at that first full entry
        bool diagBestNoseIn;        // facing direction at the closest-to-inside moment (nose-in vs reverse-in)

        void ResetDiagnostics()
        {
            diagTurnBeforeEntry = 0f;
            diagPrevYaw = transform.eulerAngles.y;
            diagEntered = false;
            diagEntrySpeed = 0f;
            diagBestNoseIn = true;
            diagEntries = 0;
            diagWasCentreIn = false;
            diagMaxCorners = 0;
            diagBestOutside = float.MaxValue;
            diagBestDepth = diagBestLateral = 0f;
            diagAll4 = diagAll4Aligned = false;
            diagMinHeadingAll4 = 180f;
            diagMinSpeedAligned = float.MaxValue;
            diagStepsAligned = 0;
        }

        void UpdateDiagnostics()
        {
            var target = Target;
            if (!target) return;

            Vector3 centre = CarCenter();
            bool centreIn = target.ContainsPoint(centre, 0f);
            if (centreIn && !diagWasCentreIn) diagEntries++;
            diagWasCentreIn = centreIn;

            int corners = 0;
            float outside = 0f;
            Vector3 half = body.size * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = body.transform.TransformPoint(body.center + new Vector3(sx * half.x, 0f, sz * half.z));
                float d = target.OutsideDistance(corner, positionMargin);
                if (d <= 0f) corners++;
                outside += d;
            }
            diagMaxCorners = Mathf.Max(diagMaxCorners, corners);
            if (outside < diagBestOutside)
            {
                diagBestOutside = outside;
                Vector3 local = target.transform.InverseTransformPoint(centre);
                diagBestDepth = local.z;
                diagBestLateral = Mathf.Abs(local.x);
                diagBestNoseIn = IsNoseIn(target);
            }

            float yaw = transform.eulerAngles.y;
            if (!diagEntered) diagTurnBeforeEntry += Mathf.Abs(Mathf.DeltaAngle(diagPrevYaw, yaw));
            diagPrevYaw = yaw;
            if (corners == 4 && !diagEntered)
            {
                diagEntered = true;
                diagEntrySpeed = Mathf.Abs(car.Speed);
            }

            if (corners == 4)
            {
                diagAll4 = true;
                float heading = target.HeadingError(transform.forward, allowReverseParking);
                diagMinHeadingAll4 = Mathf.Min(diagMinHeadingAll4, heading);
                if (heading <= headingTolerance)
                {
                    diagAll4Aligned = true;
                    diagStepsAligned++;
                    diagMinSpeedAligned = Mathf.Min(diagMinSpeedAligned, Mathf.Abs(car.Speed));
                }
            }
        }

        /// <summary>The furthest "parked" condition reached in a failed episode.</summary>
        string FailReason()
        {
            if (diagEntries == 0 && diagMaxCorners == 0) return "NeverInBay";
            if (!diagAll4) return "PartlyIn";               // sideways or depth: never all 4 corners inside
            if (!diagAll4Aligned) return "InButCrooked";    // all inside, but never within the heading tolerance
            return "InAlignedNotStopped";                   // all inside and aligned, but never held still for the hold time
        }

        static readonly string[] FailReasons = { "NeverInBay", "PartlyIn", "InButCrooked", "InAlignedNotStopped" };

        /// <summary>True if the car faces into the bay (front towards the bay's far end), false if reversed in.</summary>
        bool IsNoseIn(ParkingSpot target) => Vector3.Dot(transform.forward, target.Forward) >= 0f;

        static string Bucket(float v, float[] edges, string[] names)
        {
            for (int i = 0; i < edges.Length; i++) if (v < edges[i]) return names[i];
            return names[edges.Length];
        }

        static readonly float[] HeadingEdges = { 15f, 30f, 45f, 90f, 135f };
        static readonly string[] HeadingNames = { "00-15", "15-30", "30-45", "45-90", "90-135", "135-180" };
        static readonly float[] LateralEdges = { 0.5f, 1f };
        static readonly string[] LateralNames = { "0-0.5m", "0.5-1m", "1m+" };
        static readonly float[] TurnEdges = { 45f, 135f };
        static readonly string[] TurnNames = { "Straight", "Curved", "UTurn" };

        /// <summary>
        /// v11 logging: success rate by spawn pose, by approach shape and by finish direction, plus entry speed.
        /// "Finish direction" is the direction when parked (successes) or at the closest-to-inside moment (failures).
        /// </summary>
        void ReportApproachStats(Outcome result)
        {
            var stats = Academy.Instance.StatsRecorder;
            var target = Target;
            if (!target) return;
            float success = result == Outcome.Parked ? 1f : 0f;

            if (!environment.SpawnFallback)
            {
                stats.Add($"Parking/Spawn/Heading/{Bucket(Mathf.Abs(environment.SpawnYaw), HeadingEdges, HeadingNames)}/SuccessRate", success);
                stats.Add($"Parking/Spawn/Lateral/{Bucket(Mathf.Abs(environment.SpawnLateral), LateralEdges, LateralNames)}/SuccessRate", success);
            }

            string approach = Bucket(diagTurnBeforeEntry, TurnEdges, TurnNames);
            stats.Add($"Parking/Approach/{approach}/SuccessRate", success);
            stats.Add($"Parking/Approach/{approach}/Share", 1f);
            foreach (var a in TurnNames) if (a != approach) stats.Add($"Parking/Approach/{a}/Share", 0f);
            stats.Add("Parking/Approach/TurnBeforeEntryDeg", diagTurnBeforeEntry);

            bool noseIn = result == Outcome.Parked ? IsNoseIn(target) : diagBestNoseIn;
            string dir = noseIn ? "NoseIn" : "ReverseIn";
            stats.Add($"Parking/Finish/{dir}/SuccessRate", success);
            stats.Add("Parking/Finish/ReverseInShare", noseIn ? 0f : 1f);
            if (diagEntered)
            {
                stats.Add($"Parking/Finish/{dir}/EntrySpeed", diagEntrySpeed);
            }
        }

        void ReportDiagnostics(Outcome result)
        {
            var stats = Academy.Instance.StatsRecorder;
            ReportApproachStats(result);

            if (result == Outcome.Parked)
            {
                stats.Add("Parking/Diag/EntriesOnSuccess", diagEntries);
                return;
            }

            // Shares of failed episodes (each failure adds 1 to its reason and 0 to the others)
            string reason = FailReason();
            foreach (var r in FailReasons)
            {
                float v = r == reason ? 1f : 0f;
                stats.Add($"Parking/Diag/FailReason/{r}", v);
            }

            stats.Add("Parking/Diag/EntriesOnFail", diagEntries);
            stats.Add("Parking/Diag/MaxCornersOnFail", diagMaxCorners);
            if (diagMaxCorners > 0)
            {
                // Signed depth (+ too deep, - too shallow) and sideways offset at the closest-to-inside moment
                stats.Add("Parking/Diag/BestDepthOnFail", diagBestDepth);
                stats.Add("Parking/Diag/BestLateralOnFail", diagBestLateral);
            }
            if (diagAll4)
            {
                stats.Add("Parking/Diag/MinHeadingAll4OnFail", diagMinHeadingAll4);
            }
            if (diagAll4Aligned)
            {
                stats.Add("Parking/Diag/MinSpeedAlignedOnFail", diagMinSpeedAligned);
                stats.Add("Parking/Diag/StepsAlignedOnFail", diagStepsAligned);
            }
        }

        /// <summary>How many of the car footprint's 4 corners lie inside the bay (with margin).</summary>
        int CornersInside(ParkingSpot target)
        {
            int inside = 0;
            Vector3 half = body.size * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = body.transform.TransformPoint(body.center + new Vector3(sx * half.x, 0f, sz * half.z));
                if (target.ContainsPoint(corner, positionMargin)) inside++;
            }
            return inside;
        }

        /// <summary>Read-only snapshot for ScenarioRunner traces (no effect on rewards, actions or observations).</summary>
        public struct TraceInfo
        {
            public float distance, headingError, depth, lateral;           // m, deg, m (+ = further in), m
            public float potential, inBay, lineUp, stop, approach;         // shaping terms (unweighted 0..1, potential weighted)
            public int corners;                                            // footprint corners inside the target (0-4)
            public bool slowEnough, alignedEnough, parkedNow;
        }

        /// <summary>Showcase debug overlay: episode counters behind the endings (read-only, no effect on training).</summary>
        public struct DebugCounters
        {
            public int steps, parkedHold, holdSteps, softContacts;
            public int sinceProgress, noProgressSteps, sinceMoved, stuckSteps;
            public bool noProgressOn, stuckOn;
        }

        public DebugCounters GetDebugCounters() => new DebugCounters
        {
            steps = episodeSteps, parkedHold = parkedCounter, holdSteps = holdSteps, softContacts = softContacts,
            sinceProgress = episodeSteps - bestPotentialStep, noProgressSteps = noProgressSteps,
            sinceMoved = episodeSteps - stuckAnchorStep, stuckSteps = stuckSteps,
            noProgressOn = enableNoProgressEnding && environment && environment.EarlyEndings,
            stuckOn = enableStuckEnding && environment && environment.EarlyEndings,
        };

        public TraceInfo GetTrace()
        {
            var t = Target;
            var info = new TraceInfo();
            if (!t) return info;
            info.distance = DistanceToTarget();
            info.headingError = t.HeadingError(transform.forward, allowReverseParking);
            Vector3 inBayPos = t.transform.InverseTransformPoint(CarCenter());
            info.depth = inBayPos.z;
            info.lateral = inBayPos.x;
            info.inBay = InBayScore(t);
            info.lineUp = LineUpScore(t, info.distance);
            info.stop = StopScore(info.inBay, info.headingError);
            info.approach = SlowApproachScore(info.distance);
            info.potential = Potential();
            info.corners = CornersInside(t);
            info.slowEnough = Mathf.Abs(car.Speed) <= stopSpeed;
            info.alignedEnough = info.headingError <= headingTolerance;
            info.parkedNow = IsParked();
            return info;
        }

        /// <summary>
        /// Shaping potential (reward v4): closer is better, being aligned matters more near the bay,
        /// getting the whole footprint inside the lines is worth extra, and near the bay being on its
        /// centre-line is worth extra (line up first, then drive straight in).
        /// Both the alignment and in-bay terms are linear near the goal, so straightening the last
        /// few degrees or nudging the last corner in still earns a clear reward
        /// (v2 used cos(error), which is flat near 0°, and a corner count, which only changes in jumps).
        /// Reward each step is the change in potential, so driving in circles earns nothing.
        /// </summary>
        float Potential()
        {
            var target = Target;
            if (!target) return 0f;
            float distance = DistanceToTarget();
            float closeness = Mathf.Clamp01(1f - distance / alignmentRadius);
            float headingError = target.HeadingError(transform.forward, allowReverseParking);
            float alignment = 1f - headingError / 90f;
            float inBay = InBayScore(target);
            // Round 6: with route_distance on, the far-range term uses the route along drivable space (DrivableMap), so
            // going round a row of parked cars is rewarded instead of driving into it. Off: the straight line (unchanged).
            return -environment.RewardDistance(CarCenter()) / maxObservationDistance
                 + alignmentWeight * closeness * alignment
                 + inBayWeight * inBay
                 + lineUpWeight * LineUpScore(target, distance)
                 + stopWeight * StopScore(inBay, headingError)
                 + approachWeight * SlowApproachScore(distance);
        }

        /// <summary>
        /// 0..1 (reward v10): near the bay AND slow, so speed is shed before the car enters, not after.
        /// Potential-based: speeding up and slowing down again nets zero.
        /// </summary>
        float SlowApproachScore(float distance)
        {
            float near = Mathf.Clamp01(1f - distance / approachRadius);
            float slow = Mathf.Clamp01(1f - Mathf.Abs(car.Speed) / approachSpeedScale);
            return near * slow;
        }

        /// <summary>
        /// 0..1 (reward v9): slow AND inside the bay AND aligned. Smooth in all three, so braking in the
        /// right spot is rewarded step by step. Potential-based: driving in fast and out again nets zero.
        /// </summary>
        float StopScore(float inBay, float headingError)
        {
            float aligned = Mathf.Clamp01(1f - headingError / (2f * headingTolerance));
            float slow = Mathf.Clamp01(1f - Mathf.Abs(car.Speed) / stopSpeedScale);
            return inBay * aligned * slow;
        }

        /// <summary>
        /// 0..1: how well the car is lined up for a straight entry: on the bay's centre-line AND
        /// pointing along it, weighted by how near the bay it is. A curved approach only scores
        /// at the very end, so lining up early is worth more (reward v5).
        /// </summary>
        float LineUpScore(ParkingSpot target, float distance)
        {
            float sideways = Mathf.Abs(target.transform.InverseTransformPoint(CarCenter()).x);
            float onLine = Mathf.Clamp01(1f - sideways / lineUpTolerance);
            float facing = Mathf.Clamp01(1f - target.HeadingError(transform.forward, allowReverseParking) / lineUpHeadingTolerance);
            float near = Mathf.Clamp01(1f - distance / lineUpRadius);
            return near * onLine * facing;
        }

        /// <summary>0..1: average over the 4 footprint corners of how close each is to being inside the bay.</summary>
        float InBayScore(ParkingSpot target)
        {
            float sum = 0f;
            Vector3 half = body.size * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 corner = body.transform.TransformPoint(body.center + new Vector3(sx * half.x, 0f, sz * half.z));
                sum += Mathf.Clamp01(1f - target.OutsideDistance(corner, positionMargin) / inBayFalloff);
            }
            return sum / 4f;
        }

        float DistanceToTarget()
        {
            if (!Target) return 0f;
            Vector3 d = CarCenter() - Target.Center;
            d.y = 0f;
            return d.magnitude;
        }

        Vector3 CarCenter() => body ? body.transform.TransformPoint(body.center) : transform.position;

        float SignedHeadingToBay(ParkingSpot target)
        {
            Vector3 carForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Vector3 bayForward = Vector3.ProjectOnPlane(target.Forward, Vector3.up);
            return Vector3.SignedAngle(carForward, bayForward, Vector3.up);
        }
    }
}
