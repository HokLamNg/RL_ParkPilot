using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ParkPilot
{
    /// <summary>
    /// Driver-facing interface of the showcase (the view half of ShowcaseController): main menu, a guided four-step flow
    /// (capture, choose a bay, drive in, park) with an instruction card and the keyboard shortcut on every button, live
    /// parking feedback, a result screen (success / failure with the reason), retry, pause, help, settings and exit.
    ///
    /// The interface itself is a uGUI prefab in the scene (ShowcaseUIView, Assets/Project/UI/ShowcaseUI.prefab),
    /// edited in the Scene view. This file only decides what is shown, fills in
    /// the live values and reacts to buttons and keys. The developer debug overlay (D) is still drawn with OnGUI
    /// (ShowcaseController.DrawDebugPanel).
    /// </summary>
    public partial class ShowcaseController
    {
        enum Overlay { None, Help, Settings, Pause, ConfirmExit, ConfirmBay }

        [Header("Interface (scene object)")]
        [Tooltip("The ShowcaseUI prefab instance in the scene. Empty = the first ShowcaseUIView found.")]
        public ShowcaseUIView ui;

        Overlay overlay;
        float savedTimeScale = 1f;
        bool timeFrozen;
        int hoverBay = -1, pendingBay = -1;
        string toast = "";
        float toastUntil;
        bool hasResult;
        ParkingAgent.EpisodeResult result;
        float resultSeconds, flashStart = -10f;
        Color flashColour;
        bool settingCentre;                      // the hand-over the driver chose in Settings
        bool settingAlternative;                 // round 6: the driver chose the alternative model (DQN) in Settings

        /// <summary>The parking policy used for the next parking: Model, or Alternative Model if chosen in Settings.</summary>
        ModelAsset ActiveModel => settingAlternative && alternativeModel ? alternativeModel : model;

        static string Algorithm(ModelAsset m) => !m ? "" : m.name.ToLowerInvariant().Contains("dqn") ? "DQN" : "PPO";

        /// <summary>Switch the agent to the chosen model (only when it changes; same inputs, checked at start).</summary>
        void ApplyModel()
        {
            var m = ActiveModel;
            var bp = agent.GetComponent<BehaviorParameters>();
            if (!m || bp.Model == m) return;
            agent.SetModel(bp.BehaviorName, m, bp.InferenceDevice);
            bp.DeterministicInference = true;
        }

        // photo screen: one dot per visible bay, 4 edges for the hover outline
        Texture shownPhoto;
        readonly List<Image> bayDots = new List<Image>();
        readonly Image[] hoverEdges = new Image[4];
        Canvas canvas;

        Color Good => ui ? ui.success : new Color(0.2f, 0.78f, 0.36f);
        Color Bad => ui ? ui.failure : new Color(0.93f, 0.27f, 0.27f);

        void InitUI()
        {
            // Default: hand over in the bay's aisle, the shorter drive. The centre hand-over starts far bays 15-24 m away,
            // often behind the car. round5_s1 failed there (centre 22/30), the round 6 models park from both
            // (round6_s1 and rdqn2_s2 centre 30/30).
            handoverAtCentre = false;
            settingCentre = false;
            settingAlternative = false;
            if (alternativeModel)
            {
                var a = ParkingAgent.ModelInputSizes(model);
                var b = ParkingAgent.ModelInputSizes(alternativeModel);
                if (!a.SequenceEqual(b))
                {
                    Debug.LogError($"[Showcase] Alternative model {alternativeModel.name} has inputs ({string.Join(", ", b)}), " +
                                   $"{model.name} has ({string.Join(", ", a)}). Both must match to switch at runtime; choice disabled.", this);
                    alternativeModel = null;
                }
            }
            if (!ui) ui = FindFirstObjectByType<ShowcaseUIView>(FindObjectsInactive.Include);
            if (!ui)
            {
                Debug.LogError("[Showcase] No interface in the scene: add Assets/Project/UI/ShowcaseUI.prefab and assign it to ShowcaseController.ui. " +
                               "Keys still work (Enter, C, T, R, M, Esc, H, D).", this);
                return;
            }
            canvas = ui.GetComponentInParent<Canvas>();
            if (canvas) canvas = canvas.rootCanvas;
            if (!EventSystem.current) Debug.LogWarning("[Showcase] No EventSystem in the scene: buttons will not react to the mouse.", this);
            WireButtons();
        }

        static void On(Button b, UnityEngine.Events.UnityAction a)
        {
            if (b) b.onClick.AddListener(a);
        }

        void WireButtons()
        {
            var v = ui;
            On(v.menu.start, StartFromMenu);
            On(v.menu.howItWorks, () => Open(Overlay.Help));
            On(v.menu.settings, () => Open(Overlay.Settings));
            On(v.menu.exit, () => Open(Overlay.ConfirmExit));
            On(v.topBar.help, () => Open(Overlay.Help));
            On(v.topBar.menu, () => Open(Overlay.Pause));
            On(v.arrive.capture, Capture);
            On(v.arrive.newCarPark, NewCarPark);
            On(v.photo.retake, Capture);
            On(v.photo.back, CancelPhoto);
            On(v.driving.cancel, CancelRun);
            On(v.parking.cancel, CancelRun);
            On(v.result.again, TryAgain);
            On(v.result.anotherBay, ChooseAnotherBay);
            On(v.result.newCarPark, NewCarPark);
            On(v.result.mainMenu, MainMenu);
            On(v.confirmBay.park, ConfirmPark);
            On(v.confirmBay.cancel, CloseOverlay);
            On(v.confirmExit.exit, ExitApp);
            On(v.confirmExit.cancel, CloseOverlay);
            On(v.pause.resume, CloseOverlay);
            On(v.pause.cancelParking, CancelRun);
            On(v.pause.newCarPark, NewCarPark);
            On(v.pause.help, () => overlay = Overlay.Help);
            On(v.pause.settings, () => overlay = Overlay.Settings);
            On(v.pause.mainMenu, MainMenu);
            On(v.pause.exit, () => overlay = Overlay.ConfirmExit);
            On(v.help.close, CloseOverlay);
            On(v.settings.handover, () => settingCentre = !settingCentre);
            On(v.settings.less, () => occupiedShare = Mathf.Max(0.1f, Mathf.Round((occupiedShare - 0.1f) * 10f) / 10f));
            On(v.settings.more, () => occupiedShare = Mathf.Min(0.9f, Mathf.Round((occupiedShare + 0.1f) * 10f) / 10f));
            On(v.settings.sensorView, () => debugMode = !debugMode);
            On(v.settings.batch, () =>
            {
                if (phase != Phase.Arrive && phase != Phase.Menu) return;
                CloseOverlay();
                if (phase == Phase.Menu) Restart();
                StartCoroutine(BatchTest());
            });
            On(v.settings.done, CloseOverlay);
            On(v.settings.model, () => { if (alternativeModel) settingAlternative = !settingAlternative; });
        }

        // ------------------------------------------------------------------ input

        void HandleKeys(Keyboard kb)
        {
            if (kb == null) return;
            bool esc = kb.escapeKey.wasPressedThisFrame, enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;

            if (overlay != Overlay.None)
            {
                if (overlay == Overlay.ConfirmBay && enter) ConfirmPark();
                else if (overlay == Overlay.ConfirmExit && enter) ExitApp();
                else if (overlay == Overlay.Pause && kb.xKey.wasPressedThisFrame && (phase == Phase.Driving || phase == Phase.Parking)) CancelRun();
                else if (esc) CloseOverlay();
                return;
            }
            if (phase == Phase.Menu)
            {
                if (enter) StartFromMenu();
                if (kb.hKey.wasPressedThisFrame) Open(Overlay.Help);
                if (esc) Open(Overlay.ConfirmExit);
                return;
            }
            if (phase == Phase.Generating) return;
            if (kb.hKey.wasPressedThisFrame) { Open(Overlay.Help); return; }
            if (kb.dKey.wasPressedThisFrame) debugMode = !debugMode;
            switch (phase)
            {
                case Phase.Arrive:
                    if (kb.cKey.wasPressedThisFrame) Capture();
                    else if (kb.rKey.wasPressedThisFrame) NewCarPark();
                    else if (kb.bKey.wasPressedThisFrame) StartCoroutine(BatchTest());
                    else if (kb.gKey.wasPressedThisFrame) StartCoroutine(GenerateSynthetic());
                    else if (esc) Open(Overlay.Pause);
                    break;
                case Phase.Photo:
                    if (kb.cKey.wasPressedThisFrame) Capture();
                    else if (esc) CancelPhoto();
                    break;
                case Phase.Driving:
                case Phase.Parking:
                    if (kb.xKey.wasPressedThisFrame) CancelRun();
                    else if (esc) Open(Overlay.Pause);
                    break;
                case Phase.Done:
                    if (kb.tKey.wasPressedThisFrame) TryAgain();
                    else if (kb.cKey.wasPressedThisFrame) ChooseAnotherBay();
                    else if (kb.rKey.wasPressedThisFrame) NewCarPark();
                    else if (kb.mKey.wasPressedThisFrame) MainMenu();
                    else if (esc) Open(Overlay.Pause);
                    break;
            }
        }

        // ------------------------------------------------------------------ actions

        void Open(Overlay o)
        {
            if (batchRunning) return;
            overlay = o;
            if ((phase == Phase.Driving || phase == Phase.Parking) && o != Overlay.ConfirmBay && !timeFrozen)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                timeFrozen = true;
            }
        }

        void CloseOverlay()
        {
            overlay = Overlay.None;
            pendingBay = -1;
            if (timeFrozen) { Time.timeScale = savedTimeScale; timeFrozen = false; }
        }

        void StartFromMenu()
        {
            CloseOverlay();
            Restart();                                   // a fresh car park every time the driver starts
        }

        void MainMenu()
        {
            CloseOverlay();
            Restart();
            phase = Phase.Menu;
        }

        void NewCarPark()
        {
            CloseOverlay();
            Restart();
            Toast("New car park");
        }

        void CancelRun()
        {
            CloseOverlay();
            ResetCarToStart();
            phase = Phase.Arrive;
            Toast("Parking cancelled. The car is back at the start.");
        }

        void ConfirmPark()
        {
            int i = pendingBay;
            CloseOverlay();
            if (i < 0) return;
            handoverAtCentre = settingCentre;
            ParkIn(i);
        }

        void TryAgain()
        {
            if (target < 0) return;
            int t = target;
            bool failed = hasResult && result.outcome != ParkingAgent.Outcome.Parked;
            // The policy is deterministic: the same start gives the same result. After a failure, approach the bay
            // the other way (aisle or car park centre hand-over), so the attempt is genuinely different.
            if (failed) handoverAtCentre = !handoverAtCentre;
            CloseOverlay();
            ResetCarToStart(atEntrance: true);           // straight from the car park entrance, no drive through town
            ParkIn(t);
            Toast(failed ? (handoverAtCentre ? "Trying again from the entrance, handing over at the car park centre"
                                             : "Trying again from the entrance, handing over in the bay's aisle")
                         : "Parking again, starting at the car park entrance");
        }

        void ChooseAnotherBay()
        {
            CloseOverlay();
            ResetCarToStart();
            if (photo == null) { phase = Phase.Arrive; return; }
            DrawPhoto();
            phase = Phase.Photo;
        }

        void ExitApp()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Toast(string text, float seconds = 2.5f)
        {
            toast = text;
            toastUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Called when the RL episode ends (ShowcaseController.OnEpisodeFinished).</summary>
        void ShowResult(ParkingAgent.EpisodeResult r, float seconds)
        {
            hasResult = true;
            result = r;
            resultSeconds = seconds;
            bool ok = r.outcome == ParkingAgent.Outcome.Parked;
            flashColour = ok ? Good : Bad;
            flashStart = Time.unscaledTime;
            if (target >= 0)
            {
                env.spots[target].SetIndicatorVisible(true);
                env.spots[target].SetIndicatorOverride(ok ? Good : Bad);
            }
        }

        string FailReason()
        {
            switch (result.outcome)
            {
                case ParkingAgent.Outcome.Collision:
                    string what = result.collidedWith switch
                    {
                        "car" => "a parked car", "pillar" => "a pillar", "row wall" => "a wall", "boundary wall" => "the car park wall", _ => "an obstacle",
                    };
                    return $"The car touched {what}. The assistant stopped the manoeuvre.";
                case ParkingAgent.Outcome.Stuck: return "The car stopped before it was fully in the bay.";
                case ParkingAgent.Outcome.NoProgress: return "The car stopped getting closer to the bay.";
                case ParkingAgent.Outcome.Timeout: return "The manoeuvre took too long.";
                case ParkingAgent.Outcome.OutOfBounds: return "The car left the car park area.";
                default: return "The manoeuvre did not finish.";
            }
        }

        string FailTip() => result.outcome == ParkingAgent.Outcome.Collision
            ? "Bays between two parked cars or next to a pillar are the hardest. Try again with a different approach, or choose a bay with more room."
            : "Try again with a different approach, or choose another bay.";

        static string BayName(string n) => n.StartsWith("Bay_") ? n.Substring(4).Replace('_', '-') : n;

        // ------------------------------------------------------------------ the interface, every frame (LateUpdate)

        void OnGUI()
        {
            if (debugMode && phase != Phase.Photo && phase != Phase.Menu) DrawDebugPanel();   // developer overlay, native resolution
        }

        void UpdateUI()
        {
            if (!ui) return;
            // Buttons must not stay selected: the UI module would "submit" them on Enter, on top of our own Enter key.
            var es = EventSystem.current;
            if (es && es.currentSelectedGameObject) es.SetSelectedGameObject(null);

            bool busy = batchRunning || phase == Phase.Generating;
            ShowcaseUIView.Show(ui.menu.root, !busy && phase == Phase.Menu);
            ShowcaseUIView.Show(ui.topBar.root, !busy && phase != Phase.Menu);
            ShowcaseUIView.Show(ui.arrive.root, !busy && phase == Phase.Arrive);
            ShowcaseUIView.Show(ui.photo.root, !busy && phase == Phase.Photo && photoShown);
            ShowcaseUIView.Show(ui.driving.root, !busy && phase == Phase.Driving);
            ShowcaseUIView.Show(ui.parking.root, !busy && phase == Phase.Parking);
            ShowcaseUIView.Show(ui.result.root, !busy && phase == Phase.Done && hasResult);
            ShowcaseUIView.Show(ui.busy.root, busy);
            ShowcaseUIView.Show(ui.dim, overlay != Overlay.None);
            ShowcaseUIView.Show(ui.confirmBay.root, overlay == Overlay.ConfirmBay);
            ShowcaseUIView.Show(ui.confirmExit.root, overlay == Overlay.ConfirmExit);
            ShowcaseUIView.Show(ui.pause.root, overlay == Overlay.Pause);
            ShowcaseUIView.Show(ui.help.root, overlay == Overlay.Help);
            ShowcaseUIView.Show(ui.settings.root, overlay == Overlay.Settings);

            if (busy) UpdateBusy();
            else
            {
                if (phase == Phase.Menu) UpdateMenu();
                else UpdateTopBar();
                if (phase == Phase.Photo) UpdatePhotoScreen(); else hoverBay = -1;
                if (phase == Phase.Driving) UpdateDriving();
                if (phase == Phase.Parking) UpdateParking();
                if (phase == Phase.Done && hasResult) UpdateResult();
            }
            UpdateDialogs();
            UpdateFeedback();
        }

        void UpdateBusy()
        {
            ShowcaseUIView.Set(ui.busy.title, batchRunning ? "Batch test running" : "Generating CNN training data");
            ShowcaseUIView.Set(ui.busy.status, status);
            ShowcaseUIView.Show(ui.busy.hint ? ui.busy.hint.gameObject : null, batchRunning);
        }

        void UpdateMenu()
        {
            ShowcaseUIView.Set(ui.menu.footer,
                $"Parking policy: {(ActiveModel ? $"{Algorithm(ActiveModel)} ({ActiveModel.name})" : "none")}   |   Free-bay detection: {(occupancyCnn ? "neural network (CNN)" : "scene ground truth")}");
        }

        void UpdateTopBar()
        {
            int current = phase switch { Phase.Arrive => 0, Phase.Photo => 1, Phase.Driving => 2, _ => 3 };
            bool finished = phase == Phase.Done;
            bool parkedOk = finished && hasResult && result.outcome == ParkingAgent.Outcome.Parked;
            var steps = ui.topBar.steps;
            for (int i = 0; steps != null && i < steps.Length; i++)
            {
                var s = steps[i];
                if (s == null) continue;
                bool done = i < current || parkedOk;
                bool now = i == current && !finished;
                ShowcaseUIView.Tint(s.circle, done ? ui.success : now ? ui.accent : ui.inactive);
                ShowcaseUIView.Show(s.check ? s.check.gameObject : null, done);
                ShowcaseUIView.Show(s.number ? s.number.gameObject : null, !done);
                if (s.label)
                {
                    s.label.color = now ? ui.text : ui.muted;
                    s.label.fontStyle = now ? FontStyle.Bold : FontStyle.Normal;
                }
            }
        }

        // ---- photo: bay dots, hover outline and tooltip, click to choose

        Camera UiCamera => canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        void UpdatePhotoScreen()
        {
            var p = ui.photo;
            if (!p.photo) return;
            if (shownPhoto != photoShown) RebuildBayDots();

            int free = 0;
            for (int i = 0; i < quads.Count; i++)
                if (quads[i] != null && occupiedProb[i] < occupiedThreshold) free++;
            ShowcaseUIView.Set(p.info, $"{free} free bays found. Click a green bay to park there.");
            ShowcaseUIView.Set(p.source, usedCnn ? "Detected by the occupancy neural network" : "Using the scene's ground truth");

            // Bay under the mouse (photo pixels, top-left origin).
            hoverBay = -1;
            var mouse = Mouse.current;
            RectTransform photoRect = p.photo.rectTransform;
            Vector2 m = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            if (mouse != null && overlay == Overlay.None && RectTransformUtility.RectangleContainsScreenPoint(photoRect, m, UiCamera)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(photoRect, m, UiCamera, out Vector2 local))
            {
                Rect r = photoRect.rect;
                var px = new Vector2((local.x - r.xMin) / r.width * photoWidth, (r.yMax - local.y) / r.height * photoHeight);
                hoverBay = BayAt(px);
            }

            // Hover outline
            for (int e = 0; e < 4; e++)
            {
                var edge = HoverEdge(e);
                if (!edge) continue;
                bool on = hoverBay >= 0;
                ShowcaseUIView.Show(edge.gameObject, on);
                if (!on) continue;
                bool taken = occupiedProb[hoverBay] >= occupiedThreshold;
                Vector2 a = PhotoLocal(quads[hoverBay][e]), b = PhotoLocal(quads[hoverBay][(e + 1) % 4]);
                var rt = edge.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.localPosition = (a + b) / 2f;
                rt.sizeDelta = new Vector2(Vector2.Distance(a, b), p.hoverEdgeWidth);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                edge.color = taken ? ui.failure : Color.white;
            }

            // Tooltip: line 1 the bay and whether it is free, line 2 how sure the PHOTO DETECTION is (the occupancy CNN's
            // score for its free / taken call), worded so it cannot be read as a chance of parking successfully.
            if (p.tooltip)
            {
                ShowcaseUIView.Show(p.tooltip.gameObject, hoverBay >= 0);
                if (hoverBay >= 0)
                {
                    bool taken = occupiedProb[hoverBay] >= occupiedThreshold;
                    ShowcaseUIView.Set(p.tooltipLine, $"Bay {BayName(env.spots[hoverBay].name)}:  {(taken ? "taken" : "free")}" + (taken ? "" : "  -  click to choose"));
                    ShowcaseUIView.Set(p.tooltipDetail, usedCnn
                        ? $"Photo detection is {100f * (taken ? occupiedProb[hoverBay] : 1f - occupiedProb[hoverBay]):F0}% confident this bay is {(taken ? "taken" : "free")}"
                        : "From the scene (no photo detection)");
                    PlaceTooltip(p.tooltip, m);
                }
            }

            // Click a bay
            if (hoverBay >= 0 && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                string why = RejectReason(hoverBay);
                if (why != null) Toast(why);
                else { pendingBay = hoverBay; Open(Overlay.ConfirmBay); }
            }
        }

        /// <summary>A photo pixel (top-left origin) in the photo's local UI coordinates (relative to its pivot).</summary>
        Vector2 PhotoLocal(Vector2 px)
        {
            Rect r = ui.photo.photo.rectTransform.rect;
            return new Vector2(r.xMin + px.x / photoWidth * r.width, r.yMax - px.y / photoHeight * r.height);
        }

        void RebuildBayDots()
        {
            var p = ui.photo;
            shownPhoto = photoShown;
            p.photo.texture = photoShown;
            foreach (var d in bayDots) if (d) Destroy(d.gameObject);
            bayDots.Clear();
            if (!p.bayDotTemplate) return;
            p.bayDotTemplate.gameObject.SetActive(false);
            for (int i = 0; i < quads.Count; i++)
            {
                if (quads[i] == null) continue;
                Vector2 c = Vector2.zero;
                foreach (var v in quads[i]) c += v / 4f;
                var dot = Instantiate(p.bayDotTemplate, p.bayDotTemplate.transform.parent);
                dot.name = $"Bay dot {BayName(env.spots[i].name)}";
                dot.raycastTarget = false;
                var rt = dot.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(c.x / photoWidth, 1f - c.y / photoHeight);   // follows the photo's size
                rt.anchoredPosition = Vector2.zero;
                dot.color = occupiedProb[i] >= occupiedThreshold ? ui.failure : ui.success;
                dot.gameObject.SetActive(true);
                bayDots.Add(dot);
            }
        }

        Image HoverEdge(int e)
        {
            var template = ui.photo.hoverEdgeTemplate;
            if (!template) return null;
            if (!hoverEdges[e])
            {
                template.gameObject.SetActive(false);
                hoverEdges[e] = Instantiate(template, template.transform.parent);
                hoverEdges[e].name = $"Hover edge {e}";
                hoverEdges[e].raycastTarget = false;
            }
            return hoverEdges[e];
        }

        void PlaceTooltip(RectTransform tip, Vector2 mouse)
        {
            var parent = tip.parent as RectTransform;
            if (!parent || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, mouse, UiCamera, out Vector2 lp)) return;
            tip.pivot = new Vector2(0f, 1f);
            Vector2 size = tip.rect.size;
            Rect pr = parent.rect;
            var pos = new Vector2(lp.x + 18f, lp.y - 14f);
            if (pos.x + size.x > pr.xMax - 10f) pos.x = lp.x - size.x - 18f;
            if (pos.y - size.y < pr.yMin + 10f) pos.y = lp.y + size.y + 14f;
            tip.localPosition = pos;
        }

        // ---- driving, parking, result

        void UpdateDriving()
        {
            var d = ui.driving;
            ShowcaseUIView.Set(d.title, $"Driving to bay {(target >= 0 ? BayName(env.spots[target].name) : "")}");
            ShowcaseUIView.Set(d.body, handoverAtCentre
                ? "Driving in automatically. The parking assistant takes over at the centre of the car park."
                : "Driving in automatically. The parking assistant takes over once the car is lined up in the bay's aisle.");
            ShowcaseUIView.Fill(d.progressFill, RouteProgress());
        }

        void UpdateParking()
        {
            var v = ui.parking;
            var t = agent.GetTrace();
            var k = agent.GetDebugCounters();
            ShowcaseUIView.Set(v.title, $"Parking assistant active  -  bay {(target >= 0 ? BayName(env.spots[target].name) : "")}");
            // "Gear" is the direction of travel from the sign of the speed (the car has no gearbox).
            string gear = car.Speed > 0.05f ? "D" : car.Speed < -0.05f ? "R" : "N";
            ShowcaseUIView.Set(v.gear, gear);
            if (v.gear) v.gear.color = gear == "R" ? ui.warning : ui.text;
            ShowcaseUIView.Set(v.speed, $"{Mathf.Abs(car.Speed) * 3.6f:F1} km/h");
            float steer = Mathf.Clamp(car.SteerAngle / Mathf.Max(1f, car.maxSteerAngle), -1f, 1f);
            if (v.steeringKnob)
            {
                float x = 0.5f + steer * 0.5f;
                v.steeringKnob.anchorMin = new Vector2(x, v.steeringKnob.anchorMin.y);
                v.steeringKnob.anchorMax = new Vector2(x, v.steeringKnob.anchorMax.y);
                v.steeringKnob.anchoredPosition = new Vector2(0f, v.steeringKnob.anchoredPosition.y);
            }
            ShowcaseUIView.Set(v.steeringWord, steer < -0.1f ? "left" : steer > 0.1f ? "right" : "straight");
            ShowcaseUIView.Set(v.distance, $"{t.distance:F1} m");
            ShowcaseUIView.Set(v.angle, $"{t.headingError:F0}°");
            if (v.angle) v.angle.color = t.alignedEnough ? ui.success : ui.text;
            ShowcaseUIView.Set(v.cornersText, $"Corners in bay {t.corners}/4");
            ShowcaseUIView.Tint(v.cornersChip, t.corners == 4 ? ui.success : ui.inactive);
            ShowcaseUIView.Tint(v.straightChip, t.alignedEnough ? ui.success : ui.inactive);
            ShowcaseUIView.Tint(v.stoppedChip, t.slowEnough ? ui.success : ui.inactive);
            ShowcaseUIView.Fill(v.holdFill, k.holdSteps > 0 ? (float)k.parkedHold / k.holdSteps : 0f);
        }

        void UpdateResult()
        {
            var v = ui.result;
            bool ok = result.outcome == ParkingAgent.Outcome.Parked;
            string bay = target >= 0 ? BayName(env.spots[target].name) : "";
            ShowcaseUIView.Tint(v.stripe, ok ? ui.success : ui.failure);
            ShowcaseUIView.Tint(v.iconCircle, ok ? ui.success : ui.failure);
            ShowcaseUIView.Show(v.iconCheck, ok);
            ShowcaseUIView.Show(v.iconCross, !ok);
            ShowcaseUIView.Set(v.title, ok ? "Parked" : "Parking failed");
            ShowcaseUIView.Set(v.subtitle, ok ? $"The car is parked in bay {bay}." : FailReason());
            ShowcaseUIView.Show(v.stats, ok);
            ShowcaseUIView.Show(v.failTip ? v.failTip.gameObject : null, !ok);
            if (ok)
            {
                string rating = result.finalDistance < 0.2f && result.finalHeadingError < 2f ? "Excellent"
                              : result.finalDistance < 0.35f && result.finalHeadingError < 5f ? "Good" : "Acceptable";
                ShowcaseUIView.Set(v.time, $"{resultSeconds:F1} s");
                ShowcaseUIView.Set(v.offCentre, $"{result.finalDistance * 100f:F0} cm");
                ShowcaseUIView.Set(v.angle, $"{result.finalHeadingError:F1}°");
                ShowcaseUIView.Set(v.rating, rating);
                if (v.rating) v.rating.color = rating == "Excellent" ? ui.success : ui.text;
            }
            else ShowcaseUIView.Set(v.failTip, FailTip());
            ShowcaseUIView.Set(ShowcaseUIView.Label(v.again), ok ? "Park again" : "Try again, another approach");
            ui.Style(v.again, !ok);                      // the suggested next step is the primary button
            ui.Style(v.anotherBay, ok);
        }

        // ---- dialogs and feedback

        void UpdateDialogs()
        {
            if (overlay == Overlay.ConfirmBay)
                ShowcaseUIView.Set(ui.confirmBay.title, $"Park in bay {(pendingBay >= 0 ? BayName(env.spots[pendingBay].name) : "")}?");
            if (overlay == Overlay.Pause)
            {
                var b = ui.pause.cancelParking;
                ShowcaseUIView.Show(b ? b.gameObject : null, phase == Phase.Driving || phase == Phase.Parking);
            }
            if (overlay == Overlay.Settings)
            {
                var s = ui.settings;
                ShowcaseUIView.Set(ShowcaseUIView.Label(s.handover), settingCentre ? "At the car park centre" : "In the bay's aisle");
                ShowcaseUIView.Set(s.fullness, $"How full the car park is: {occupiedShare * 100f:F0}%");
                ShowcaseUIView.Set(ShowcaseUIView.Label(s.sensorView), debugMode ? "On" : "Off");
                ShowcaseUIView.Set(ShowcaseUIView.Label(s.batch), $"Run batch test ({batchRuns} parkings)");
                if (s.model)
                {
                    var m = ActiveModel;
                    ShowcaseUIView.Set(ShowcaseUIView.Label(s.model), m ? $"{Algorithm(m)}  ({m.name})" : "none");
                    s.model.interactable = alternativeModel;
                }
                if (s.batch) s.batch.interactable = phase == Phase.Arrive || phase == Phase.Menu;
            }
        }

        void UpdateFeedback()
        {
            if (ui.toast)
            {
                bool on = Time.unscaledTime <= toastUntil && toast != "";
                ShowcaseUIView.Show(ui.toast.gameObject, on);
                if (on)
                {
                    ui.toast.alpha = Mathf.Clamp01((toastUntil - Time.unscaledTime) / 0.4f);
                    ShowcaseUIView.Set(ui.toastText, toast);
                }
            }
            if (ui.flash)
            {
                float t = Time.unscaledTime - flashStart;
                bool on = t <= 0.9f;
                ShowcaseUIView.Show(ui.flash.gameObject, on);
                if (on) ui.flash.color = new Color(flashColour.r, flashColour.g, flashColour.b, 0.35f * (1f - t / 0.9f));
            }
        }
    }
}
