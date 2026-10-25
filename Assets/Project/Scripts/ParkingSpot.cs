using UnityEngine;

namespace ParkPilot
{
    /// <summary>Bay categories, used to stage which bays can be targets and to log success per group.</summary>
    [System.Flags]
    public enum BayGroup
    {
        None = 0,
        /// <summary>Rows N2/S2: open aisle on both sides (bay 8, the stage 0-3 target, is one of these).</summary>
        Inner = 1,
        /// <summary>Rows N1/S1 against the north/south wall, except the corner bays.</summary>
        Outer = 2,
        /// <summary>Rows N1/S1, end columns W1/E1: wall behind and wall to the side.</summary>
        Corner = 4,
        /// <summary>The middle-column bays entered sideways from the lanes.</summary>
        Sideways = 8,
        All = Inner | Outer | Corner | Sideways,
    }

    /// <summary>
    /// One parking bay. Place the GameObject at the bay centre (on the ground) with its
    /// blue Z axis (transform.forward) pointing from the aisle INTO the bay, i.e. the way a
    /// car faces when it has parked nose-in.
    ///
    /// "Parked" is checked geometrically (car footprint corners inside the bay rectangle),
    /// not with a trigger collider: triggers get hit by the ray sensors and are unreliable
    /// at high time scales.
    /// </summary>
    public class ParkingSpot : MonoBehaviour
    {
        [Tooltip("Bay width (local X) and length (local Z) in metres.")]
        public Vector2 size = new Vector2(2.8f, 5.5f);

        // Generated car parks only (ProceduralLot sets them per layout), for evaluation analysis; untouched elsewhere.
        /// <summary>Width of the aisle in front of the bay (m, between open ends, or to the wall); -1 = not generated.</summary>
        [System.NonSerialized] public float layoutAisleWidth = -1f;
        /// <summary>A pillar stands on one of this bay's side lines.</summary>
        [System.NonSerialized] public bool layoutPillarBeside;
        /// <summary>A wall runs behind the bay (back-to-back rows).</summary>
        [System.NonSerialized] public bool layoutWallBehind;
        /// <summary>First or last bay of its row segment.</summary>
        [System.NonSerialized] public bool layoutRowEnd;

        [Tooltip("Parked-car object for this bay. Enabled when the bay is occupied. " +
                 "If empty, ParkingEnvironment instantiates its obstacle prefab here on demand.")]
        public GameObject parkedCar;

        [Tooltip("Optional flat renderer (e.g. a thin quad) coloured green/red/yellow for vacant/occupied/target.")]
        public Renderer indicator;

        [Tooltip("If no indicator is assigned, create a coloured wireframe outline at runtime so the bay state shows in the Game view.")]
        public bool autoCreateIndicator = true;

        [Tooltip("Height (m) of the auto-created outline above the bay. Must be above the visual lot surface " +
                 "(AssestGround sits at 0.1 m).")]
        public float indicatorHeight = 0.15f;

        [Tooltip("Line thickness (m) of the auto-created outline.")]
        public float indicatorLineWidth = 0.12f;

        public bool IsOccupied { get; private set; }
        public bool IsTarget { get; private set; }

        public Vector3 Center => transform.position;
        public Vector3 Forward => transform.forward;

        /// <summary>
        /// Bay category, derived from the names created by ParkPilot → Build Bays For Asset Lot
        /// (Bay_N1_W1, Bay_S2_E3, Bay_MidN_2, ...). Unrecognised names count as Inner.
        /// </summary>
        public BayGroup Group
        {
            get
            {
                if (group == BayGroup.None) group = GroupFromName(name);
                return group;
            }
        }
        BayGroup group;

        static BayGroup GroupFromName(string n)
        {
            if (n.Contains("_Mid")) return BayGroup.Sideways;
            if (n.Contains("_N1_") || n.Contains("_S1_"))
                return n.EndsWith("_W1") || n.EndsWith("_E1") ? BayGroup.Corner : BayGroup.Outer;
            return BayGroup.Inner;
        }

        static readonly Color VacantColor = new Color(0.2f, 0.85f, 0.3f);
        static readonly Color OccupiedColor = new Color(0.9f, 0.2f, 0.2f);
        static readonly Color TargetColor = new Color(1f, 0.8f, 0.1f);
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
        static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in fallback
        MaterialPropertyBlock block;

        /// <summary>
        /// Creates a wireframe outline of the bay (LineRenderer) if no indicator is assigned.
        /// Line renderers have no collider, so ray sensors and physics never see it.
        /// </summary>
        public void EnsureIndicator(Material material)
        {
            if (indicator || !autoCreateIndicator) return;

            var go = new GameObject("Indicator");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, indicatorHeight, 0f);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = indicatorLineWidth;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            SetOutline(line);

            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader) material = new Material(shader);
            }
            if (material) line.sharedMaterial = material;
            indicator = line;
        }

        void SetOutline(LineRenderer line)
        {
            // Inset by half the line width so the outline sits inside the bay
            float hx = size.x * 0.5f - indicatorLineWidth * 0.5f;
            float hz = size.y * 0.5f - indicatorLineWidth * 0.5f;
            line.positionCount = 4;
            line.SetPositions(new[]
            {
                new Vector3(-hx, 0f, -hz),
                new Vector3(-hx, 0f,  hz),
                new Vector3( hx, 0f,  hz),
                new Vector3( hx, 0f, -hz),
            });
        }

        /// <summary>Change the bay's size and local position at runtime (geometry randomisation); redraws the outline.</summary>
        public void Resize(Vector2 newSize, Vector3 localPosition)
        {
            size = newSize;
            transform.localPosition = localPosition;
            if (indicator is LineRenderer line && line.name == "Indicator") SetOutline(line);
        }

        /// <summary>Show or hide the coloured outline (the showcase hides it so the CCTV photo does not give occupancy away).</summary>
        public void SetIndicatorVisible(bool visible)
        {
            if (indicator) indicator.enabled = visible;
        }

        public void SetState(bool occupied, bool isTarget)
        {
            IsOccupied = occupied;
            IsTarget = isTarget;
            if (parkedCar) parkedCar.SetActive(occupied);
            UpdateIndicator();
        }

        /// <summary>True if a world-space point lies inside the bay rectangle (plus margin).</summary>
        public bool ContainsPoint(Vector3 worldPoint, float margin = 0f)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.x) <= size.x * 0.5f + margin
                && Mathf.Abs(local.z) <= size.y * 0.5f + margin;
        }

        /// <summary>
        /// Distance (m) from a world-space point to the bay rectangle (plus margin) on the ground plane.
        /// 0 when the point is inside.
        /// </summary>
        public float OutsideDistance(Vector3 worldPoint, float margin = 0f)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - (size.x * 0.5f + margin));
            float dz = Mathf.Max(0f, Mathf.Abs(local.z) - (size.y * 0.5f + margin));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Unsigned heading error in degrees between a direction and the bay axis.
        /// With allowBothDirections, reversing into the bay (facing out) counts as aligned too.
        /// </summary>
        public float HeadingError(Vector3 direction, bool allowBothDirections)
        {
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
            float angle = Vector3.Angle(flat, Vector3.ProjectOnPlane(Forward, Vector3.up));
            return allowBothDirections ? Mathf.Min(angle, 180f - angle) : angle;
        }

        Color? indicatorOverride;

        /// <summary>Showcase: draw the outline in this colour (e.g. green = parked, red = failed); null = normal colours.</summary>
        public void SetIndicatorOverride(Color? colour)
        {
            indicatorOverride = colour;
            UpdateIndicator();
        }

        void UpdateIndicator()
        {
            if (!indicator) return;
            block ??= new MaterialPropertyBlock();
            Color c = indicatorOverride ?? (IsTarget ? TargetColor : IsOccupied ? OccupiedColor : VacantColor);
            indicator.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            indicator.SetPropertyBlock(block);
        }

        void OnValidate()
        {
            // Live-tweak the height while playing
            if (indicator && indicator.name == "Indicator")
            {
                indicator.transform.localPosition = new Vector3(0f, indicatorHeight, 0f);
                if (indicator is LineRenderer line) line.widthMultiplier = indicatorLineWidth;
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = IsTarget ? TargetColor : IsOccupied ? OccupiedColor : VacantColor;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.up * 0.05f, new Vector3(size.x, 0.1f, size.y));
            // Arrow showing the "parked" facing direction
            Gizmos.DrawLine(Vector3.up * 0.1f, new Vector3(0f, 0.1f, size.y * 0.4f));
            Gizmos.DrawLine(new Vector3(0f, 0.1f, size.y * 0.4f), new Vector3(-0.4f, 0.1f, size.y * 0.2f));
            Gizmos.DrawLine(new Vector3(0f, 0.1f, size.y * 0.4f), new Vector3(0.4f, 0.1f, size.y * 0.2f));
        }
    }
}
