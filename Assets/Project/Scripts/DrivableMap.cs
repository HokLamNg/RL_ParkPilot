using System.Collections.Generic;
using UnityEngine;

namespace ParkPilot
{
    /// <summary>
    /// Round 6: the distance a car would actually have to drive to the target bay, around parked
    /// cars, pillars and walls, on a grid over one training area. Used for
    /// * the reward's distance term (environment parameter route_distance = 1): a straight-line distance rewards driving
    ///   into a row of parked cars when the bay is behind it, and cutting the corner at row ends;
    /// * far starts (far_start_share): start poses are drawn from free aisle cells by their route distance.
    /// The policy never sees this map: it only shapes the reward and picks start poses.
    ///
    /// Built at each reset (the parked cars do not move during an episode). Obstacles are the colliders tagged Wall or
    /// ObstacleCar, enlarged by the car's half-width plus a clearance, so a free cell is a place where the car's CENTRE
    /// can be with room to spare. The target bay and the 1.5 m in front of its open end are never closed by that
    /// enlargement (only by an obstacle's own footprint), so a bay between two parked cars or beside a pillar stays
    /// reachable. Empty bays other than the target are passable but cost more (emptyBayCost), so routes prefer aisles.
    /// No random numbers are drawn here.
    /// </summary>
    public class DrivableMap
    {
        public float CellSize { get; }
        public bool Ready { get; private set; }

        readonly Transform area;
        readonly int nx, nz;
        readonly float x0, z0;                   // area-local coordinates of the grid's lower-left corner
        readonly bool[] blocked;
        readonly float[] cost;                   // per cell: 1 = aisle, emptyBayCost = inside an empty bay
        readonly bool[] inBay;                   // inside any bay rectangle (target included)
        readonly float[] dist;                   // route distance to the target centre, +inf = unreachable
        readonly List<Collider> colliders = new List<Collider>();
        float[] heapKey = new float[1024];
        int[] heapCell = new int[1024];
        int heapCount;

        public DrivableMap(Transform area, Vector2 halfExtents, float cellSize)
        {
            this.area = area;
            CellSize = cellSize;
            nx = Mathf.CeilToInt(2f * halfExtents.x / cellSize);
            nz = Mathf.CeilToInt(2f * halfExtents.y / cellSize);
            x0 = -halfExtents.x;
            z0 = -halfExtents.y;
            int n = nx * nz;
            blocked = new bool[n];
            cost = new float[n];
            inBay = new bool[n];
            dist = new float[n];
        }

        /// <summary>Short summary for the Inspector: share of blocked cells, of reachable free cells, and the farthest route.</summary>
        public string Summary()
        {
            if (!Ready) return "not built";
            int n = nx * nz, blockedCount = 0, reachable = 0;
            float far = 0f;
            for (int c = 0; c < n; c++)
            {
                if (blocked[c]) { blockedCount++; continue; }
                if (!float.IsInfinity(dist[c])) { reachable++; far = Mathf.Max(far, dist[c]); }
            }
            return $"{nx} x {nz} cells of {CellSize:0.##} m: blocked {100f * blockedCount / n:0}%, reachable {100f * reachable / n:0}%, " +
                   $"unreachable free {100f * (n - blockedCount - reachable) / n:0}%, farthest route {far:0.0} m";
        }

        /// <summary>Mark the map unused for this episode (Distance then falls back to the straight line).</summary>
        public void Invalidate() => Ready = false;

        Vector2 CellCentre(int i, int k) => new Vector2(x0 + (i + 0.5f) * CellSize, z0 + (k + 0.5f) * CellSize);

        Vector2 Local(Vector3 world)
        {
            Vector3 l = area.InverseTransformPoint(world);
            return new Vector2(l.x, l.z);
        }

        /// <summary>Rebuild for this episode's parked cars, obstacles and target bay.</summary>
        public void Build(List<ParkingSpot> spots, ParkingSpot target, float carHalfWidth, float clearance, float emptyBayCost)
        {
            int n = nx * nz;
            for (int c = 0; c < n; c++) { blocked[c] = false; cost[c] = 1f; inBay[c] = false; dist[c] = float.PositiveInfinity; }
            Ready = false;
            if (!target) return;

            // Bays: the target is plain floor, other empty bays cost more. Occupied bays are covered by their car.
            foreach (var s in spots)
            {
                if (!s || !s.gameObject.activeInHierarchy) continue;
                MarkRect(s, 0f, 0f, c => { inBay[c] = true; if (s != target) cost[c] = Mathf.Max(cost[c], emptyBayCost); });
            }

            // Obstacles, enlarged; inside the target's protected zone only their own footprint blocks.
            float r = carHalfWidth + clearance;
            area.GetComponentsInChildren(false, colliders);
            foreach (var col in colliders)
            {
                if (!col.enabled || col.isTrigger || !ParkingEnvironment.IsObstacle(col)) continue;
                if (!Footprint(col, out Vector2 c, out Vector2 u, out Vector2 v, out Vector2 half)) continue;
                float reach = Mathf.Sqrt(half.x * half.x + half.y * half.y) + r;
                ForCellsAround(c, reach, (cell, p) =>
                {
                    Vector2 d = p - c;
                    float qx = Mathf.Max(Mathf.Abs(Vector2.Dot(d, u)) - half.x, 0f);
                    float qz = Mathf.Max(Mathf.Abs(Vector2.Dot(d, v)) - half.y, 0f);
                    float gap = Mathf.Sqrt(qx * qx + qz * qz);
                    float limit = InTargetZone(target, p) ? 0f : r;
                    if (gap <= limit) blocked[cell] = true;
                });
            }

            if (!CellOf(Local(target.Center), out int ti, out int tk)) return;
            int start = tk * nx + ti;
            blocked[start] = false;
            dist[start] = 0f;
            heapCount = 0;
            Push(0f, start);
            float diag = CellSize * 1.41421356f;
            while (heapCount > 0)
            {
                Pop(out float d0, out int cur);
                if (d0 > dist[cur]) continue;
                int ci = cur % nx, ck = cur / nx;
                for (int dk = -1; dk <= 1; dk++)
                for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dk == 0) continue;
                    int ni = ci + di, nk = ck + dk;
                    if (ni < 0 || nk < 0 || ni >= nx || nk >= nz) continue;
                    int nb = nk * nx + ni;
                    if (blocked[nb]) continue;
                    if (di != 0 && dk != 0 && (blocked[ck * nx + ni] || blocked[nk * nx + ci])) continue;
                    float step = (di != 0 && dk != 0 ? diag : CellSize) * 0.5f * (cost[cur] + cost[nb]);
                    float nd = d0 + step;
                    if (nd < dist[nb]) { dist[nb] = nd; Push(nd, nb); }
                }
            }
            Ready = true;
        }

        /// <summary>
        /// Route distance (m) from a world position to the target centre. Bilinear between free cells; next to an
        /// obstacle (inside the enlarged zone) it is the distance to the nearest reachable cell plus the straight step
        /// to it, so the value changes smoothly. Falls back to the straight-line distance if nothing is reachable nearby.
        /// </summary>
        public float Distance(Vector3 world, Vector3 targetWorld)
        {
            Vector2 p = Local(world);
            float straight = (p - Local(targetWorld)).magnitude;
            if (!Ready) return straight;
            float fx = (p.x - x0) / CellSize - 0.5f, fz = (p.y - z0) / CellSize - 0.5f;
            int i0 = Mathf.FloorToInt(fx), k0 = Mathf.FloorToInt(fz);
            float tx = fx - i0, tz = fz - k0;
            if (Finite(i0, k0) && Finite(i0 + 1, k0) && Finite(i0, k0 + 1) && Finite(i0 + 1, k0 + 1))
            {
                float a = Mathf.Lerp(dist[k0 * nx + i0], dist[k0 * nx + i0 + 1], tx);
                float b = Mathf.Lerp(dist[(k0 + 1) * nx + i0], dist[(k0 + 1) * nx + i0 + 1], tx);
                return Mathf.Max(straight, Mathf.Lerp(a, b, tz));
            }
            float best = float.PositiveInfinity;
            int ring = Mathf.CeilToInt(3f / CellSize);
            for (int k = k0 - ring; k <= k0 + ring + 1; k++)
            for (int i = i0 - ring; i <= i0 + ring + 1; i++)
            {
                if (!Finite(i, k)) continue;
                float d = dist[k * nx + i] + (CellCentre(i, k) - p).magnitude;
                if (d < best) best = d;
            }
            return float.IsInfinity(best) ? straight : Mathf.Max(straight, best);
        }

        /// <summary>A random free aisle cell (not inside any bay) whose route distance lies in [minRoute, maxRoute].
        /// Returns its centre in area-local coordinates. Uses the caller's random generator.</summary>
        public bool SampleAisleCell(System.Random rng, float minRoute, float maxRoute, out Vector2 local, out float route)
        {
            local = Vector2.zero;
            route = 0f;
            if (!Ready) return false;
            int n = nx * nz;
            for (int attempt = 0; attempt < 400; attempt++)
            {
                int c = rng.Next(n);
                if (blocked[c] || inBay[c] || float.IsInfinity(dist[c]) || dist[c] < minRoute || dist[c] > maxRoute) continue;
                local = CellCentre(c % nx, c / nx);
                route = dist[c];
                return true;
            }
            return false;
        }

        /// <summary>Scene view check (ParkingEnvironment.OnDrawGizmosSelected): blocked cells red, reachable cells coloured
        /// by route distance (green near the bay, blue far), unreachable free cells grey.</summary>
        public void DrawGizmos(float maxShown = 40f)
        {
            if (!Ready) return;
            var size = new Vector3(CellSize * 0.9f, 0.05f, CellSize * 0.9f);   // drawn 0.3 m up, above the painted ground
            for (int k = 0; k < nz; k++)
            for (int i = 0; i < nx; i++)
            {
                int c = k * nx + i;
                Vector2 p = CellCentre(i, k);
                Gizmos.color = blocked[c] ? new Color(1f, 0.2f, 0.2f, 0.6f)
                    : float.IsInfinity(dist[c]) ? new Color(0.5f, 0.5f, 0.5f, 0.3f)
                    : Color.Lerp(new Color(0.2f, 1f, 0.3f, 0.45f), new Color(0.2f, 0.3f, 1f, 0.45f), dist[c] / maxShown);
                Gizmos.DrawCube(area.TransformPoint(new Vector3(p.x, 0.3f, p.y)), size);
            }
        }

        bool Finite(int i, int k) => i >= 0 && k >= 0 && i < nx && k < nz && !float.IsInfinity(dist[k * nx + i]);

        bool CellOf(Vector2 p, out int i, out int k)
        {
            i = Mathf.FloorToInt((p.x - x0) / CellSize);
            k = Mathf.FloorToInt((p.y - z0) / CellSize);
            return i >= 0 && k >= 0 && i < nx && k < nz;
        }

        /// <summary>The target bay, plus 1.5 m in front of its open end (bay local -z side).</summary>
        bool InTargetZone(ParkingSpot target, Vector2 p)
        {
            Vector3 l = target.transform.InverseTransformPoint(area.TransformPoint(new Vector3(p.x, 0f, p.y)));
            return Mathf.Abs(l.x) <= target.size.x * 0.5f && l.z <= target.size.y * 0.5f && l.z >= -target.size.y * 0.5f - 1.5f;
        }

        void MarkRect(ParkingSpot s, float growX, float growZ, System.Action<int> mark)
        {
            Vector2 c = Local(s.Center);
            float reach = 0.5f * Mathf.Sqrt(s.size.x * s.size.x + s.size.y * s.size.y) + Mathf.Max(growX, growZ);
            ForCellsAround(c, reach, (cell, p) =>
            {
                Vector3 l = s.transform.InverseTransformPoint(area.TransformPoint(new Vector3(p.x, 0f, p.y)));
                if (Mathf.Abs(l.x) <= s.size.x * 0.5f + growX && Mathf.Abs(l.z) <= s.size.y * 0.5f + growZ) mark(cell);
            });
        }

        void ForCellsAround(Vector2 c, float reach, System.Action<int, Vector2> visit)
        {
            int iMin = Mathf.Max(0, Mathf.FloorToInt((c.x - reach - x0) / CellSize));
            int iMax = Mathf.Min(nx - 1, Mathf.FloorToInt((c.x + reach - x0) / CellSize));
            int kMin = Mathf.Max(0, Mathf.FloorToInt((c.y - reach - z0) / CellSize));
            int kMax = Mathf.Min(nz - 1, Mathf.FloorToInt((c.y + reach - z0) / CellSize));
            for (int k = kMin; k <= kMax; k++)
            for (int i = iMin; i <= iMax; i++)
                visit(k * nx + i, CellCentre(i, k));
        }

        /// <summary>A collider's footprint on the ground as an oriented rectangle in area-local coordinates.</summary>
        bool Footprint(Collider col, out Vector2 centre, out Vector2 u, out Vector2 v, out Vector2 half)
        {
            if (col is BoxCollider box)
            {
                var t = box.transform;
                Vector3 c = area.InverseTransformPoint(t.TransformPoint(box.center));
                Vector3 ax = area.InverseTransformDirection(t.TransformDirection(Vector3.right));
                Vector3 az = area.InverseTransformDirection(t.TransformDirection(Vector3.forward));
                Vector3 s = Vector3.Scale(box.size, t.lossyScale);
                centre = new Vector2(c.x, c.z);
                u = new Vector2(ax.x, ax.z).normalized;
                v = new Vector2(az.x, az.z).normalized;
                half = new Vector2(Mathf.Abs(s.x) * 0.5f, Mathf.Abs(s.z) * 0.5f);
                return u.sqrMagnitude > 0.5f && v.sqrMagnitude > 0.5f;
            }
            // Other collider shapes: their bounding box (axis-aligned in world space, close enough here).
            Bounds b = col.bounds;
            Vector3 lc = area.InverseTransformPoint(b.center);
            centre = new Vector2(lc.x, lc.z);
            u = Vector2.right;
            v = Vector2.up;
            half = new Vector2(b.extents.x, b.extents.z);
            return true;
        }

        void Push(float key, int cell)
        {
            if (heapCount == heapKey.Length)
            {
                System.Array.Resize(ref heapKey, heapKey.Length * 2);
                System.Array.Resize(ref heapCell, heapCell.Length * 2);
            }
            int i = heapCount++;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heapKey[parent] <= key) break;
                heapKey[i] = heapKey[parent];
                heapCell[i] = heapCell[parent];
                i = parent;
            }
            heapKey[i] = key;
            heapCell[i] = cell;
        }

        void Pop(out float key, out int cell)
        {
            key = heapKey[0];
            cell = heapCell[0];
            float lastKey = heapKey[--heapCount];
            int lastCell = heapCell[heapCount];
            int i = 0;
            while (true)
            {
                int child = 2 * i + 1;
                if (child >= heapCount) break;
                if (child + 1 < heapCount && heapKey[child + 1] < heapKey[child]) child++;
                if (heapKey[child] >= lastKey) break;
                heapKey[i] = heapKey[child];
                heapCell[i] = heapCell[child];
                i = child;
            }
            heapKey[i] = lastKey;
            heapCell[i] = lastCell;
        }
    }
}
