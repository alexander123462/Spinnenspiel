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
        public int DebrisCount { get; private set; }
        public float DebrisMaxSizeMeters { get; private set; }
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
            Terrain terrain = t.GetComponent<Terrain>();

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
                    // FIX: SampleHeight statt World.Height, damit Baum-Base bei Terrain + 0.15m steht (konsistent mit VegetationBuilder.BuildTrees()).
                    // + terrain.transform.position.y: Terrain is y=0 today, but the test's groundY adds it; keep root contract exact either way.
                    float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y + 0.15f;

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

                // DENSE grass patches: extra pass in forest clearings (ForestFactor low),
                // finer step and taller blades so meadows look really thick.
                const float denseStep = 0.28f;
                int denseN = Mathf.CeilToInt(groundRadius / denseStep);
                int denseAdded = 0;
                const int denseMax = 14000;
                for (int i = -denseN; i <= denseN && groundN < groundMax + denseMax; i++)
                {
                    for (int j = -denseN; j <= denseN && groundN < groundMax + denseMax; j++)
                    {
                        int hi = i + 20000, hj = j + 20000;
                        float x = _spawnXZ.x + i * denseStep + (World.Hash01(hi, hj, 81) - 0.5f) * 0.22f;
                        float z = _spawnXZ.z + j * denseStep + (World.Hash01(hi, hj, 82) - 0.5f) * 0.22f;
                        float ddx = x - _spawnXZ.x, ddz = z - _spawnXZ.z;
                        if (ddx * ddx + ddz * ddz > groundRadius * groundRadius) continue;
                        if (x < 0.5f || x > size - 0.5f || z < 0.5f || z > size - 0.5f) continue;
                        if (WorldLayout.ForestFactor(x, z) > 0.3f) continue; // only in clearings
                        float patch = World.Hash01(hi / 2, hj / 2, 83); // coarse patch field
                        if (patch < 0.45f) continue;                     // patchy, not uniform
                        if (!IsDryFlat(World, x, z)) continue;
                        if (World.Height(x, z) < JungleWorld.WaterLevel + 0.1f) continue;

                        int variant = Mathf.FloorToInt(World.Hash01(hi, hj, 84) * (float)library.groundCover.Length);
                        if (variant < 0 || variant >= library.groundCover.Length || variant >= library.groundCoverMats.Length) continue;
                        Mesh mesh = library.groundCover[variant];
                        Material mat = library.groundCoverMats[variant];
                        if (mesh == null || mat == null) continue;

                        float rot = World.Hash01(hi, hj, 85) * 360f;
                        float targetH = 0.45f + World.Hash01(hi, hj, 86) * 0.45f; // 0.45-0.9 m
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
                        denseAdded++;
                    }
                }
                UnityEngine.Debug.Log($"[Jungle] Bodendecker: {groundN} Objekte ({denseAdded} Dense-Grass in clearings, ForestFactor<0.3, step {denseStep} m)");
            }

            // 5c) M7-10.5 Spinngscale Debris: cm-sized Laubstreu, Zweiglein, Kieselchen.
            // Fein detail for the spider macro-perspective; no colliders (walk on ground).
            GameObject debris = new GameObject("Debris");
            var leafMeshes = new Mesh[6];
            for (int k = 0; k < 6; k++) leafMeshes[k] = JunglePlantFactory.FallenLeaf(Seed * 11 + k + 1);
            var twigMeshes = new Mesh[4];
            for (int k = 0; k < 4; k++) twigMeshes[k] = JunglePlantFactory.GroundTwig(Seed * 13 + k + 1);
            int debrisN = 0;
            const int debrisMax = 4500;
            const float debrisRadius = 26f;
            const float debrisStep = 0.55f;
            {
                int dn = Mathf.CeilToInt(debrisRadius / debrisStep);
                for (int i = -dn; i <= dn && debrisN < debrisMax; i++)
                {
                    for (int j = -dn; j <= dn && debrisN < debrisMax; j++)
                    {
                        int hi = i + 10000, hj = j + 10000;
                        float x = _spawnXZ.x + i * debrisStep + (World.Hash01(hi, hj, 91) - 0.5f) * 0.3f;
                        float z = _spawnXZ.z + j * debrisStep + (World.Hash01(hi, hj, 92) - 0.5f) * 0.3f;
                        float ddx = x - _spawnXZ.x, ddz = z - _spawnXZ.z;
                        if (ddx * ddx + ddz * ddz > debrisRadius * debrisRadius) continue;
                        if (x < 0.5f || x > size - 0.5f || z < 0.5f || z > size - 0.5f) continue;
                        if (!IsDryFlat(World, x, z)) continue;
                        if (World.Height(x, z) < JungleWorld.WaterLevel + 0.05f) continue;

                        float kind = World.Hash01(i, j, 93);
                        Mesh mesh; Material mat; float scale;
                        if (kind < 0.6f)
                        {
                            int v = Mathf.FloorToInt(World.Hash01(i, j, 94) * leafMeshes.Length) % leafMeshes.Length;
                            mesh = leafMeshes[v]; mat = library.leaves; scale = 1f;
                        }
                        else if (kind < 0.85f)
                        {
                            int v = Mathf.FloorToInt(World.Hash01(i, j, 95) * twigMeshes.Length) % twigMeshes.Length;
                            mesh = twigMeshes[v]; mat = library.logMat; scale = 1f;
                        }
                        else
                        {
                            int v = Mathf.FloorToInt(World.Hash01(i, j, 96) * Math.Min(5, library.rocks.Length)) % Math.Max(1, library.rocks.Length);
                            mesh = library.rocks[v]; mat = library.rockMat;
                            float ext = Mathf.Max(Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y), mesh.bounds.size.z);
                            scale = 0.02f / Mathf.Max(ext, 0.01f); // max-dimension ~2 cm
                        }
                        if (mesh == null || mat == null) continue;

                        float rot = World.Hash01(i, j, 97) * 360f;
                        float h = World.Height(x, z);
                        GameObject g = new GameObject();
                        g.transform.SetParent(debris.transform, false);
                        g.layer = 0;
                        g.transform.position = new Vector3(x, h + 0.001f, z);
                        g.transform.rotation = Quaternion.Euler(0f, rot, 0f);
                        g.transform.localScale = Vector3.one * scale;
                        g.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var r = g.AddComponent<MeshRenderer>();
                        r.sharedMaterial = mat;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        Vector3 worldSize = mesh.bounds.size * scale;
                        float maxDim = Mathf.Max(Mathf.Max(worldSize.x, worldSize.y), worldSize.z);
                        if (maxDim > DebrisMaxSizeMeters) DebrisMaxSizeMeters = maxDim;
                        debrisN++;
                    }
                }
                StaticBatchingUtility.Combine(debris);
            }
            DebrisCount = debrisN;
            UnityEngine.Debug.Log($"[Jungle] Spinngscale-Debris: {debrisN} Objekte (Laubstreu/Zweiglein/Kieseln, cm-sized), grösste {DebrisMaxSizeMeters * 100f:0.0} cm");

            // 5b) Lianen: an Ästen der PH-Bäume hängen (Startpunkt in der Kronen-Bounds),
            // leicht geschwung, unteres Ende 0.2-0.8 m über Boden. Collider = Kette kurzer
            // Kapseln (worldspace, SurfaceTag Wood) -> Spinne klettert + Netz-Anker nutzbar.
            GameObject lianen = new GameObject("Lianen");
            var lianaMeshes = new Mesh[5];
            for (int k = 0; k < 5; k++) lianaMeshes[k] = JunglePlantFactory.HangingLiana(Seed * 7 + k + 1);
            int lianaN = 0;
            const int lianaMax = 300;
            for (int tIdx = 0; tIdx < Trees.Count && lianaN < lianaMax; tIdx++)
            {
                Transform tree = Trees[tIdx];
                Mesh lod0 = null;
                var mf = tree.transform.Find("TreeLOD0").GetComponent<MeshFilter>();
                if (mf != null) lod0 = mf.sharedMesh;
                if (lod0 == null) continue;
                Vector3 gb = lod0.bounds.center, ge = lod0.bounds.extents;
                float ts = Mathf.Max(0.01f, tree.transform.localScale.x);
                Vector3 rootPos = tree.transform.position;
                float lod0WorldY = mf.transform.position.y;
                // Kronen in WORLD-space: obere Drittel der (schiertas) Bounds.
                float crownTopY = lod0WorldY + (gb.y + ge.y) * ts;
                float crownBotY = crownTopY - 2f * ge.y * ts;
                float radiusX = ge.x * ts, radiusZ = ge.z * ts;
                int wantedThisTree = 3 + (int)(World.Hash01(tIdx, 0, 71) * 6f); // 3..8 per Tree
                for (int a = 0; a < wantedThisTree && lianaN < lianaMax; a++)
                {
                    float ax = rootPos.x + (World.Hash01(tIdx, a, 72) * 2f - 1f) * radiusX * 0.85f;
                    float az = rootPos.z + (World.Hash01(tIdx, a, 73) * 2f - 1f) * radiusZ * 0.85f;
                    // Downcast: die FIRST hit on the TreeCollider (child of tree) = Branch-Surface.
                    Vector3 origin = new Vector3(ax, crownTopY + 1f, az);
                    float rayLen = (crownTopY - crownBotY) + 2f;
                    RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, rayLen);
                    bool found = false;
                    Vector3 anchor = Vector3.zero;
                    foreach (var h in hits)
                    {
                        if (h.collider.gameObject.transform.parent != tree) continue; // not this Tree
                        float wy = h.point.y;
                        if (wy < crownBotY || wy > crownTopY) continue; // not in the Kronen
                        if (!found || wy > anchor.y) anchor = h.point; // highest branch hit
                        found = true;
                    }
                    if (!found) continue; // no Branch there -> skip
                    float groundY = World.Height(anchor.x, anchor.z);
                    float gap = 0.2f + World.Hash01(tIdx, a, 74) * 0.6f; // unteres Ende 0.2..0.8 m
                    float len = (anchor.y - groundY) - gap;
                    if (len < 1.5f || len > 30f) continue; // too short / Branch too high
                    int variant = Mathf.FloorToInt(World.Hash01(tIdx, a, 75) * 5f) % 5;
                    float rot = World.Hash01(tIdx, a, 76) * 360f;
                    GameObject g = AddLiana(lianen.transform, lianaMeshes[variant], library.logMat,
                        anchor, rot, len);
                    if (g == null) continue;
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

        /// <summary>
        /// Hänging Liane at an branch anchor: the mesh hangs down (top at anchor), scaled so its
        /// lower end sits 'len' below the anchor. Collider = Kette kurzer Kapseln (identity scale,
        /// so no capsule distortion) with SurfaceTag Wood -> Spider can climb and WebSiteFinder
        /// can use them as Netz-Anker.
        /// </summary>
        private GameObject AddLiana(Transform parent, Mesh mesh, Material mat, Vector3 anchor, float rotY, float len)
        {
            Bounds lb = mesh.bounds;
            float sizeY = lb.size.y;
            if (sizeY < 0.1f || len < 0.5f) return null;
            float sy = len / sizeY;

            GameObject g = new GameObject("Liana");
            g.transform.SetParent(parent, false);
            g.transform.position = anchor;
            g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            g.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Wood;

            // Visual: non-uniform scale (thin in XZ, stretched in Y), top of mesh at the anchor.
            GameObject vis = new GameObject("Visual");
            vis.transform.SetParent(g.transform, false);
            vis.transform.localPosition = new Vector3(0f, -sy * lb.max.y, 0f);
            vis.transform.localScale = new Vector3(1f, sy, 1f);
            vis.AddComponent<MeshFilter>().sharedMesh = mesh;
            vis.AddComponent<MeshRenderer>().sharedMaterial = mat;

            // Collider chain: Kapsels of ~2.4 m in root space (root scale = 1).
            int segs = Mathf.Max(2, Mathf.CeilToInt(len / 2.4f));
            float segLen = len / segs;
            for (int s = 0; s < segs; s++)
            {
                GameObject c = new GameObject("Seg" + s);
                c.transform.SetParent(g.transform, false);
                c.transform.localPosition = new Vector3(0f, -(s + 0.5f) * segLen, 0f);
                c.layer = GameLayers.Plants;
                var cc = c.AddComponent<CapsuleCollider>();
                cc.direction = 1; // Y-Axis
                cc.radius = 0.05f;
                cc.height = segLen + 0.06f; // small overlap so the chain has no gaps
            }
            return g;
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
            // ONE woodSub per tree feeds material assignment AND anchor/collider/baseDrop
            // (BUG 1: two separate rules diverged and picked BRANCHES on the Jacaranda).
            int woodSub = PickWoodSubmesh(lod0, variant, scale);
            float baseDrop = lod0.GetSubMesh(woodSub).bounds.min.y * scale;
            GameObject tree = new GameObject("PH-Tree");
            tree.transform.SetParent(parent, false);
            Terrain terr = Terrain.activeTerrain;
            float groundAtPos = pos.y;
            if (terr != null)
            {
                Vector3 terrPos = terr.transform.position;
                groundAtPos = terr.SampleHeight(pos) + terrPos.y;
                Quaternion rotYq = Quaternion.Euler(0f, rotY, 0f);
                float rootY = groundAtPos + 0.15f;
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
            r0.sharedMaterials = AssignMaterialsPerSubmesh(lod0, woodSub, woodMat, leafMat);

            GameObject lod1Node = null;
            if (lod1 != lod0)
            {
                lod1Node = new GameObject("TreeLOD1");
                lod1Node.transform.SetParent(tree.transform, false);
                lod1Node.transform.localPosition = Vector3.zero;
                lod1Node.AddComponent<MeshFilter>().sharedMesh = lod1;
                r1 = lod1Node.AddComponent<MeshRenderer>();
                r1.sharedMaterials = AssignMaterialsPerSubmesh(lod1, PickWoodSubmesh(lod1, variant, scale), woodMat, leafMat);
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
                // WELT-AABB aus ALLEN 8 Eckpunkten: nur die beiden Diagonalecken
                // (center ∓ extents) zu transformieren liefert bei Y-Rotation KEIN gültiges
                // min/max (z. B. center (2,0,0), ext (1,1,1), rotY 150°: wl.x > wh.x),
                // wodurch die Scan-Loops leer liefen und das Einpflanzen übersprungen wurde.
                Vector3 bc = wsb.center, be = wsb.extents;
                Vector3 wl = new Vector3(1e30f, 1e30f, 1e30f);
                Vector3 wh = new Vector3(-1e30f, -1e30f, -1e30f);
                for (int ci = 0; ci < 8; ci++)
                {
                    Vector3 corner = colNode.transform.TransformPoint(new Vector3(
                        (ci & 1) == 0 ? bc.x - be.x : bc.x + be.x,
                        (ci & 2) == 0 ? bc.y - be.y : bc.y + be.y,
                        (ci & 4) == 0 ? bc.z - be.z : bc.z + be.z));
                    wl = Vector3.Min(wl, corner);
                    wh = Vector3.Max(wh, corner);
                }
                // PERF: cap the scan to a trunk-centred box. A wide root-plate/flare woodSub
                // would otherwise span tens of metres -> 1e4 rays/tree (GenSeed 11s, vegCost 45ms).
                // The trunk foot can sit FAR from the woodSub XZ centre (jacaranda: woodSub is the
                // leaves submesh, crown-centred, trunk foot offset >3 m) -> a centred box missed it
                // and the planting shift never ran. Centre the cap on the TRUNK AXIS instead.
                Vector3 wc = new Vector3(tree.transform.position.x, 0f, tree.transform.position.z);
                float scanHalf = 3f;
                float sx0 = Mathf.Max(wl.x, wc.x - scanHalf), sx1 = Mathf.Min(wh.x, wc.x + scanHalf);
                float sz0 = Mathf.Max(wl.z, wc.z - scanHalf), sz1 = Mathf.Min(wh.z, wc.z + scanHalf);
                float scanStep = 1f;
                float rootY0 = tree.transform.position.y;
                float footOffset = 1e9f;   // lowest trunk point relative to rootY
                float footGround = -1e9f;
                // Reusable NonAlloc-Buffer, local to this Tree-Scan (no per-Sample-Allocations)
                RaycastHit[] scanHits = new RaycastHit[64];
                for (float x = sx0; x <= sx1; x += scanStep)
                {
                    for (float z = sz0; z <= sz1; z += scanStep)
                    {
                        var top = new Vector3(x, wh.y + 2f, z);
                        float rayLen = (wh.y - wl.y) + 6f;
                        float lowestOwn = 1e9f;
                        int hitCount = Physics.RaycastNonAlloc(top, Vector3.down, scanHits, rayLen);
                        if (hitCount >= scanHits.Length)
                        {
                            // Buffer filled: fall back to RaycastAll, so the result stays correct
                            RaycastHit[] hits = Physics.RaycastAll(top, Vector3.down, rayLen);
                            foreach (var h in hits)
                                if (h.collider.gameObject == colNode && h.point.y < lowestOwn) lowestOwn = h.point.y;
                        }
                        else
                        {
                            for (int i = 0; i < hitCount; i++)
                            {
                                var h = scanHits[i];
                                if (h.collider.gameObject == colNode && h.point.y < lowestOwn) lowestOwn = h.point.y;
                            }
                        }
                        if (lowestOwn >= 1e9f) continue; // no geometry in this column
                        float offset = lowestOwn - rootY0;
                        if (offset < footOffset)
                        {
                            footOffset = offset;
                            footGround = terr.SampleHeight(new Vector3(x, 0f, z)) + terr.transform.position.y;
                        }
                    }
                }
                // Planting via woodSub-Bottom (Bounds), not via raycast foot: species whose FBX pivot sits high above the wood bottom (island_tree_03, jacaranda) need the full bounds drop; a small clamp would leave them floating.
                float newRootY = footGround + 0.25f - wsb.min.y * scale;
                // Sanity guard: never push a whole crown below ground, never lift more than 3 m.
                float woodWorldH = (wsb.max.y - wsb.min.y) * scale;
                if (newRootY != rootY0 && (rootY0 - newRootY) <= woodWorldH + 1f && (newRootY - rootY0) <= 3f)
                {
                    // Plant so the woodSub BOTTOM sits ~0.25 m above the terrain under it.
                    // BOTH directions: with a wrong woodSub (branches) the old down-only
                    // rule sank trees ~9.5 m; now the trunk foot is raised too if needed.
                    // FIX: shift the VISUAL+COLLIDER CHILDREN, never the tree root: the
                    // root must stay at SampleHeight + 0.15 (VegetationContactTest contract);
                    // moving the root broke it deterministically (-3.2..-4.6 m on variants
                    // whose FBX pivot is not at the trunk base, e.g. jacaranda woodSub min.y=2.2).
                    float dropLocal = (newRootY - rootY0) / scale; // local units
                    lod0Node.transform.localPosition += new Vector3(0f, dropLocal, 0f);
                    colNode.transform.localPosition += new Vector3(0f, dropLocal, 0f);
                    if (lod1Node != null)
                        lod1Node.transform.localPosition += new Vector3(0f, dropLocal, 0f);
                }
            }

            var lod = tree.AddComponent<LODGroup>();
            if (r1 != null)
            {
                // BUG 3 guard was here: r0 must never sit in two LOD levels. Now the
                // single-LOD path below handles lod1 == lod0 (FBX without LOD1).
                lod.SetLODs(new[]
                {
                    new LOD(0.18f, new MeshRenderer[] { r0 }),
                    new LOD(0.04f, new MeshRenderer[] { r1 }),
                });
            }
            else
            {
                lod.SetLODs(new[] { new LOD(0.18f, new MeshRenderer[] { r0 }) });
            }
            lod.fadeMode = LODFadeMode.CrossFade;
            lod.RecalculateBounds();

            return tree.transform;
        }

        /// <summary>
        /// UNIFIED trunk pick (BUG 1: anchor-rule and material-rule diverged and picked BRANCHES
        /// on the Jacaranda, sinking trees ~9.5 m): ONE woodSub per tree feeds material
        /// assignment AND anchor/collider/baseDrop. Slot NAMES come from the FBX LOD0 MeshRenderer
        /// resolved once in the Editor (library.phTreeSlotNames[variant], '|' = submesh index;
        /// Mesh itself has NO material-name accessor - CS1061). bark|trunk -> Wood, twig weakly
        /// Wood, leaf|leaves|branch -> Leaf; geometry fallback in WORLD units (scale-multiplied,
        /// BUG 2: the old local-unit tolerance grew with scale up to 5 and flipped Wood onto
        /// root-plate submeshes): trunk = reaches ground (0.3 m) + smallest footprint; short
        /// submeshes (twigs) penalised.
        /// Logs "Value -> Kriterium -> Result" once per variant as ground truth.
        /// </summary>
        private int PickWoodSubmesh(Mesh mesh, int variant, float scale)
        {
            int subs = Math.Max(1, mesh.subMeshCount);
            if (subs == 1) return 0;
            string[] slotNames = null;
            if (library.phTreeSlotNames != null && variant < library.phTreeSlotNames.Length &&
                library.phTreeSlotNames[variant] != null && library.phTreeSlotNames[variant].Length > 0)
                slotNames = library.phTreeSlotNames[variant].Split('|');
            float meshMinYWorld = mesh.bounds.min.y * scale;
            int woodSub = -1;
            float bestScore = 1e9f;
            for (int s = 0; s < subs; s++)
            {
                Bounds sb = mesh.GetSubMesh(s).bounds;
                float areaWorld = sb.size.x * scale * sb.size.z * scale;
                bool touchesGround = sb.min.y * scale <= meshMinYWorld + 0.3f;
                float score = areaWorld + (touchesGround ? 0f : 1e6f);
                if (sb.size.y * scale < 0.5f) score += 1e5f; // twig/flower submeshes never win
                // Name match FIRST: FBX slot names are ground truth (Poly Haven names per part;
                // Unity sorts slots alphabetically, so index order is NOT a contract).
                // bark|trunk|wood -> Wood, leaf|leaves|branch(es) -> Leaf. Geometry only as
                // fallback when the slot has no usable name.
                string slotName = slotNames != null && s < slotNames.Length ? slotNames[s].ToLower() : "";
                if (slotName.Contains("bark") || slotName.Contains("trunk")) score -= 1e7f; // wood slot
                else if (slotName.Contains("twig")) score -= 1e4f; // twig counts as wood but weaker
                if (slotName.Contains("leaf") || slotName.Contains("leaves") || slotName.Contains("branch")) score += 1e7f; // crown slot
                UnityEngine.Debug.Log(string.Format("[Tree] v{0} s{1}: minY={2:0.000} maxY={3:0.000} hW={4:0.00} fW={5:0.000} touch={6} slot='{7}' score={8:0.0}",
                    variant, s, sb.min.y, sb.max.y, sb.size.y * scale, areaWorld, touchesGround, slotName, score));
                if (score < bestScore) { bestScore = score; woodSub = s; }
            }
            UnityEngine.Debug.Log(string.Format("[Tree] variant {0}: subs={1} -> Wood=Sub{2}", variant, subs, woodSub));
            return woodSub;
        }

        private static Material[] AssignMaterialsPerSubmesh(Mesh mesh, int woodSub, Material woodMat, Material leafMat)
        {
            int subs = Math.Max(1, mesh.subMeshCount);
            var matPerSub = new Material[subs];
            for (int s = 0; s < subs; s++) matPerSub[s] = s == woodSub ? woodMat : leafMat;
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
