using UnityEngine;
using UnityEngine.UI;

namespace ParkPilot
{
    /// <summary>
    /// The showcase interface as scene objects (uGUI), edited in the Scene view and the Inspector.
    /// Saved as Assets/Project/UI/ShowcaseUI.prefab. ShowcaseController
    /// (ShowcaseUI.cs) shows and hides the screens, fills in the live values and connects the buttons.
    ///
    /// Editing: move, resize, recolour, change fonts and wording freely. Objects referenced below are filled in or
    /// switched on and off by code: keep them (renaming is fine, the references stay), or re-assign the reference if you
    /// replace one. Texts that code overwrites are marked "(code)" in their object name. Button colours come from the
    /// colours below (primary / secondary), so change those rather than the Button's own colour block.
    /// </summary>
    public class ShowcaseUIView : MonoBehaviour
    {
        [Header("Colours")]
        public Color text = new Color(0.95f, 0.97f, 1f);
        public Color muted = new Color(0.68f, 0.73f, 0.8f);
        public Color accent = new Color(0.16f, 0.55f, 0.98f);
        public Color success = new Color(0.2f, 0.78f, 0.36f);
        public Color failure = new Color(0.93f, 0.27f, 0.27f);
        public Color warning = new Color(1f, 0.72f, 0.18f);
        [Tooltip("Chips and step circles that are not (yet) done.")]
        public Color inactive = new Color(1f, 1f, 1f, 0.16f);
        [Tooltip("Secondary buttons (the primary ones use Accent).")]
        public Color secondaryButton = new Color(1f, 1f, 1f, 0.12f);

        [System.Serializable]
        public class MenuScreen
        {
            public GameObject root;
            public Button start, howItWorks, settings, exit;
            public Text footer;
        }

        [System.Serializable]
        public class Step
        {
            public Image circle, check;
            public Text number, label;
        }

        [System.Serializable]
        public class TopBar
        {
            public GameObject root;
            public Step[] steps = new Step[4];
            public Button help, menu;
        }

        [System.Serializable]
        public class ArriveScreen
        {
            public GameObject root;
            public Button capture, newCarPark;
        }

        [System.Serializable]
        public class PhotoScreen
        {
            public GameObject root;
            [Tooltip("The CCTV photo. Its size and position decide where the bay dots appear.")]
            public RawImage photo;
            [Tooltip("Copied once per visible bay (kept hidden). Its colour is replaced by success / failure.")]
            public Image bayDotTemplate;
            [Tooltip("Copied 4 times for the outline of the bay under the mouse (kept hidden).")]
            public Image hoverEdgeTemplate;
            public float hoverEdgeWidth = 4f;
            public Text info, source;
            public Button retake, back;
            public RectTransform tooltip;
            public Text tooltipLine, tooltipDetail;
        }

        [System.Serializable]
        public class DrivingScreen
        {
            public GameObject root;
            public Text title, body;
            [Tooltip("Width follows the drive-in progress (anchor max x = 0..1).")]
            public RectTransform progressFill;
            public Button cancel;
        }

        [System.Serializable]
        public class ParkingScreen
        {
            public GameObject root;
            public Text title, gear, speed, steeringWord, distance, angle, cornersText;
            [Tooltip("Moves along its parent (the steering track): left end = full left, right end = full right.")]
            public RectTransform steeringKnob;
            public Image cornersChip, straightChip, stoppedChip;
            public RectTransform holdFill;
            public Button cancel;
        }

        [System.Serializable]
        public class ResultScreen
        {
            public GameObject root;
            public Graphic stripe, iconCircle;
            public GameObject iconCheck, iconCross;
            public Text title, subtitle;
            public GameObject stats;
            public Text time, offCentre, angle, rating;
            public Text failTip;
            public Button again, anotherBay, newCarPark, mainMenu;
        }

        [System.Serializable]
        public class BusyCard
        {
            public GameObject root;
            public Text title, status, hint;
        }

        [System.Serializable]
        public class ConfirmBayDialog
        {
            public GameObject root;
            public Text title;
            public Button park, cancel;
        }

        [System.Serializable]
        public class ConfirmExitDialog
        {
            public GameObject root;
            public Button exit, cancel;
        }

        [System.Serializable]
        public class PauseDialog
        {
            public GameObject root;
            public Button resume, cancelParking, newCarPark, help, settings, mainMenu, exit;
        }

        [System.Serializable]
        public class HelpDialog
        {
            public GameObject root;
            public Button close;
        }

        [System.Serializable]
        public class SettingsDialog
        {
            public GameObject root;
            public Button handover, less, more, sensorView, batch, done;
            [Tooltip("Round 6: parking model choice (PPO / DQN). Optional.")]
            public Button model;
            public Text fullness;
        }

        [Header("Screens")]
        public MenuScreen menu = new MenuScreen();
        public TopBar topBar = new TopBar();
        public ArriveScreen arrive = new ArriveScreen();
        public PhotoScreen photo = new PhotoScreen();
        public DrivingScreen driving = new DrivingScreen();
        public ParkingScreen parking = new ParkingScreen();
        public ResultScreen result = new ResultScreen();
        public BusyCard busy = new BusyCard();

        [Header("Dialogs")]
        [Tooltip("Darkens the screen behind any dialog.")]
        public GameObject dim;
        public ConfirmBayDialog confirmBay = new ConfirmBayDialog();
        public ConfirmExitDialog confirmExit = new ConfirmExitDialog();
        public PauseDialog pause = new PauseDialog();
        public HelpDialog help = new HelpDialog();
        public SettingsDialog settings = new SettingsDialog();

        [Header("Feedback")]
        public CanvasGroup toast;
        public Text toastText;
        [Tooltip("Full-screen flash at the result (green or red, fades out).")]
        public Image flash;

        // ---------------------------------------------------------------- helpers used by ShowcaseUI.cs

        public static void Show(GameObject go, bool on)
        {
            if (go && go.activeSelf != on) go.SetActive(on);
        }

        public static void Set(Text t, string s)
        {
            if (t && t.text != s) t.text = s;
        }

        public static void Tint(Graphic g, Color c)
        {
            if (g) g.color = c;
        }

        /// <summary>A bar fill: anchor max x = value (0-1).</summary>
        public static void Fill(RectTransform fill, float value)
        {
            if (!fill) return;
            var max = fill.anchorMax;
            max.x = Mathf.Clamp01(value);
            fill.anchorMax = max;
        }

        /// <summary>The button's "Label" child text (the key hint is a separate child).</summary>
        public static Text Label(Button b)
        {
            if (!b) return null;
            var t = b.transform.Find("Label");
            return t ? t.GetComponent<Text>() : b.GetComponentInChildren<Text>();
        }

        /// <summary>Colour a button as primary (accent) or secondary, with lighter hover and darker press.</summary>
        public void Style(Button b, bool primary)
        {
            if (!b) return;
            Color c = primary ? accent : secondaryButton;
            var cb = b.colors;
            cb.normalColor = c;
            cb.selectedColor = c;
            cb.highlightedColor = primary ? Color.Lerp(c, Color.white, 0.18f)                  // lighter on hover
                                          : new Color(c.r, c.g, c.b, Mathf.Min(1f, c.a + 0.10f));  // translucent: a bit more opaque
            cb.pressedColor = new Color(c.r * 0.85f, c.g * 0.85f, c.b * 0.85f, c.a);
            cb.colorMultiplier = 1f;
            b.colors = cb;
        }
    }
}
