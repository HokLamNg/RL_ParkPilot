using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace ParkPilot
{
    /// <summary>
    /// Evaluates a trained policy (or a baseline) on fixed, reproducible test layouts.
    ///
    /// Put it on an empty GameObject in the Training scene, assign the model, press Play
    /// (with NO trainer running). For each stage in <see cref="stages"/> it forces every TrainingArea
    /// to that stage, reseeds each area's layout generator (seed = seedBase + stage * 1000 + area),
    /// records layouts 1..<see cref="episodesPerArea"/> of every area (by layout index, see ParkingEnvironment.LayoutIndex),
    /// then moves on. With a Model Queue, every model in the queue is evaluated in turn on the same layouts.
    /// Same seeds => every model and baseline is tested on the same layouts.
    ///
    /// Output (project folder / outputFolder): one CSV row per episode, and a Markdown summary.
    /// Leave this GameObject disabled while training.
    /// </summary>
    [DefaultExecutionOrder(-1000)]   // Awake before the agents are enabled (their input size depends on the model)
    public class EvaluationRunner : MonoBehaviour
    {
        public enum Mode { Policy, Random, RuleBased }

        [Header("What to evaluate")]
        [Tooltip("Name used in the output files, e.g. the run ID and step.")]
        public string label = "curric6_ppo_s1_64M";
        public Mode mode = Mode.Policy;
        [Tooltip("Trained .onnx (Mode = Policy).")]
        public ModelAsset model;
        [Tooltip("Optional (Mode = Policy): evaluate several models one after another in one Play session, each on the " +
                 "same layouts. Each model's file name is used as its label. Overrides Model / Label when not empty.")]
        public ModelAsset[] modelQueue;
        [Tooltip("Use the policy's mean action (no exploration noise). Recommended for evaluation.")]
        public bool deterministic = true;
        [Tooltip("Sampled evaluation: Gaussian noise (std: steering, throttle) added to the mean action, redrawn at every " +
                 "decision, like training. Use the policy's own std divided by 3 (e.g. round4_bay_s1: 0.22, 0.20; " +
                 "finetune_occ_s3: 0.42, 0.35). Needs Deterministic on (Unity's stochastic mode does not resample). 0 = off.")]
        public Vector2 actionNoise = Vector2.zero;
        [Tooltip("SAC check: sets every car's throttle dead zone for this evaluation (0 = off: no auto-brake, the car " +
                 "coasts at 1.5 m/s^2 and the throttle is not rescaled). -1 = keep the car's own value (0.15).")]
        public float throttleDeadzoneOverride = -1f;

        [Header("Test set")]
        [Tooltip("Curriculum stages to evaluate, in order (e.g. 4 = random inner bay, 6 = inner + outer, 10 = full task).")]
        public int[] stages = { 4, 6, 10 };
        [Tooltip("Episodes recorded per TrainingArea per stage (40 areas x 10 = 400 episodes per stage).")]
        public int episodesPerArea = 10;
        [Tooltip("Base seed for the test layouts. Must differ from training (training uses random seeds).")]
        public int seedBase = 100000;
        [Tooltip("Test with geometry randomisation (random target-bay size, parked cars moved, turned and resized; " +
                 "seeded like the layouts). Off = the original test set, identical to all earlier evaluations.")]
        public bool geometryRandomization = false;
        [Tooltip("Test with occupancy randomisation (0-27 parked cars on the training car park, 0-90% of the bays on a " +
                 "generated car park, instead of the stage's range; seeded like the layouts). Off = the original test set.")]
        public bool occupancyRandomization = false;
        [Tooltip("Test on generated car parks (ProceduralLot: aisle width, bay size, pillars, walls; one new car park per " +
                 "layout, seeded like the layouts). Off = the training car park.")]
        public bool proceduralLayouts = false;
        [Tooltip("Round 6: every episode starts far from the bay, anywhere in the aisles (10-40 m route distance, any heading, " +
                 "on the training car park partly the showcase's centre hand-over pose; seeded like the layouts). Off = the usual " +
                 "starts in front of the bay.")]
        public bool farStarts = false;

        [Header("Run")]
        public float timeScale = 20f;
        public bool quitWhenDone = true;
        [Tooltip("Folder (relative to the project folder) for the CSV and summary.")]
        public string outputFolder = "Evaluation";

        readonly List<ParkingEnvironment> envs = new List<ParkingEnvironment>();
        readonly Dictionary<ParkingAgent, int> areaOf = new Dictionary<ParkingAgent, int>();
        readonly List<(int stage, ParkingAgent.EpisodeResult r)> rows = new List<(int, ParkingAgent.EpisodeResult)>();
        int stageIndex = -1;
        int[] counts;
        bool[,] recorded;                 // [area, layout - 1]: already recorded this stage
        StreamWriter csv;
        string basePath;
        bool finished;
        readonly List<ModelAsset> jobs = new List<ModelAsset>();
        int jobIndex;
        ModelAsset currentModel;

        void Awake()
        {
            // Bay-size observations follow the model(s): 9 inputs (before round 4) or 11. One size per session.
            if (mode != Mode.Policy || Academy.Instance.IsCommunicatorOn) return;
            var all = (modelQueue != null && modelQueue.Any(m => m) ? modelQueue.Where(m => m) : new[] { model }).Where(m => m).ToList();
            var signatures = all.Select(m => string.Join("+", ParkingAgent.ModelInputSizes(m))).Where(s => s != "").Distinct().ToList();
            if (signatures.Count > 1)
                Debug.LogError($"[Evaluation] The model queue mixes models with different inputs ({string.Join(" / ", signatures)}). " +
                               "Evaluate them in separate sessions.", this);
            if (all.Count > 0) ParkingAgent.ConfigureFor(all[0]);
        }

        void Start()
        {
            if (Academy.Instance.IsCommunicatorOn)
            {
                Debug.LogWarning("[EvaluationRunner] A trainer is connected - evaluation disabled. Disable this GameObject while training.", this);
                enabled = false;
                return;
            }
            ParkingAgent.EvaluationActionNoise = mode == Mode.Policy ? actionNoise : Vector2.zero;
            if (actionNoise != Vector2.zero && !deterministic)
                Debug.LogWarning("[EvaluationRunner] Action Noise is meant for Deterministic on (mean action + noise).", this);
            bool useQueue = mode == Mode.Policy && modelQueue != null && modelQueue.Any(m => m);
            if (useQueue) jobs.AddRange(modelQueue.Where(m => m));
            else jobs.Add(model);
            if (mode == Mode.Policy && !jobs[0])
            {
                Debug.LogError("[EvaluationRunner] Mode = Policy needs a model (.onnx) or a Model Queue.", this);
                enabled = false;
                return;
            }

            // Stable area order (by position), so seeds map to the same areas every run
            envs.AddRange(FindObjectsByType<ParkingEnvironment>()
                .OrderBy(e => Mathf.Round(e.transform.position.z)).ThenBy(e => Mathf.Round(e.transform.position.x)));
            foreach (var agent in FindObjectsByType<ParkingAgent>())
            {
                var env = agent.GetComponentInParent<ParkingEnvironment>();
                if (!env) continue;
                areaOf[agent] = envs.IndexOf(env);
                agent.EpisodeFinished += OnEpisodeFinished;
                if (throttleDeadzoneOverride >= 0f) agent.GetComponent<CarController>().throttleDeadzone = throttleDeadzoneOverride;
            }

            Time.timeScale = timeScale;
            StartJob(0);
        }

        /// <summary>Configure all agents for job i (one model, or the baseline) and start its first stage.</summary>
        void StartJob(int i)
        {
            jobIndex = i;
            currentModel = jobs[i];
            if (jobs.Count > 1 || (modelQueue != null && modelQueue.Any(m => m))) label = currentModel.name;
            foreach (var agent in areaOf.Keys)
            {
                Configure(agent);
            }
            rows.Clear();
            stageIndex = -1;
            finished = false;
            basePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputFolder,
                $"{label}_{mode}{(mode == Mode.Policy && !deterministic ? "_stochastic" : "")}{(mode == Mode.Policy && actionNoise != Vector2.zero ? $"_noise{actionNoise.x:0.00}-{actionNoise.y:0.00}" : "")}{(throttleDeadzoneOverride >= 0f ? $"_dz{throttleDeadzoneOverride:0.00}" : "")}{(geometryRandomization ? "_geomrand" : "")}{(occupancyRandomization ? "_occrand" : "")}{(proceduralLayouts ? "_proc" : "")}{(farStarts ? "_far" : "")}_{System.DateTime.Now:yyyyMMdd-HHmmss}"));
            Directory.CreateDirectory(Path.GetDirectoryName(basePath));
            csv = new StreamWriter(basePath + ".csv", false, new UTF8Encoding(false));
            csv.WriteLine("label,mode,deterministic,stage,area,area_episode,outcome,success,steps,seconds,target_bay,group,occupied_neighbours," +
                          "spawn_heading_deg,spawn_lateral_m,spawn_distance_m,spawn_fallback,approach,turn_before_entry_deg,finish_direction," +
                          "entries,entry_speed,final_distance_m,final_heading_err_deg,soft_contacts,reward,collided_with," +
                          "bay_width_m,bay_length_m,aisle_width_m,pillar_beside,wall_behind,row_end,occupied_bays,total_bays,neighbour_left,neighbour_right,spawn_far,spawn_route_m," +
                          "mean_abs_throttle,throttle_lt_015_share,stopped_lt_015_share," +
                          "tail_lt_015_share,tail_015_030_share,tail_mean_abs_throttle,tail_mean_abs_speed,tail_direction_changes");
            Debug.Log($"[EvaluationRunner] Job {jobIndex + 1}/{jobs.Count}: {label} / {mode}: {envs.Count} areas, {areaOf.Count} agents, stages {string.Join(",", stages)}, " +
                      $"{episodesPerArea} episodes per area per stage. Writing {basePath}.csv", this);
            NextStage();
        }

        void Configure(ParkingAgent agent)
        {
            var bp = agent.GetComponent<BehaviorParameters>();
            if (mode == Mode.Policy)
            {
                bp.Model = currentModel;
                bp.DeterministicInference = deterministic;
                bp.BehaviorType = BehaviorType.InferenceOnly;
            }
            else
            {
                agent.heuristicMode = mode == Mode.Random ? ParkingAgent.HeuristicMode.Random : ParkingAgent.HeuristicMode.RuleBased;
                bp.BehaviorType = BehaviorType.HeuristicOnly;
            }
        }

        void NextStage()
        {
            stageIndex++;
            if (stageIndex >= stages.Length)
            {
                Finish();
                return;
            }
            int stage = stages[stageIndex];
            counts = new int[envs.Count];
            recorded = new bool[envs.Count, episodesPerArea];
            for (int i = 0; i < envs.Count; i++)
            {
                envs[i].stageOverride = stage;
                envs[i].geometryRandomOverride = geometryRandomization ? 1 : 0;
                envs[i].occupancyRandomOverride = occupancyRandomization ? 1 : 0;
                envs[i].proceduralOverride = proceduralLayouts ? 1f : 0f;
                envs[i].farStartOverride = farStarts ? 1f : 0f;
                envs[i].Reseed(seedBase + stage * 1000 + i);
            }
            Debug.Log($"[EvaluationRunner] Stage {stage}: collecting {episodesPerArea} episodes in each of {envs.Count} areas.", this);
        }

        void OnEpisodeFinished(ParkingAgent agent, ParkingAgent.EpisodeResult r)
        {
            if (finished || stageIndex < 0 || stageIndex >= stages.Length) return;
            int stage = stages[stageIndex];
            int area = areaOf[agent];
            // Record exactly layouts 1..episodesPerArea of the current seed in every area, so every model,
            // baseline and stage is tested on identical layouts. Episodes that started before the latest
            // reseed (stage or model switch) carry an older epoch or layout index 0 and are ignored.
            if (r.stage != stage || r.seedEpoch != envs[area].SeedEpoch) return;
            if (r.layoutIndex < 1 || r.layoutIndex > episodesPerArea || recorded[area, r.layoutIndex - 1]) return;
            recorded[area, r.layoutIndex - 1] = true;
            counts[area]++;
            rows.Add((stage, r));
            WriteRow(stage, area, r.layoutIndex, r);
            if (counts.All(c => c >= episodesPerArea)) NextStage();
        }

        void WriteRow(int stage, int area, int areaEpisode, ParkingAgent.EpisodeResult r)
        {
            var ci = CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("0.###", ci);
            // Layout of this episode (the environment has not reset yet when the result is reported).
            var env = envs[area];
            var t = env.Target;
            var around = env.TargetSurroundings();
            string B(bool b) => b ? "1" : "0";
            csv.WriteLine(string.Join(",", new[]
            {
                label, mode.ToString(), (mode == Mode.Policy && deterministic) ? "1" : "0", stage.ToString(ci), area.ToString(ci), areaEpisode.ToString(ci),
                r.outcome.ToString(), r.outcome == ParkingAgent.Outcome.Parked ? "1" : "0", r.steps.ToString(ci), F(r.steps * Time.fixedDeltaTime),
                r.targetName, r.group.ToString(), r.occupiedNeighbours.ToString(ci),
                F(r.spawnHeading), F(r.spawnLateral), F(r.spawnDistance), r.spawnFallback ? "1" : "0",
                r.approach, F(r.turnBeforeEntry), r.noseIn ? "NoseIn" : "ReverseIn",
                r.entries.ToString(ci), F(r.entrySpeed), F(r.finalDistance), F(r.finalHeadingError), r.softContacts.ToString(ci), F(r.cumulativeReward), r.collidedWith ?? "",
                t ? F(t.size.x) : "", t ? F(t.size.y) : "", t && t.layoutAisleWidth > 0f ? F(t.layoutAisleWidth) : "",
                t ? B(t.layoutPillarBeside) : "", t ? B(t.layoutWallBehind) : "", t ? B(t.layoutRowEnd) : "",
                around.occupied.ToString(ci), around.total.ToString(ci), B(around.left), B(around.right),
                B(r.spawnFar), F(r.spawnRoute),
                F(r.meanAbsThrottle), F(r.throttleLowShare), F(r.stoppedLowShare),
                F(r.tailLowShare), F(r.tailNearShare), F(r.tailMeanAbsThrottle), F(r.tailMeanAbsSpeed), r.tailDirectionChanges.ToString(ci),
            }));
            csv.Flush();
        }

        void Finish()
        {
            if (finished) return;
            finished = true;
            csv?.Close();
            csv = null;
            string summary = Summary();
            File.WriteAllText(basePath + "_summary.md", summary, new UTF8Encoding(false));
            Debug.Log($"[EvaluationRunner] Job {jobIndex + 1}/{jobs.Count} done ({label}).\n{summary}", this);
            if (jobIndex + 1 < jobs.Count)
            {
                StartJob(jobIndex + 1); // next model in the queue, same layouts
                return;
            }
            if (quitWhenDone)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        string Summary()
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            string P(double v) => (100.0 * v).ToString("0.0", ci) + "%";
            sb.AppendLine($"# Evaluation: {label} ({mode}{(mode == Mode.Policy ? (deterministic ? ", deterministic" : ", stochastic") : "")})");
            sb.AppendLine();
            if (mode == Mode.Policy && actionNoise != Vector2.zero)
                sb.AppendLine($"Action noise (sampled like training): std {actionNoise.x:0.00} steering, {actionNoise.y:0.00} throttle, redrawn each decision.\n");
            if (throttleDeadzoneOverride >= 0f)
                sb.AppendLine($"Throttle dead zone set to {throttleDeadzoneOverride:0.00} for this evaluation (trained with 0.15).\n");
            sb.AppendLine($"Seeds: {seedBase} + stage x 1000 + area; {episodesPerArea} episodes per area per stage; {envs.Count} areas." +
                          (geometryRandomization ? " Geometry randomisation ON (target bay size, parked cars moved / turned / resized)." : "") +
                          (occupancyRandomization ? " Occupancy randomisation ON (0-27 parked cars on the training car park, 0-90% of the bays on generated car parks)." : "") +
                          (proceduralLayouts ? " Procedural car parks ON (a new generated car park per layout)." : "") +
                          (farStarts ? " Far starts ON (anywhere in the aisles, 10-40 m route distance, 60 s limit)." : ""));
            sb.AppendLine();
            sb.AppendLine("| Stage | Episodes | Success | Collision | Stuck | No progress | Timeout | Out of bounds | Time to park (s) | Final dist (m) | Heading err (°) |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var g in rows.GroupBy(x => x.stage))
            {
                var list = g.Select(x => x.r).ToList();
                double Rate(ParkingAgent.Outcome o) => list.Count(r => r.outcome == o) / (double)list.Count;
                var parked = list.Where(r => r.outcome == ParkingAgent.Outcome.Parked).ToList();
                string park = parked.Count > 0 ? (parked.Average(r => r.steps) * Time.fixedDeltaTime).ToString("0.0", ci) : "-";
                sb.AppendLine($"| {g.Key} | {list.Count} | {P(Rate(ParkingAgent.Outcome.Parked))} | {P(Rate(ParkingAgent.Outcome.Collision))} | " +
                              $"{P(Rate(ParkingAgent.Outcome.Stuck))} | {P(Rate(ParkingAgent.Outcome.NoProgress))} | {P(Rate(ParkingAgent.Outcome.Timeout))} | " +
                              $"{P(Rate(ParkingAgent.Outcome.OutOfBounds))} | {park} | {list.Average(r => r.finalDistance).ToString("0.00", ci)} | " +
                              $"{list.Average(r => r.finalHeadingError).ToString("0.0", ci)} |");
            }
            sb.AppendLine();
            sb.AppendLine("**Success by bay group** (per stage):");
            sb.AppendLine();
            foreach (var g in rows.GroupBy(x => x.stage))
            {
                var parts = g.GroupBy(x => x.r.group).OrderBy(x => x.Key.ToString())
                    .Select(x => $"{x.Key} {P(x.Count(y => y.r.outcome == ParkingAgent.Outcome.Parked) / (double)x.Count())} (n={x.Count()})");
                sb.AppendLine($"- Stage {g.Key}: " + string.Join(", ", parts));
            }
            sb.AppendLine();
            sb.AppendLine("Per-episode details (spawn pose, approach, finish direction, neighbours) are in the CSV.");
            return sb.ToString();
        }

        void OnDestroy()
        {
            foreach (var agent in areaOf.Keys)
                if (agent) agent.EpisodeFinished -= OnEpisodeFinished;
            csv?.Close();
        }
    }
}
