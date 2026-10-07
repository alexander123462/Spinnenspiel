using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace Spinnenspiel
{
    /// <summary>
    /// Erzeugt bei Awake aus einem Seed eine vollständige Urwald-Welt:
    /// Terrain (Gelände + Sumpfwasser), Bäume und Positionierung der Spinne.
    /// F5 erzeugt eine neue Zufalls-Welt (Szene neu laden).
    /// </summary>
    public class JungleWorldGenerator : MonoBehaviour
    {
        public JungleAssetLibrary library;
        public int fixedSeed;
        public static int NextSeed;

        public int Seed { get; private set; }
        public JungleWorld World { get; private set; }

        /// <summary>Waagrechter Spawnplatz der Spinne (y wird erst gegen das Terrain gemessen).</summary>
        private Vector3 _spawnXZ;
        public bool IsReady { get; private set; }
        public float GenerationSeconds { get; private set; }
        public int TreeCount => Trees.Count;
        public List<Transform> Trees { get; } = new List<Transform>();
        public int BushCount { get; private set; }
        public int FernCount { get; private set; }
        public int ReedCount { get; private set; }
        public int GiantLeafCount { get; private set; }
        public int TreeFernCount { get; private set; }
        public int GroundCoverCount { get; private set; }
        public int PlantColliderCount { get; private set; }
        public int LianaCount { get; private set; }
        public int LogCount { get; private set; }
        public int RockCount { get; private set; }

        private void Awake()
        {
            var sw = Stopwatch.StartNew();

            // 1) Seed festlegen
            Seed = NextSeed != 0
                ? NextSeed
                : (fixedSeed != 0 ? fixedSeed : 1 + (System.Environment.TickCount & 0x7fffffff) % 999999);
            NextSeed = 0;
            World = new JungleWorld(Seed);
            // Spawnplatz früh merken: Bäume, Unterholz, Totholz und Steine halten darum Platz frei.
            _spawnXZ = World.FindSpawnPoint();
            float size = World.Size;

            // 2) Terrain
            var data = new TerrainData { heightmapResolution = 513, alphamapResolution = 256 };
            data.size = new Vector3(size, JungleWorld.MaxHeight, size);

            float[,] heights = new float[513, 513];
            for (int z = 0; z < 513; z++)
                for (int x = 0; x < 513; x++)
                    heights[z, x] = World.Height(x * (size / 512f), z * (size / 512f)) / JungleWorld.MaxHeight;
            data.SetHeights(0, 0, heights);

            data.terrainLayers = library.terrainLayers; // Reihenfolge: Earth(0), Grass(1), Mud(2), Rock(3)

            // AlphaMap: float[256,256,4], Kanäle in Layer-Reihenfolge (Earth, Grass, Mud, Rock).
            float[,,] alpha = new float[256, 256, 4];
            for (int pz = 0; pz < 256; pz++)
            {
                for (int px = 0; px < 256; px++)
                {
                    float wx = px * (size / 255f);
                    float wz = pz * (size / 255f);
                    float steep = data.GetSteepness(px / 255f, pz / 255f);
                    float wet = World.Wetness(wx, wz);
                    int channel;
                    if (wet > 0.6f || World.IsUnderWater(wx, wz)) channel = 2;    // Mud
                    else if (wet >= 0.3f) channel = 0;                            // Earth
                    else if (steep > 30f) channel = 3;                           // Rock
                    else channel = 1;                                            // Grass
                    alpha[px, pz, channel] = 1f;
                }
            }
            data.SetAlphamaps(0, 0, alpha);

            GameObject t = Terrain.CreateTerrainGameObject(data);
            t.name = "Urwaldboden";
            t.GetComponent<Terrain>().drawInstanced = true;
            t.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Ground;

            // 3) Sumpfwasser (als Wasser erkennbar: dünner BoxCollider + SurfaceTag Water + WaterBody)
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(water.GetComponent<Collider>());
            var wbox = water.AddComponent<BoxCollider>();
            wbox.size = new Vector3(1f, 1f, 0.02f); // im lokalen Quad-Raum dünn (Z)
            water.name = "Sumpfwasser";
            water.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            water.transform.position = new Vector3(size / 2f, JungleWorld.WaterLevel, size / 2f);
            water.transform.localScale = new Vector3(size, size, 1f);
            water.GetComponent<MeshRenderer>().sharedMaterial = library.water;
            water.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Water;
            water.AddComponent<WaterBody>();

            // 4) Bäume
            Transform treesRoot = new GameObject("Bäume").transform;
            var placed = new Dictionary<Vector2Int, List<Vector3>>(); // 3-m-Zellen, Mindestdistanz 2,2 m
            for (int gx = 2; gx < size - 1; gx += 2)
            {
                for (int gz = 2; gz < size - 1; gz += 2)
                {
                    float ox = (World.Hash01(gx, gz, 1) - 0.5f) * 1.6f;
                    float oz = (World.Hash01(gx, gz, 2) - 0.5f) * 1.6f;
                    float x = gx + ox;
                    float z = gz + oz;
                    if (x < 1f || x > size - 1f || z < 1f || z > size - 1f) continue;
                    if (NearSpawn(x, z, 2.0f)) continue; // 2 m um den Spawnplatz frei

                    float h = World.Height(x, z);
                    if (h < JungleWorld.WaterLevel + 0.3f) continue;

                    float e = 0.5f;
                    float dh = Mathf.Max(Mathf.Abs(World.Height(x + e, z) - h),
                                        Mathf.Abs(World.Height(x, z + e) - h));
                    if (Mathf.Atan2(dh, e) * Mathf.Rad2Deg >= 25f) continue;

                    if (World.Hash01(gx, gz, 3) >= World.Density(x, z) * 0.55f) continue;

                    Vector2Int cell = new Vector2Int(Mathf.FloorToInt(x / 3f), Mathf.FloorToInt(z / 3f));
                    Vector3 flat = new Vector3(x, 0f, z);
                    if (HasNearbyTree(placed, cell, flat)) continue;

                    int treeVariants = library.phTreeLod0 != null ? library.phTreeLod0.Length : 0;
                    if (treeVariants == 0) continue; // ohne PH-Trees im Library: skip (geloggt frueh unten)
                    int variant = (int)(World.Hash01(gx, gz, 4) * treeVariants) % treeVariants;
                    float rot = World.Hash01(gx, gz, 5) * 360f;
                    // Zufalls-Skalierung mal Library-Scale (Jacaranda ca. 30 m, Inselbäume 12-15 m).
                    float scale = (0.8f + World.Hash01(gx, gz, 6) * 0.5f) * library.phTreeScales[variant];
                    float y = h - 0.15f;

                    Transform tree = CreateTree(new Vector3(x, y, z), rot, scale, variant, treesRoot);
                    if (tree == null) continue;
                    Trees.Add(tree);

                    if (!placed.TryGetValue(cell, out List<Vector3> list))
                    {
                        list = new List<Vector3>();
                        placed[cell] = list;
                    }
                    list.Add(flat);
                }
            }

            // 5) Unterholz: Büsche, Farne, Schilf (nur Mesh, keine Collider, statisch gebatcht)
            GameObject unterholz = new GameObject("Unterholz");
            // G4: dichtereres Unterholz (Schritt 1.5→1.0 bzw. 1.0→0.6); Zufallsrotation/-skalierung kommen aus PlaceGrid.
            BushCount = PlaceGrid(unterholz.transform, size, 1.0f, library.bush, library.bushMat,
                (i, j, x, z) => World.Height(x, z) >= JungleWorld.WaterLevel + 0.2f
                    && World.Hash01(i, j, 13) < World.Density(x, z) * 0.6f,
                (i, j) => 0.7f + World.Hash01(i, j, 14) * 0.7f, 1500);
            FernCount = PlaceGrid(unterholz.transform, size, 0.6f, library.fern, library.fernMat,
                (i, j, x, z) => World.Hash01(i, j, 23) < World.Density(x, z) * 0.7f,
                (i, j) => 0.8f + World.Hash01(i, j, 24) * 0.8f, 1500);
            ReedCount = PlaceGrid(unterholz.transform, size, 0.7f, library.reed, library.reedMat,
                (i, j, x, z) => World.Height(x, z) >= JungleWorld.WaterLevel - 0.3f
                    && World.Height(x, z) <= JungleWorld.WaterLevel + 0.2f
                    && World.Hash01(i, j, 33) < 0.9f,
                (i, j) => 0.8f + World.Hash01(i, j, 34) * 0.7f, 1500);

            // Riesenbläter (Blender, 3 Varianten): groß tropische Blattschnitte mit
            // Cutout-Maske und Normal. Im selben statischen Batching wie das restliche Unterholz.
            if (library.giantLeaves != null && library.giantLeafMats != null)
            {
                int giantN = 0;
                int gcells = Mathf.FloorToInt(size / 2.0f);
                for (int i = 0; i < gcells && giantN < 400; i++)
                {
                    for (int j = 0; j < gcells && giantN < 400; j++)
                    {
                        float x = 1.0f + i * 2.0f + (World.Hash01(i, j, 41) - 0.5f);
                        float z = 1.0f + j * 2.0f + (World.Hash01(i, j, 42) - 0.5f);
                        if (x < 0.5f || x > size - 0.5f || z < 0.5f || z > size - 0.5f) continue;
                        if (NearSpawn(x, z, 1.0f)) continue;
                        if (!IsDryFlat(World, x, z)) continue;
                        if (World.Hash01(i, j, 61) >= World.Density(x, z) * 0.25f) continue;
                        int variant = Mathf.FloorToInt(World.Hash01(i, j, 62) * (float)library.giantLeaves.Length);
                        if (library.giantLeaves[variant] == null || library.giantLeafMats[variant] == null) continue;
                        float rot = World.Hash01(i, j, 63) * 360f;
                        float scale = 0.7f + World.Hash01(i, j, 64) * 0.7f;
                        AddPlant(unterholz.transform, library.giantLeaves[variant], library.giantLeafMats[variant],
                            new Vector3(x, World.Height(x, z) - 0.05f, z), rot, scale);
                        PlantColliderCount++;
                        giantN++;
                    }
                }
                GiantLeafCount = giantN;
            }
            StaticBatchingUtility.Combine(unterholz);

            // Baumfarne (Blender, 3 Varianten): kletterbarer Stamm (Capsule-Collider) + Blattschnitt-Cutout.
            // Kein statisches Batching: jeder Farn trägt einen eigenen Kletter-Collider.
            // Plus 3 garantierte Exemplare 6–10 m vor dem Spawn, in Blickrichtung der Startkamera.
            if (library.treeFerns != null && library.treeFernLeafMat != null && library.treeFernStemMat != null)
            {
                GameObject baumfarne = new GameObject("Baumfarne");
                int fernN = 0;
                const int fernMax = 120;
                int fcells = Mathf.FloorToInt(size / 7f);
                for (int i = 0; i < fcells && fernN < fernMax; i++)
                {
                    for (int j = 0; j < fcells && fernN < fernMax; j++)
                    {
                        float x = 3.5f + i * 7f + (World.Hash01(i, j, 81) - 0.5f) * 3f;
                        float z = 3.5f + j * 7f + (World.Hash01(i, j, 82) - 0.5f) * 3f;
                        if (x < 0.5f || x > size - 0.5f || z < 0.5f || z > size - 0.5f) continue;
                        if (NearSpawn(x, z, 4f)) continue;
                        if (!IsDryFlat(World, x, z)) continue;
                        if (World.Hash01(i, j, 83) >= World.Density(x, z) * 0.5f) continue;
                        int variant = Mathf.FloorToInt(World.Hash01(i, j, 84) * (float)library.treeFerns.Length);
                        if (variant < 0 || variant >= library.treeFerns.Length) continue;
                        if (library.treeFerns[variant] == null) continue;
                        float rot = World.Hash01(i, j, 85) * 360f;
                        float scale = 0.8f + World.Hash01(i, j, 86) * 0.4f;
                        AddTreeFern(baumfarne.transform, library.treeFerns[variant],
                            new Vector3(x, World.Height(x, z) - 0.05f, z), rot, scale);
                        fernN++;
                    }
                }
                // 3 garantierte Exemplare 6/8/10 m vor dem Spawn: Kamera schaut vom Spawn aus in +Z,
                // also stehen die Farne in Blickrichtung des Start-Screenshots.
                Vector3 vor = new Vector3(0f, 0f, 1f);
                for (int k = 0; k < 3; k++)
                {
                    float dist = 6f + k * 2f;
                    float fx = _spawnXZ.x + vor.x * dist + (World.Hash01(0, k, 91) - 0.5f) * 1.5f;
                    float fz = _spawnXZ.z + vor.z * dist + (World.Hash01(0, k, 92) - 0.5f) * 1.5f;
                    if (fx < 0.5f || fx > size - 0.5f || fz < 0.5f || fz > size - 0.5f) continue;
                    if (!IsDryFlat(World, fx, fz)) continue;
                    int variant = Mathf.FloorToInt(World.Hash01(0, k, 93) * (float)library.treeFerns.Length) % library.treeFerns.Length;
                    if (library.treeFerns[(int)variant] == null) continue;
                    float rot = World.Hash01(0, k, 94) * 360f;
                    float scale = 0.8f + World.Hash01(0, k, 95) * 0.4f;
                    AddTreeFern(baumfarne.transform, library.treeFerns[(int)variant],
                        new Vector3(fx, World.Height(fx, fz) - 0.05f, fz), rot, scale);
                    fernN++;
                }
                TreeFernCount = fernN;
                UnityEngine.Debug.Log("[Jungle] Baumfarne: " + fernN + " (inkl. 3 garantiert vor dem Spawn)");
            }

            // 6b) Bodendecker: Scan-Gras/Kraut als reine Optik (keine Collider), dicht über dem Boden.
            if (library.groundCover != null && library.groundCoverMats != null && library.groundCover.Length > 0)
            {
                GameObject bodendecker = new GameObject("Bodendecker");
                int groundN = 0;
                const int groundMax = 12000;
                const float groundRadius = 28f;
                const float groundStep = 0.5f; // Bei Performance-Problemen auf 0.6f erhöhen.
                int n = Mathf.CeilToInt(groundRadius / groundStep);

                for (int i = -n; i <= n && groundN < groundMax; i++)
                {
                    for (int j = -n; j <= n && groundN < groundMax; j++)
                    {
                        int hi = i + 10000, hj = j + 10000; // positive Indizes für Hash01
                        float x = _spawnXZ.x + i * groundStep + (World.Hash01(hi, hj, 71) - 0.5f) * 0.4f;
                        float z = _spawnXZ.z + j * groundStep + (World.Hash01(hi, hj, 72) - 0.5f) * 0.4f;
                        float ddx = x - _spawnXZ.x, ddz = z - _spawnXZ.z;
                        if (ddx * ddx + ddz * ddz > groundRadius * groundRadius) continue;
                        if (x < 0.5f || x > size - 0.5f || z < 0.5f || z > size - 0.5f) continue;
                        if (!IsDryFlat(World, x, z)) continue;
                        if (World.Height(x, z) < JungleWorld.WaterLevel + 0.1f) continue;

                        int variant = Mathf.FloorToInt(World.Hash01(i, j, 73) * (float)library.groundCover.Length);
                        if (variant < 0 || variant >= library.groundCover.Length || variant >= library.groundCoverMats.Length) continue;
                        Mesh mesh = library.groundCover[variant];
                        Material mat = library.groundCoverMats[variant];
                        if (mesh == null || mat == null) continue;

                        float rot = World.Hash01(i, j, 74) * 360f;
                        float targetH = 0.25f + World.Hash01(i, j, 75) * 0.35f; // 0.25–0.60 m
                        float meshH = Mathf.Max(mesh.bounds.size.y, 0.01f);
                        float scale = targetH / meshH;
                        float h = World.Height(x, z);

                        GameObject g = new GameObject();
                        g.transform.SetParent(bodendecker.transform, false);
                        g.layer = 0; // Default
                        g.transform.position = new Vector3(x, h - 0.02f, z);
                        g.transform.rotation = Quaternion.Euler(0f, rot, 0f);
                        g.transform.localScale = Vector3.one * scale;
                        g.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var r = g.AddComponent<MeshRenderer>();
                        r.sharedMaterial = mat;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        groundN++;
                    }
                }

                StaticBatchingUtility.Combine(bodendecker);
                GroundCoverCount = groundN;
                UnityEngine.Debug.Log($"[Jungle] Bodendecker: {groundN} Objekte, mittlere Höhe 0.25–0.60 m (bounds-basiert)");
            }

            // 5b) Lianen: hängen aus dem Kronendach, dünne senkrechte Halte für Netz-Anker.
            // Eigener Eltern-Node (nicht in das statische Batching der Unterhölzer),
            // 5 Mesh-Varianten, Variante und Skalierung pro Zelle per Hash.
            GameObject lianen = new GameObject("Lianen");
            var lianaMeshes = new Mesh[5];
            for (int k = 0; k < 5; k++) lianaMeshes[k] = JunglePlantFactory.HangingLiana(Seed * 7 + k + 1);
            float lianaStep = 1.3f;
            int lianaCells = Mathf.FloorToInt(size / lianaStep);
            int lianaN = 0;
            for (int i = 0; i < lianaCells && lianaN < 1500; i++)
            {
                for (int j = 0; j < lianaCells && lianaN < 1500; j++)
                {
                    float x = lianaStep / 2f + i * lianaStep + (World.Hash01(i, j, 51) - 0.5f);
                    float z = lianaStep / 2f + j * lianaStep + (World.Hash01(i, j, 52) - 0.5f);
                    if (x < 0.5f || x > size - 0.5f || z < 0.5f || z > size - 0.5f) continue;
                    if (NearSpawn(x, z, 1.0f)) continue;
                    if (!IsDryFlat(World, x, z)) continue; // enthält Height >= WaterLevel + 0.3 und Steigung < 20°
                    if (World.IsClearing(x, z)) continue;
                    int variant = Mathf.FloorToInt(World.Hash01(i, j, 53) * 5f);
                    float rot = World.Hash01(i, j, 54) * 360f;
                    float sxz = 0.8f + World.Hash01(i, j, 55) * 0.5f;
                    float lianaL = 6f + World.Hash01(i, j, 56) * 3f; // Lianen-Länge 6..9 m
                    float gap = 0.2f + World.Hash01(i, j, 57) * 0.6f; // unteres Ende 0.2..0.8 m über Boden
                    GameObject g = AddPlant(lianen.transform, lianaMeshes[variant], library.logMat,
                        new Vector3(x, World.Height(x, z) - 0.05f, z), rot, sxz);
                    // Hängen lassen: 180° um X (Liane hängt nach unten), Y-Skalierung L/3.
                    // Unteres Ende endet in "gap" über dem Boden, oberes Ende im Kronendach.
                    Bounds lb = lianaMeshes[variant].bounds;
                    float sy = lianaL / 3f;
                    g.transform.rotation = Quaternion.Euler(180f, rot, 0f);
                    g.transform.localScale = new Vector3(sxz, sy, sxz);
                    g.transform.position = new Vector3(x, World.Height(x, z) - 0.05f + gap + sy * lb.max.y, z);
                    // Kapsel-Kollider muss die neue gestreckte Höhe abdecken: Kapselhöhe im
                    // Mesh-Lokalraum = lb.size.y, die Y-Skalierung sy dehnt sie auf die Weltlänge.
                    var cc = g.transform.Find("PflanzenCollider").GetComponent<CapsuleCollider>();
                    cc.height = lb.size.y;
                    lianaN++;
                }
            }
            LianaCount = lianaN;

            // 6) Totholz und Steine (mit Convex-Collider und SurfaceTag)
            var treePos = new List<Vector3>(Trees.Count);
            foreach (var tree in Trees) treePos.Add(tree.position);

            GameObject totholz = new GameObject("Totholz");
            int logCount = 12 + (int)(World.Hash01(0, 0, 41) * 9f);
            int placedLogs = 0;
            for (int i = 0; i < 300 && placedLogs < logCount; i++)
            {
                float x = 1f + World.Hash01(0, i, 42) * (size - 2f);
                float z = 1f + World.Hash01(0, i, 43) * (size - 2f);
                if (!IsDryFlat(World, x, z)) continue;
                if (NearAny(treePos, new Vector3(x, 0f, z), 1.5f)) continue;
                if (NearSpawn(x, z, 2.0f)) continue;
                float h = World.Height(x, z);
                CreateProp(totholz.transform, library.logs[placedLogs % Math.Min(3, library.logs.Length)],
                    library.logMat, new Vector3(x, h - 0.05f, z),
                    World.Hash01(0, i, 44) * 360f, 0.8f + World.Hash01(0, i, 45) * 0.4f, SurfaceType.Wood);
                placedLogs++;
            }
            LogCount = placedLogs;

            GameObject steine = new GameObject("Steine");
            int rockCount = 25 + (int)(World.Hash01(0, 0, 61) * 16f);
            int placedRocks = 0;
            for (int i = 0; i < 300 && placedRocks < rockCount; i++)
            {
                float x = 1f + World.Hash01(0, i, 62) * (size - 2f);
                float z = 1f + World.Hash01(0, i, 63) * (size - 2f);
                if (!IsDryFlat(World, x, z)) continue;
                if (NearAny(treePos, new Vector3(x, 0f, z), 1.5f)) continue;
                if (NearSpawn(x, z, 2.0f)) continue;
                float h = World.Height(x, z);
                CreateProp(steine.transform, library.rocks[placedRocks % Math.Min(5, library.rocks.Length)],
                    library.rockMat, new Vector3(x, h - 0.05f, z),
                    World.Hash01(0, i, 64) * 360f, 0.5f + World.Hash01(0, i, 65) * 1.0f, SurfaceType.Rock);
                placedRocks++;
            }
            RockCount = placedRocks;

            // 7) Spinne + Kamera
            var mv = FindFirstObjectByType<SpiderMovement>();
            if (mv != null)
            {
                // Spinne auf das ECHTE Terrain absetzen (nicht nur World.Height):
                // SampleHeight liefert die Höhe der erzeugten Terrain-Surface.
                var terr = t.GetComponent<Terrain>();
                Vector3 s = _spawnXZ;
                s.y = terr.SampleHeight(s) + terr.transform.position.y + 0.03f;
                // Teleport statt roher transform.position: setzt Geschwindigkeit=0,
                // haftend, Dragline-Anker neu – sonst hängen alte Zustände (48,20,48) in der Luft.
                mv.Teleport(s, Quaternion.identity);
                mv.SetWorldBounds(new Vector3(0.5f, -100f, 0.5f), new Vector3(size - 0.5f, 100f, size - 0.5f));
                Physics.SyncTransforms();
                foreach (var leg in mv.GetComponentsInChildren<SpiderLeg>(true))
                    leg.PoseAtRest();

                var cam = Camera.main;
                if (cam != null)
                {
                    cam.transform.position = mv.transform.position + new Vector3(0f, 0.35f, -0.95f);
                    cam.transform.LookAt(mv.transform.position + Vector3.up * 0.1f);
                }
            }

            // 8) Fertig
            sw.Stop();
            GenerationSeconds = (float)sw.Elapsed.TotalSeconds;
            IsReady = true;
            UnityEngine.Debug.Log("[Urwald] Seed " + Seed + ": " + TreeCount + " Bäume, "
                + BushCount + " Büsche, " + FernCount + " Farne, " + ReedCount + " Schilf, " + GiantLeafCount + " Riesenbläter, " + TreeFernCount + " Baumfarne, "
                + GroundCoverCount + " Bodendecker, "
                + LianaCount + " Lianen, "
                + LogCount + " Stämme, " + RockCount + " Steine, "
                + PlantColliderCount + " Pflanzen-Collider, "
                + GenerationSeconds.ToString("F2") + " s");
        }

        private delegate bool PlantPredicate(int i, int j, float x, float z);
        private delegate float PlantScale(int i, int j);

        /// <summary>
        /// Pflanzt ein Mesh auf einem quadratischen Raster (Rastermaß 1.0 m Grundzelle
        /// für Farne, davor Versatz per Hash01). Maximal maxCount Exemplare.
        /// </summary>
        private int PlaceGrid(Transform parent, float size, float step, Mesh mesh, Material mat,
            PlantPredicate ok, PlantScale scale, int maxCount)
        {
            int n = 0;
            int cells = Mathf.FloorToInt(size / step);
            for (int i = 0; i < cells && n < maxCount; i++)
            {
                for (int j = 0; j < cells && n < maxCount; j++)
                {
                    float off = 0.5f;
                    float x = step / 2f + i * step + (World.Hash01(i, j, SaltA(step)) - 0.5f) * (2f * off);
                    float z = step / 2f + j * step + (World.Hash01(i, j, SaltA(step) + 1) - 0.5f) * (2f * off);
                    if (x < 0.5f || x > size - 0.5f || z < 0.5f || z > size - 0.5f) continue;
                    if (NearSpawn(x, z, 1.0f)) continue; // Unterholz: 1 m um den Spawnplatz frei
                    if (!ok(i, j, x, z)) continue;
                    float rot = World.Hash01(i, j, SaltA(step) + 4) * 360f;
                    AddPlant(parent, mesh, mat, new Vector3(x, World.Height(x, z) - 0.05f, z), rot, scale(i, j));
                    PlantColliderCount++;
                    n++;
                }
            }
            return n;
        }

        /// <summary>Rastergröße auf Salzpaar abbilden: 1.5 → 11, 1.0 → 21, 0.7 → 31.</summary>
        private static int SaltA(float step) => step >= 1.4f ? 11 : step >= 0.9f ? 21 : 31;

        /// <summary>Mesh-Objekt mit Kind „PflanzenCollider“ erzeugen (Spinne/Netze können andocken).</summary>
        private static GameObject AddPlant(Transform parent, Mesh mesh, Material mat, Vector3 pos, float rotY, float scale)
        {
            GameObject g = new GameObject();
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            g.transform.localScale = Vector3.one * scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;

            // Dünnkapsel in der Mesh-Mitte: klein genug, dass die Spinne nicht feststeckt,
            // groß genug, dass Netz-Anker und Beine andocken können.
            GameObject c = new GameObject("PflanzenCollider");
            c.transform.SetParent(g.transform, false);
            c.layer = GameLayers.Plants;
            Bounds b = mesh.bounds;
            Vector3 s = g.transform.localScale;
            var cc = c.AddComponent<CapsuleCollider>();
            cc.direction = 1; // Y-Achse
            cc.height = b.size.y * s.y;
            cc.radius = Mathf.Clamp(0.25f * Mathf.Min(b.size.x * s.x, b.size.z * s.z), 0.015f, 0.12f);
            c.transform.position = g.transform.TransformPoint(b.center);
            return g;
        }

        /// <summary>Kann ein Objekt an (x,z) stehen: trocken und Steigung < 20°?</summary>
        private static bool IsDryFlat(JungleWorld world, float x, float z)
        {
            float h = world.Height(x, z);
            if (h < JungleWorld.WaterLevel + 0.3f) return false;
            float e = 0.5f;
            float dh = Mathf.Max(Mathf.Abs(world.Height(x + e, z) - h),
                                 Mathf.Abs(world.Height(x, z + e) - h));
            return Mathf.Atan2(dh, e) * Mathf.Rad2Deg < 20f;
        }

        /// <summary>Wagrechter Abstand zum Spawnplatz kleiner als dist?</summary>
        private bool NearSpawn(float x, float z, float dist)
        {
            float dx = x - _spawnXZ.x, dz = z - _spawnXZ.z;
            return dx * dx + dz * dz < dist * dist;
        }

        private static bool NearAny(List<Vector3> pos, Vector3 p, float dist)
        {
            float d2 = dist * dist;
            foreach (Vector3 q in pos)
                if ((q - p).sqrMagnitude < d2) return true;
            return false;
        }

        /// <summary>
        /// Baumfarn erzeugen: ein MeshRenderer mit 2 sharedMaterials (0 = Stamm, 1 = Blatt,
        /// Submesh-Reihenfolge) + dünner Capsule-Kollider am Stamm (Spinne kann klettern).
        /// Die Wedel selbst bekommen keinen Collider.
        /// </summary>
        private void AddTreeFern(Transform parent, Mesh mesh, Vector3 pos, float rotY, float scale)
        {
            GameObject g = new GameObject();
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            g.transform.localScale = Vector3.one * scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { library.treeFernStemMat, library.treeFernLeafMat };

            // Kletter-Stamm: dünne Kapsel an der Basis (Radius 0.16, Höhe = bounds.y * 0.75 * scale).
            // Die Wedel (oberes Viertel) bleiben kollisionsfrei.
            Bounds b = mesh.bounds;
            GameObject c = new GameObject("StammCollider");
            c.transform.SetParent(g.transform, false);
            c.layer = GameLayers.Plants;
            var cc = c.AddComponent<CapsuleCollider>();
            cc.direction = 1; // Y-Achse
            cc.radius = 0.16f;
            cc.height = b.size.y * 0.75f * scale;
            c.transform.localPosition = new Vector3(0f, 0.375f * b.size.y, 0f);
        }

        /// <summary>Stamm/Stein mit Convex-Collider und SurfaceTag erzeugen.</summary>
        private static void CreateProp(Transform parent, Mesh mesh, Material mat, Vector3 pos, float rotY, float scale, SurfaceType surface)
        {
            GameObject g = new GameObject();
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            g.transform.localScale = Vector3.one * scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var mc = g.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
            mc.convex = true;
            g.AddComponent<SurfaceTag>().surfaceType = surface;
        }

        private static bool HasNearbyTree(Dictionary<Vector2Int, List<Vector3>> placed, Vector2Int cell, Vector3 pos)
        {
            for (int cx = cell.x - 1; cx <= cell.x + 1; cx++)
            {
                for (int cz = cell.y - 1; cz <= cell.y + 1; cz++)
                {
                    if (!placed.TryGetValue(new Vector2Int(cx, cz), out List<Vector3> list)) continue;
                    foreach (Vector3 p in list)
                        if ((p - pos).sqrMagnitude < 2.2f * 2.2f) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// PH-Tree (Poly Haven) aufbauen: LOD0/LOD1-Mesh from Library, one MeshRenderer per LOD
        /// with 2 materials in SUBMESH order. Submesh order NOT guaranteed by FBX import ->
        /// wood/leaf assigned per GetSubMesh(i).bounds (taller+narrower = Wood, broader = Leaf).
        /// MeshCollider on the LOD1-Mesh (concave), SurfaceTag Wood on root and collider node.
        /// </summary>
        private Transform CreateTree(Vector3 pos, float rotY, float scale, int variant, Transform parent)
        {
            Mesh lod0 = library.phTreeLod0[variant];
            Mesh lod1 = library.phTreeLod1 != null && variant < library.phTreeLod1.Length ? library.phTreeLod1[variant] : lod0;
            if (lod0 == null) return null;
            Material woodMat = library.phTreeWoodMats[variant];
            Material leafMat = library.phTreeLeafMats[variant];

            // Planting: align the tree so no TRUNK vertex floats more than 0.35 m above the
            // terrain under it (measured in TreesTests: root-pos and footprint-corner sampling
            // both floated leaning/offset trunks 3..6 m, so the Spider was not blocked).
            float baseDrop = lod0.bounds.min.y * scale;
            Vector3 woodCenterLocal = lod0.bounds.center;
            Vector3 woodExtLocal = lod0.bounds.extents;
            int woodSub = -1;
            if (lod0.subMeshCount >= 2)
            {
                float bestArea = 1e9f;
                for (int s = 0; s < lod0.subMeshCount; s++)
                {
                    Bounds sb = lod0.GetSubMesh(s).bounds;
                    float area = sb.size.x * sb.size.z;
                    if (sb.size.y >= 1f && area < bestArea) { bestArea = area; woodSub = s; }
                }
                if (woodSub >= 0)
                {
                    baseDrop = lod0.GetSubMesh(woodSub).bounds.min.y * scale;
                    woodCenterLocal = lod0.GetSubMesh(woodSub).bounds.center;
                    woodExtLocal = lod0.GetSubMesh(woodSub).bounds.extents;
                }
            }
            GameObject tree = new GameObject("PH-Tree");
            tree.transform.SetParent(parent, false);
            Terrain terr = Terrain.activeTerrain;
            float groundAtPos = pos.y;
            if (terr != null)
            {
                Vector3 terrPos = terr.transform.position;
                groundAtPos = terr.SampleHeight(pos) + terrPos.y;
                Quaternion rotYq = Quaternion.Euler(0f, rotY, 0f);
                float rootY = groundAtPos + 0.35f - baseDrop;
                tree.transform.position = new Vector3(pos.x, rootY, pos.z);
            }
            else
            {
                tree.transform.position = new Vector3(pos.x, pos.y + 0.35f, pos.z) - Vector3.up * baseDrop;
            }
            tree.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            tree.transform.localScale = Vector3.one * scale;
            tree.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Wood;

            // Material per Submesh: Wood/Leaf in der ACTUALN submesh-order (Blender-Order not guaranteed).
            MeshRenderer r0 = null, r1 = null;
            GameObject lod0Node = new GameObject("TreeLOD0");
            lod0Node.transform.SetParent(tree.transform, false);
            lod0Node.transform.localPosition = Vector3.zero;
            lod0Node.AddComponent<MeshFilter>().sharedMesh = lod0;
            r0 = lod0Node.AddComponent<MeshRenderer>();
            r0.sharedMaterials = PickMaterialsPerSubmesh(lod0, woodMat, leafMat, variant);

            if (lod1 != lod0)
            {
                GameObject lod1Node = new GameObject("TreeLOD1");
                lod1Node.transform.SetParent(tree.transform, false);
                lod1Node.transform.localPosition = Vector3.zero;
                lod1Node.AddComponent<MeshFilter>().sharedMesh = lod1;
                r1 = lod1Node.AddComponent<MeshRenderer>();
                r1.sharedMaterials = PickMaterialsPerSubmesh(lod1, woodMat, leafMat, variant);
            }

            // Collider on the LOD0-Mesh: LOD1 lacks lower-trunk triangles (measured in TreesTests),
            // so blocking the Spider needs the full scan mesh (concave).
            GameObject colNode = new GameObject("TreeCollider");
            colNode.transform.SetParent(tree.transform, false);
            var mc = colNode.AddComponent<MeshCollider>();
            mc.sharedMesh = lod0;
            mc.convex = false;

            // Planting fix (runtime): scans are not vertex-readable at runtime, so measure the
            // trunk-bottom offset via downcasts on the TreeCollider within the TRUNK footprint
            // and sink the tree until no trunk column floats more than 0.35 m over terrain
            // (measured in TreesTests: offset/leaning trunks floated 3..6 m, Spider not blocked).
            if (terr != null && woodSub >= 0)
            {
                Bounds wsb = lod0.GetSubMesh(woodSub).bounds;
                Vector3 wl = colNode.transform.TransformPoint(wsb.center - wsb.extents);
                Vector3 wh = colNode.transform.TransformPoint(wsb.center + wsb.extents);
                float rootY0 = tree.transform.position.y;
                float footOffset = 1e9f;   // lowest trunk point relative to rootY
                float footGround = -1e9f;
                for (float x = wl.x; x <= wh.x; x += 0.5f)
                {
                    for (float z = wl.z; z <= wh.z; z += 0.5f)
                    {
                        var top = new Vector3(x, wh.y + 2f, z);
                        RaycastHit[] hits = Physics.RaycastAll(top, Vector3.down, (wh.y - wl.y) + 6f);
                        float lowestOwn = 1e9f;
                        foreach (var h in hits)
                            if (h.collider.gameObject == colNode && h.point.y < lowestOwn) lowestOwn = h.point.y;
                        if (lowestOwn >= 1e9f) continue; // no geometry in this column
                        float offset = lowestOwn - rootY0;
                        if (offset < footOffset)
                        {
                            footOffset = offset;
                            footGround = terr.SampleHeight(new Vector3(x, 0f, z)) + terr.transform.position.y;
                        }
                    }
                }
                if (footOffset < 1e9f)
                {
                    // Plant so the trunk FOOT sits ~0.25 m above the terrain under it.
                    float newRootY = footGround + 0.25f - footOffset;
                    if (newRootY < rootY0 && rootY0 - newRootY <= 10f)
                        tree.transform.position = new Vector3(tree.transform.position.x, newRootY, tree.transform.position.z);
                }
            }

            var lod = tree.AddComponent<LODGroup>();
            lod.SetLODs(new[]
            {
                new LOD(0.18f, new MeshRenderer[] { r0 }),
                new LOD(0.04f, new MeshRenderer[] { r1 != null ? r1 : r0 }),
            });
            lod.fadeMode = LODFadeMode.CrossFade;
            lod.RecalculateBounds();

            return tree.transform;
        }

        /// <summary>
        /// Wood/Leaf per Submesh via bounds: der Submesh mit der grueateren Bounds.Y und der
        /// schmalerer x/z-Footprint = Wood (Stem), die broader = Leaf (Crown). One Submesh: Wood.
        /// Logs the measured values once per variant ("Value -> Kriterium -> Result").
        /// </summary>
        private static Material[] PickMaterialsPerSubmesh(Mesh mesh, Material woodMat, Material leafMat, int variant)
        {
            int subs = Math.Max(1, mesh.subMeshCount);
            var matPerSub = new Material[subs];
            if (subs == 1)
            {
                matPerSub[0] = woodMat;
                return matPerSub;
            }
            Bounds m0 = mesh.GetSubMesh(0).bounds;
            Bounds m1 = mesh.GetSubMesh(1).bounds;
            float f0 = m0.size.x * m0.size.z, f1 = m1.size.x * m1.size.z;
            bool sub0IsWood = m0.size.y >= m1.size.y && f0 <= f1;
            UnityEngine.Debug.Log(string.Format("[Tree] variant {0}: s0 h={1:0.00} f={2:0.000} | s1 h={3:0.00} f={4:0.000} -> Wood=Sub{5}",
                variant, m0.size.y, f0, m1.size.y, f1, sub0IsWood ? 0 : 1));
            matPerSub[0] = sub0IsWood ? woodMat : leafMat;
            matPerSub[1] = sub0IsWood ? leafMat : woodMat;
            return matPerSub;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            {
                NextSeed = 1 + (System.Environment.TickCount & 0x7fffffff) % 999999;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 400, 24), "Welt-Seed: " + Seed + "   (F5 = neue Welt)");
        }
    }
}
