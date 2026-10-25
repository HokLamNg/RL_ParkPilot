using System.Collections.Generic;
using UnityEngine;

namespace ParkPilot
{
    /// <summary>
    /// Procedural car parks for training (ParkingEnvironment.procedural_layout): every layout is a new random car park
    /// inside the training area's boundary walls, so the agent cannot rely on one car park's proportions.
    ///
    /// Varies (simulation metres; the app scales real car parks so bays are about 3.2 m wide):
    /// * bay width 2.9-3.6 m, bay length 4.6-5.4 m, rows running along X;
    /// * aisle width (open end to open end) 6.0-11.0 m, i.e. about 4.7-8.6 m real;
    /// * 2-5 rows: single rows against the boundary wall and back-to-back pairs, optionally with a wall between them
    ///   (underground / building edges), and an optional cross aisle splitting the rows;
    /// * pillars on the bay lines every 2-3 bays, set back from the open end (underground / multi-storey).
    /// Bays, parked cars, pillars and walls come from pools, so nothing is created or destroyed after warm-up.
    /// </summary>
    public class ProceduralLot
    {
        public struct Settings
        {
            public Vector2 bayWidth, bayLength, aisleWidth;
            public float pillarChance, wallBetweenChance, crossAisleChance;
            public Vector2 pillarSize, pillarSetback;
        }

        public static Settings Default => new Settings
        {
            bayWidth = new Vector2(2.9f, 3.6f),
            bayLength = new Vector2(4.6f, 5.4f),
            aisleWidth = new Vector2(6.0f, 11.0f),
            pillarChance = 0.5f,
            wallBetweenChance = 0.4f,
            crossAisleChance = 0.5f,
            pillarSize = new Vector2(0.4f, 0.7f),
            pillarSetback = new Vector2(0.2f, 1.2f),
        };

        /// <summary>
        /// Round 4 curriculum (environment parameter procedural_difficulty): 0 = wide aisles (8-11 m), no pillars, no
        /// walls between rows; 1 = aisles 7-11 m with walls between rows; 2 (default) = the full generator.
        /// </summary>
        public static Settings ForDifficulty(int level)
        {
            var s = Default;
            if (level <= 0)
            {
                s.aisleWidth = new Vector2(8f, 11f);
                s.pillarChance = 0f;
                s.wallBetweenChance = 0f;
            }
            else if (level == 1)
            {
                s.aisleWidth = new Vector2(7f, 11f);
                s.pillarChance = 0f;
            }
            return s;
        }

        readonly Transform root;
        readonly Transform bayParent, obstacleParent;
        readonly List<ParkingSpot> bayPool = new List<ParkingSpot>();
        readonly List<GameObject> pillarPool = new List<GameObject>();
        readonly List<GameObject> wallPool = new List<GameObject>();
        readonly Material obstacleMaterial;
        int baysUsed, pillarsUsed, wallsUsed;
        const float Edge = 19.75f;      // inner face of the training area's boundary walls
        const float Height = 1.5f;      // same as the boundary walls, so the ray sensors see them the same way

        public string Description { get; private set; } = "";

        public ProceduralLot(Transform area, Material obstacleMaterial)
        {
            root = new GameObject("ProceduralLot").transform;
            root.SetParent(area, false);
            bayParent = new GameObject("Bays").transform;
            bayParent.SetParent(root, false);
            obstacleParent = new GameObject("Pillars and walls").transform;
            obstacleParent.SetParent(root, false);
            this.obstacleMaterial = obstacleMaterial;
        }

        public void SetActive(bool on) => root.gameObject.SetActive(on);

        /// <summary>Lay out a new random car park; returns its bays (positions and sizes set, states untouched).</summary>
        public List<ParkingSpot> Generate(System.Random rng, Settings s)
        {
            float R(Vector2 r) => r.x + (float)rng.NextDouble() * (r.y - r.x);
            baysUsed = pillarsUsed = wallsUsed = 0;

            float w = R(s.bayWidth), l = R(s.bayLength);
            bool pillars = rng.NextDouble() < s.pillarChance;
            int pillarEvery = rng.Next(2, 4);
            float pillarSize = R(s.pillarSize), setback = R(s.pillarSetback);

            // Columns: bays along X, optionally split by a cross aisle.
            float xEdge = R(new Vector2(0.3f, 2.0f));
            var segments = new List<(float from, float to)>();
            if (rng.NextDouble() < s.crossAisleChance)
            {
                float gap = R(new Vector2(6f, 9f)), centre = R(new Vector2(-5f, 5f));
                segments.Add((-Edge + xEdge, centre - gap / 2f));
                segments.Add((centre + gap / 2f, Edge - xEdge));
            }
            else segments.Add((-Edge + xEdge, Edge - xEdge));

            // Rows along Z, from the south wall up: [row facing an aisle] aisle [row] (back-to-back) [row] aisle ...
            var rows = new List<(float back, bool openUp, float aisle, bool wallBehind)>();   // openUp: open end on the +Z side
            bool wallBelow = false;                                  // a wall behind the next row facing +Z
            float z = -Edge + R(new Vector2(0.2f, 1.2f));
            int aisles = 0;
            while (true)
            {
                // Row with its open end facing +Z, then an aisle, then a row facing -Z.
                float aisle = R(s.aisleWidth);
                float room = Edge - 0.2f - (z + l);
                if (room < s.aisleWidth.x) break;                       // no room for this row and an aisle
                bool last = room < aisle + l;                           // no opposite row: aisle against the wall
                rows.Add((z, true, last ? room : aisle, wallBelow));
                wallBelow = false;
                aisles++;
                if (last) break;
                z += l + aisle;
                rows.Add((z + l, false, aisle, false));
                z += l;
                if (rng.NextDouble() < s.wallBetweenChance && z + 0.4f < Edge)
                {
                    Wall(new Vector3(0f, Height / 2f, z + 0.1f), new Vector3(2f * Edge, Height, 0.2f));
                    z += 0.2f;
                    rows[rows.Count - 1] = (rows[rows.Count - 1].back, false, aisle, true);
                    wallBelow = true;
                }
            }

            var bays = new List<ParkingSpot>();
            foreach (var (back, openUp, rowAisle, rowWall) in rows)
            {
                float openZ = openUp ? back + l : back - l;
                float centreZ = (back + openZ) / 2f;
                float yaw = openUp ? 180f : 0f;                         // forward = from the open end into the bay
                foreach (var (from, to) in segments)
                {
                    int n = Mathf.FloorToInt((to - from) / w);
                    float start = from + ((to - from) - n * w) / 2f;
                    for (int i = 0; i < n; i++)
                    {
                        var spot = Bay();
                        spot.transform.localPosition = new Vector3(start + (i + 0.5f) * w, 0f, centreZ);
                        spot.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                        spot.Resize(new Vector2(w, l), spot.transform.localPosition);
                        spot.layoutAisleWidth = rowAisle;
                        spot.layoutWallBehind = rowWall;
                        spot.layoutRowEnd = i == 0 || i == n - 1;
                        // Pillars stand on bay lines pillarEvery, 2 x pillarEvery, ... (line k between bays k-1 and k).
                        bool PillarLine(int k) => pillars && k >= pillarEvery && k < n && k % pillarEvery == 0;
                        spot.layoutPillarBeside = PillarLine(i) || PillarLine(i + 1);
                        bays.Add(spot);
                    }
                    if (pillars)
                    {
                        // On the bay lines, set back from the open end towards the back of the bay.
                        float pz = openUp ? openZ - setback : openZ + setback;
                        for (int i = pillarEvery; i < n; i += pillarEvery)
                            Pillar(new Vector3(start + i * w, Height / 2f, pz), pillarSize);
                    }
                }
            }

            // Hide unused pool objects.
            for (int i = baysUsed; i < bayPool.Count; i++) bayPool[i].gameObject.SetActive(false);
            for (int i = pillarsUsed; i < pillarPool.Count; i++) pillarPool[i].SetActive(false);
            for (int i = wallsUsed; i < wallPool.Count; i++) wallPool[i].SetActive(false);

            Description = $"{rows.Count} rows, {aisles} aisles, bay {w:F2} x {l:F2} m, {bays.Count} bays" +
                          (pillars ? $", pillars every {pillarEvery}" : "") + (wallsUsed > 0 ? $", {wallsUsed} wall(s)" : "") +
                          (segments.Count > 1 ? ", cross aisle" : "");
            return bays;
        }

        ParkingSpot Bay()
        {
            if (baysUsed == bayPool.Count)
            {
                var go = new GameObject($"Bay_P{bayPool.Count:00}");
                go.transform.SetParent(bayParent, false);
                bayPool.Add(go.AddComponent<ParkingSpot>());
            }
            var spot = bayPool[baysUsed++];
            spot.gameObject.SetActive(true);
            return spot;
        }

        void Pillar(Vector3 localPos, float size)
        {
            var go = Obstacle(pillarPool, ref pillarsUsed, "Pillar");
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size, Height, size);
        }

        void Wall(Vector3 localPos, Vector3 scale)
        {
            var go = Obstacle(wallPool, ref wallsUsed, "Row wall");
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
        }

        GameObject Obstacle(List<GameObject> pool, ref int used, string name)
        {
            if (used == pool.Count)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"{name} {pool.Count}";
                go.tag = "Wall";                                        // seen by the ray sensors like the boundary walls
                go.transform.SetParent(obstacleParent, false);
                if (obstacleMaterial) go.GetComponent<Renderer>().sharedMaterial = obstacleMaterial;
                pool.Add(go);
            }
            var o = pool[used++];
            o.SetActive(true);
            return o;
        }
    }
}
