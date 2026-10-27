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
        public int FernScatterCount { get; private set; }
        public int GroundCoverCount { get; private set; }
        public int GroundFernCount { get; private set; }
        public int PlantColliderCount { get; private set; }
        public int LianaCount { get; private set; }
        public int DebrisCount { get; private set; }
        public float DebrisMaxSizeMeters { get; private set; }
        public int LogCount { get; private set; }
        public int RockCount { get; private set; }
        public int FrogCount { get; private set; }
        public int YoungConiferCount { get; private set; }
        public int MoosShellCount { get; private set; }
        public int MoosCardCount { get; private set; }

        // WALD6: Vegetation-Konfiguration (Builder copies from WaldLook; defaults = old behaviour).
        public bool giantLeavesOn = true;   // Riesenblaetter in/ut the forest
        public bool treeFernsOn = true;     // Palmen-Baumfarne in/ut
        public float swordFernFactor = 1f;  // Unterholz-Farne haeufigkeit (probability+cap x)
        public float groundFernFactor = 1f; // Grundfarne haeufigkeit (probability x)
        public float fernScatterFactor = 1f; // Farn-Scatter-Patch chance per cell (x)
        public bool youngConifersOn = false; // Jungbaeumchen/Nadelwedel Gebuesch on/off
        public float bushFactor = 1f;       // Busche haeufigkeit (probability+cap x)
        // WALD7: Moos-Konfiguration (Builder copies from WaldLook).
        public bool moosOn = true;          // Moos shells/cards in/ut
        public bool moosShellsOn = false;   // Stamm-Fuss-Huellen AUS: standen als gruene Boxen frei in der Welt (Radius/Position falsch, Alex 2026-10-21); neu bauen
        public bool redwoodsOn = false;     // WALD4: Mammutbaum-Staemme
        public float oldTreeShare = 1f;     // Anteil der alten Scan-Baeume (1 = alle)
        public bool canopyOn = false;       // WALD5: Blaetterdach
        public int RedwoodCount { get; private set; }
        public int CanopyCount { get; private set; }
        /// <summary>KLEE1: Klee-Instanzen gesamt und im 10-m-Umkreis des Spawnpunkts.</summary>
        public int CloverCount { get; private set; }
        public int CloverNearSpawnCount { get; private set; }
        public float moosBaseHeightM = 1.5f; // unterste Stamm-Meterage mit Moos

        /// <summary>WALD7: Set by CreateTree for the LAST created tree (trunk base world pos +
        /// capsule radius), consumed right after CreateTree in the planting loop.</summary>
        private Vector3 _lastTrunkBaseWorld = Vector3.zero;
        private float _lastTrunkRadius = -1f;

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
            t.GetComponent<Terrain>().drawInstanced = false; // Instancing-Varianten fehlen im gebauten Spiel (Boden flach/unsichtbar)
            t.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Ground;
            Terrain terrain = t.GetComponent<Terrain>();
            if (library.terrainMat != null)
            {
                terrain.materialTemplate = library.terrainMat; // sonst Hidden/InternalErrorShader im Player
            }
            // Diagnose (Alex 2026-10-21: in der gebauten Version fehlt der Boden): Terrain-Material und Ebenen ins Log schreiben.
            {
                Material tm = terrain.materialTemplate;
                string layers = "";
                TerrainLayer[] tl = data.terrainLayers;
                for (int li = 0; tl != null && li < tl.Length; li++)
                    layers += (tl[li] == null ? "null" : tl[li].name + "[diff=" + (tl[li].diffuseTexture != null ? tl[li].diffuseTexture.name : "null") + "]") + " ";
                UnityEngine.Debug.Log("[TerrainDiag] materialTemplate=" + (tm == null ? "null" : tm.name + " shader=" + (tm.shader != null ? tm.shader.name : "null") + " supported=" + (tm.shader != null && tm.shader.isSupported))
                    + " layers=" + layers + " drawHeightmap=" + terrain.drawHeightmap + " size=" + data.size + " pipeline="
                    + (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "none"));
            }

            // 3) Wasser (E1: Sumpf + Bach per Seed — nur ueber Grundfeucht Areas,
            //    realistic Shoreline; thin MeshCollider so Spinne can wade on it)
            var waterMesh = BuildWaterMesh(World, size);
            GameObject water = new GameObject("Sumpfwasser");
            water.name = "Sumpfwasser";
            MeshFilter waterMf = water.AddComponent<MeshFilter>();
            waterMf.sharedMesh = waterMesh;
            MeshRenderer renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = library.water;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            water.AddComponent<MeshCollider>().sharedMesh = waterMesh;
            water.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Water;
            water.AddComponent<WaterBody>();

            // 3b) Frosch (T3: EIN Bedaenger per Sumpfzone — lurks on the bank at the water edge)
            var frogRoot = new GameObject("Froesch");
            PlaceFrogs(frogRoot.transform);

            // 4) Bäume
            Transform treesRoot = new GameObject("Bäume").transform;
            // WALD7: ONE shell mesh per tree variant (shared by all instances of that variant).
            var moosShellMesh = moosOn ? JunglePlantFactory.MoosBaseShell(Seed * 31 + 5) : null;
            var moosCardMesh = moosOn ? JunglePlantFactory.MoosCapCards(Seed * 31 + 7) : null;
            GameObject moosRoot = new GameObject("Moos"); // static-batched later, world-unit parent
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

                    if (World.Hash01(gx, gz, 77) >= oldTreeShare) continue; // WALD4: alte Baeume duenner
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

                    // WALD7: Moos-Shell um dem Stamm-Fuss — ONE shared shell-mesh per variant +
                    // ONE shared MAT_Moos (Alpha-Cutout with v-gradient), no material per object.
                    // Base/radius come from CreateTree's planted trunk (R6 capsule data).
                    if (moosOn && moosShellsOn && library.moosMat != null && _lastTrunkRadius > 0f)
                    {
                        float r = _lastTrunkRadius * 1.12f + 0.04f; // hair off the bark
                        GameObject sh = new GameObject("MoosBase");
                        sh.transform.SetParent(moosRoot.transform, false); // unscaled root -> world units
                        sh.transform.position = _lastTrunkBaseWorld + Vector3.up * 0.05f; // into the ground
                        sh.transform.rotation = Quaternion.Euler(0f, rot, 0f);
                        sh.transform.localScale = new Vector3(r, moosBaseHeightM, r);
                        sh.AddComponent<MeshFilter>().sharedMesh = moosShellMesh;
                        sh.AddComponent<MeshRenderer>().sharedMaterial = library.moosMat;
                        MoosShellCount++;
                    }

                    if (!placed.TryGetValue(cell, out List<Vector3> list))
                    {
                        list = new List<Vector3>();
                        placed[cell] = list;
                    }
                    list.Add(flat);
                }
            }

            if (redwoodsOn) BuildRedwoods(terrain, size);
            if (canopyOn) BuildCanopy(size);

            // 5) Unterholz: Büsche, Farne, Schilf (nur Mesh, keine Collider, statisch gebatcht)
            GameObject unterholz = new GameObject("Unterholz");
            // G4: dichtereres Unterholz (Schritt 1.5→1.0 bzw. 1.0→0.6); Zufallsrotation/-skalierung kommen aus PlaceGrid.
            // A1: Busche/Farne in VARIANTS (shrub_02/03/04, fern+nettle). Variant per Cell
            // determinist from Seed (Hash01), Zufalls-Rot/Scale as before.
            BushCount = PlaceGridVariants(unterholz.transform, size, 1.0f,
                VariantMeshes(library.bush, library.bushVariants), VariantMats(library.bushMat, library.bushVariantMats),
                // MODELL8/9: Busch1- und Busch2-Neubau haben 2 Submeshes (Ast/Blatt)
                // -> Zweitmaterial je Variante (Busch1-Ast an 0, Busch2-Ast an 1).
                // MODELL10: Busch3-Neubau (Variante 2) mit eigenem Ast-Material.
                BranchMats(library.buschBranchMat, library.busch2BranchMat,
                    library.busch3BranchMat,
                    library.bushVariants != null ? library.bushVariants.Length : 0),
                (i, j, x, z) => World.Height(x, z) >= JungleWorld.WaterLevel + 0.2f
                    && World.Hash01(i, j, 13) < World.Density(x, z) * 0.6f * bushFactor,
                (i, j) => 0.7f + World.Hash01(i, j, 14) * 0.7f, Mathf.RoundToInt(1500 * bushFactor));
            FernCount = PlaceGrid(unterholz.transform, size, 0.6f, library.fern, library.fernMat,
                (i, j, x, z) => World.Hash01(i, j, 23) < Mathf.Min(1f, World.Density(x, z) * 0.7f * swordFernFactor),
                (i, j) => 0.8f + World.Hash01(i, j, 24) * 0.8f, Mathf.RoundToInt(1500 * swordFernFactor),
                0.5f); // MODELL7: Kletter-Kern nur 0.5 m (bogige Wedel durchkrabbelbar)
            ReedCount = PlaceGrid(unterholz.transform, size, 0.7f, library.reed, library.reedMat,
                (i, j, x, z) => World.Height(x, z) >= JungleWorld.WaterLevel - 0.3f
                    && World.Height(x, z) <= JungleWorld.WaterLevel + 0.2f
                    && World.Hash01(i, j, 33) < 0.9f,
                (i, j) => 0.8f + World.Hash01(i, j, 34) * 0.7f, 1500);

            // Riesenbläter (Blender, 3 Varianten): groß tropische Blattschnitte mit
            // Cutout-Maske und Normal. Im selben statischen Batching wie das restliche Unterholz.
            // WALD6: giantLeavesOn=false -> Riesenblaetter ut dem Waldbereich.
            if (giantLeavesOn && library.giantLeaves != null && library.giantLeafMats != null)
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
                        AddGiantLeaf(unterholz.transform, library.giantLeaves[variant],
                            library.giantLeafMats[variant], library.giantLeafStemMats != null ? library.giantLeafStemMats[variant] : null,
                            new Vector3(x, World.Height(x, z) - 0.05f, z), rot, scale);
                        PlantColliderCount++;
                        giantN++;
                    }
                }
                GiantLeafCount = giantN;
            }

            // WALD6: Schwertfarne in Gruppen an Staemmen entlang: 2-4 per Tree, 0.35-1.25 m um Stamm-Fuss.
            // Inside Unterholz (counted in PlantColliderCount, Static-Batched below).
            for (int tIdx = 0; tIdx < Trees.Count; tIdx++)
            {
                Vector3 tp = Trees[tIdx].position;
                float tx = tp.x, tz = tp.z;
                if (tx < 1f || tx > size - 1f || tz < 1f || tz > size - 1f) continue;
                int per = 2 + Mathf.FloorToInt(World.Hash01(tIdx, 7, 301) * 3f); // 2..4
                for (int k = 0; k < per; k++)
                {
                    float ang = World.Hash01(tIdx, k, 302) * Mathf.PI * 2f;
                    float r = 0.35f + World.Hash01(tIdx, k, 303) * 0.9f; // 0.35..1.25 m
                    float x = tx + Mathf.Cos(ang) * r, z = tz + Mathf.Sin(ang) * r;
                    if (x < 0.5f || x > size - 0.5f || z < 0.5f || z > size - 0.5f) continue;
                    if (NearSpawn(x, z, 1.0f)) continue;
                    if (!IsDryFlat(World, x, z)) continue;
                    float rot = World.Hash01(tIdx, k, 304) * 360f;
                    float scale = 0.8f + World.Hash01(tIdx, k, 305) * 0.9f; // skaliert Gebuesch-Hoehe
                    AddPlant(unterholz.transform, library.fern, library.fernMat,
                        new Vector3(x, World.Height(x, z) - 0.05f, z), rot, scale, 0.5f); // MODELL7: Kern 0.5 m
                    PlantColliderCount++;
                }
            }

            // WALD6: Jungbaeumchen/Nadelwedel als Gebuesch (fedrige Coniferen-Zweige, 0.5-2 m).
            // MODELL8: weiter am Scan-Material bushScanMat (fedrige Einzelnetz-Optik).
            if (youngConifersOn && library.bushScanMat != null)
            {
                var coniferMeshes = new System.Collections.Generic.List<Mesh>(3);
                for (int v = 0; v < 3; v++) coniferMeshes.Add(JunglePlantFactory.YoungConifer(Seed * 17 + v + 1));
                YoungConiferCount = PlaceGridVariants(unterholz.transform, size, 1.0f,
                    coniferMeshes.ToArray(), VariantMats(library.bushScanMat, library.bushVariantMats),
                    null,
                    (i, j, x, z) => World.Height(x, z) >= JungleWorld.WaterLevel + 0.2f
                        && World.Hash01(i, j, 413) < Mathf.Min(1f, World.Density(x, z) * 0.8f * bushFactor),
                    (i, j) => 0.45f + World.Hash01(i, j, 414) * 1.2f, // 0.45..1.65 -> ~0.5-2 m high
                    Mathf.RoundToInt(1500 * bushFactor));
            }
            StaticBatchingUtility.Combine(unterholz);

            // Baumfarne (Blender, 3 Varianten): kletterbarer Stamm (Capsule-Collider) + Blattschnitt-Cutout.
            // Kein statisches Batching: jeder Farn trägt einen eigenen Kletter-Collider.
            // Plus 3 garantierte Exemplare 6–10 m vor dem Spawn, in Blickrichtung der Startkamera.
            // WALD6: treeFernsOn=false -> Palmen-Baumfarne ut dem Waldbereich.
            if (treeFernsOn && library.treeFerns != null && library.treeFernLeafMat != null && library.treeFernStemMat != null)
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

            // 6a) Farn-Scatter-Patch (MODELL6): Einzelwedel in 3 Varianten (fern_wedel.fbx),
            //     245 Patch-Punkte aus Poisson-Disc (fern_patch.txt), Meshes nur referenziert.
            // tilett ueber der Urwald. Per Instance: Zufalls-Cell als Patch-Anker, Y = SampleHeight.
            // XZ come Library-offsets (Baked im FBX), Patch-Footprint = fernScatterPatchSize m.
            if (library.fernScatterMeshes != null && library.fernScatterCount > 0
                && library.fernScatterLeafMat != null && library.fernScatterStemMat != null)
            {
                GameObject fernScatter = new GameObject("Farn-Scatter");
                int scatterN = 0;
                // Patch-Anchor per 24-m-Cell, Zufalls-offset, Dry+above-water pruef, Spawn 3 m free.
                for (int i = 0; i < Mathf.FloorToInt(size / 24f); i++)
                {
                    for (int j = 0; j < Mathf.FloorToInt(size / 24f); j++)
                    {
                        if (World.Hash01(i, j, 101) >= Mathf.Min(1f, World.Density(i * 24f, j * 24f) * fernScatterFactor)) continue; // WALD6: Scatter haeufiger
                        float ax = 12f + i * 24f + (World.Hash01(i, j, 102) - 0.5f) * 8f;
                        float az = 12f + j * 24f + (World.Hash01(i, j, 103) - 0.5f) * 8f;
                        if (!IsDryFlat(World, ax, az)) continue;
                        if (NearSpawn(ax, az, 3f)) continue;
                        float half = library.fernScatterPatchSize * 0.5f;
                        if (ax - half < 1f || ax + half > size - 1f || az - half < 1f || az + half > size - 1f) continue;
                        for (int k = 0; k < library.fernScatterCount && k < library.fernScatterMeshes.Length; k++)
                        {
                            Mesh fm = library.fernScatterMeshes[k];
                            if (fm == null) continue;
                            Vector3 o = library.fernScatterXZ != null && k < library.fernScatterXZ.Length
                                ? library.fernScatterXZ[k] : Vector3.zero;
                            float px = ax + o.x, pz = az + o.z;
                            if (px < 0.5f || px > size - 0.5f || pz < 0.5f || pz > size - 0.5f) continue;
                            GameObject g = new GameObject();
                            g.transform.SetParent(fernScatter.transform, false);
                            // Y per SampleHeight (Terrain-Surface), FBX-Bottom im Mesh y=0 -> sink 0.02 m.
                            float fy = terrain.SampleHeight(new Vector3(px, 0f, pz)) + terrain.transform.position.y - 0.02f;
                            g.transform.position = new Vector3(px, fy, pz);
                            // MODELL6: Einzelwedel stehen nicht lotrecht — Neigung in der Wedel-
                            // ebene (±13 deg Streuung) plus Groessen-Streuung (siehe unten).
                            // Einzelwedel sind flaechiger als der alte Busch-Patch: bewusst
                            // groesser skaliert, damit sich bei 0.42 m Punktabstand ein dichter
                            // Teppich ueberlappt (Deckungsflaeche waechst mit dem Quadrat).
                            const float fernScaleMin = 1.10f;
                            const float fernScaleMax = 1.60f;
                            float yawW = World.Hash01(i, j, 110 + k % 97) * 360f;
                            float leanW = (World.Hash01(i, j, 121 + k % 97) - 0.5f) * 26f;
                            g.transform.rotation = Quaternion.Euler(leanW, yawW, 0f);
                            float scaleW = fernScaleMin
                                + (fernScaleMax - fernScaleMin) * World.Hash01(i, j, 131 + k % 97);
                            g.transform.localScale = Vector3.one * scaleW;
                            g.AddComponent<MeshFilter>().sharedMesh = fm;
                            var r = g.AddComponent<MeshRenderer>();
                            // Submesh-Order measured in LoadFernScatter: leaf index fernScatterLeafSubmesh.
                            int leafIdx = library.fernScatterLeafSubmesh;
                            r.sharedMaterials = new Material[] {
                                leafIdx == 0 ? library.fernScatterLeafMat : library.fernScatterStemMat,
                                leafIdx == 0 ? library.fernScatterStemMat : library.fernScatterLeafMat,
                            };
                            ApplyInstanceTint(r, new Vector3(px, fy, pz), 74);
                            // U4.4: thin CapsuleCollider like AddPlant -> climbable/anchorable,
                            // Plants-Layer. Scale is Vector3.one here so no capsule distortion.
                            Bounds fb = fm.bounds;
                            GameObject capGo = new GameObject("PflanzenCollider");
                            capGo.transform.SetParent(g.transform, false);
                            capGo.layer = GameLayers.Plants;
                            var capCol = capGo.AddComponent<CapsuleCollider>();
                            capCol.direction = 1; // Y-Axis
                            capCol.height = fb.size.y;
                            capCol.radius = Mathf.Clamp(0.25f * Mathf.Min(fb.size.x, fb.size.z), 0.015f, 0.12f);
                            capGo.transform.position = g.transform.TransformPoint(fb.center);
                            AddPlantLod(g);
                            scatterN++;
                        }
                    }
                }
                StaticBatchingUtility.Combine(fernScatter);
                FernScatterCount = scatterN;
                UnityEngine.Debug.Log("[Jungle] Farn-Scatter: " + scatterN + " Instances placed (Y=SampleHeight)");
            }

            // 6c) Grundfarne (U3b): Blender fern_1..5.fbx, 5 Varianten, je 2 Submeshes (Stem+Leaf).
            // Separate root 'Grundfarne' (like Baumfarne/Farn-Scatter), own GroundFernCount.
            // Capsule-Collider per Farn (Plants-Layer, climbable/anchorable) but NOT counted in
            // PlantColliderCount -> keeps the Unterholz-collider assert consistent (Fix-Naechste b).
            if (library.groundFerns != null && library.groundFernLeafMat != null)
            {
                GameObject grundfarne = new GameObject("Grundfarne");
                int gfN = 0;
                int gfMax = Mathf.RoundToInt(160 * groundFernFactor); // WALD6: Grundfarne haeufiger
                int gcells = Mathf.FloorToInt(size / 6f);
                for (int i = 0; i < gcells && gfN < gfMax; i++)
                {
                    for (int j = 0; j < gcells && gfN < gfMax; j++)
                    {
                        float x = 3f + i * 6f + (World.Hash01(i, j, 201) - 0.5f) * 3f;
                        float z = 3f + j * 6f + (World.Hash01(i, j, 202) - 0.5f) * 3f;
                        if (x < 0.5f || x > size - 0.5f || z < 0.5f || z > size - 0.5f) continue;
                        if (NearSpawn(x, z, 1.2f)) continue;
                        if (!IsDryFlat(World, x, z)) continue;
                        if (World.Hash01(i, j, 203) >= Mathf.Min(1f, World.Density(x, z) * 0.45f * groundFernFactor)) continue; // WALD6
                        int variant = Mathf.FloorToInt(World.Hash01(i, j, 204) * (float)library.groundFerns.Length);
                        if (variant < 0 || variant >= library.groundFerns.Length) continue;
                        if (library.groundFerns[variant] == null) continue;
                        float rot = World.Hash01(i, j, 205) * 360f;
                        float scale = 0.7f + World.Hash01(i, j, 206) * 0.6f;
                        AddGroundFern(grundfarne.transform, library.groundFerns[variant],
                            new Vector3(x, World.Height(x, z) - 0.03f, z), rot, scale);
                        gfN++;
                    }
                }
                GroundFernCount = gfN;
                UnityEngine.Debug.Log("[Jungle] Grundfarne: " + gfN + " (separate root, own capsule colliders)");
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

                        // A1: dense meadow pass uses only the grass/kraut species (first 3),
                        // not the nettle/periwinkle bushes.
                        int coverN = Mathf.Min(3, library.groundCover.Length);
                        int variant = Mathf.FloorToInt(World.Hash01(hi, hj, 84) * (float)coverN);
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

            // 6c) KLEE1: Klee-Bodenteppich (NUR HINZUGEFUEGT, alte Bodendecker bleiben unveraendert).
            BuildClover(size);

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
                    float groundY = terrain != null ? terrain.SampleHeight(anchor) + terrain.transform.position.y : World.Height(anchor.x, anchor.z); // gebautes Gelaende, nicht die Rohfunktion (Spawn-Mulde etc.)
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
                var logMesh = library.logs[placedLogs % Math.Min(3, library.logs.Length)];
                float logScale = 0.8f + World.Hash01(0, i, 45) * 0.4f;
                float logRot = World.Hash01(0, i, 44) * 360f;
                GameObject logGo = CreateProp(totholz.transform, logMesh,
                    library.logMat, new Vector3(x, h - 0.05f, z),
                    logRot, logScale, SurfaceType.Wood);
                // WALD7: Moos-Karten on the UPPER side of the Totholz (same shared MAT_Moos).
                // Fix: Karten an der WELT-Huelle des Stamms ausrichten (gleiche Drehung wie der Stamm, Mitte und Oberkante
                // aus den echten Renderer-Bounds), sonst liegen lange Streifen quer und schwebend (gruene Baender).
                if (moosOn && library.moosMat != null && moosCardMesh != null)
                {
                    Bounds wb = logGo.GetComponent<MeshRenderer>().bounds;
                    Bounds lb = logMesh.bounds;
                    GameObject card = new GameObject("MoosCap");
                    card.transform.SetParent(moosRoot.transform, false);
                    card.transform.position = new Vector3(wb.center.x, wb.max.y + 0.02f, wb.center.z);
                    card.transform.rotation = Quaternion.Euler(0f, logRot, 0f);
                    // Karten-Mesh ist etwa 2 Einheiten breit: Halbe Stammgroesse einsetzen, Laenge hoechstens 3 m.
                    float cardX = Mathf.Min(lb.size.x * logScale * 0.5f * 0.55f, 1.5f);
                    float cardZ = Mathf.Min(lb.size.z * logScale * 0.5f * 0.55f, 1.5f);
                    card.transform.localScale = new Vector3(cardX, 1f, cardZ);
                    card.AddComponent<MeshFilter>().sharedMesh = moosCardMesh;
                    card.AddComponent<MeshRenderer>().sharedMaterial = library.moosMat;
                    MoosCardCount++;
                }
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
                + BushCount + " Büsche, " + FernCount + " Farne, " + ReedCount + " Schilf, "
                + GiantLeafCount + " Riesenbläter, " + TreeFernCount + " Baumfarne, "
                + YoungConiferCount + " Jungbaumchen, " + GroundCoverCount + " Bodendecker, "
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
            PlantPredicate ok, PlantScale scale, int maxCount, float coreHeight = 0f)
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
                    AddPlant(parent, mesh, mat, new Vector3(x, World.Height(x, z) - 0.05f, z), rot, scale(i, j), coreHeight);
                    PlantColliderCount++;
                    n++;
                }
            }
            return n;
        }

        /// <summary>Rastergröße auf Salzpaar abbilden: 1.5 → 11, 1.0 → 21, 0.7 → 31.</summary>
        private static int SaltA(float step) => step >= 1.4f ? 11 : step >= 0.9f ? 21 : 31;

        /// <summary>A1: Mesh-/Material-Arrays (primary + optional variant arrays) combine, nulls dropped.</summary>
        private static Mesh[] VariantMeshes(Mesh primary, Mesh[] extra)
        {
            var list = new System.Collections.Generic.List<Mesh>();
            if (primary != null) list.Add(primary);
            if (extra != null)
                foreach (var m in extra)
                    if (m != null) list.Add(m);
            return list.ToArray();
        }

        private static Material[] VariantMats(Material primary, Material[] extra)
        {
            var list = new System.Collections.Generic.List<Material>();
            if (primary != null) list.Add(primary);
            if (extra != null)
                foreach (var m in extra)
                    if (m != null) list.Add(m);
            return list.ToArray();
        }

        /// <summary>MODELL8: Zweitmaterial-Liste indexgleich zu VariantMats: nur fuer die
        /// erste Variante (Busch1-Neubau mit 2 Submeshes) gesetzt, fuer die Scan-Varianten null.</summary>
        private static Material[] BranchMats(Material branchMat, int extraVariants)
        {
            if (branchMat == null) return null;
            var mats = new Material[1 + Mathf.Max(0, extraVariants)];
            mats[0] = branchMat;
            return mats;
        }

        /// <summary>MODELL9: Zweitmaterial-Liste mit Eintraegen pro Variante (Busch1-Ast
        /// an 0, Busch2-Ast an 1); null-Eintrag = nur ein Material. Laengenanpassung
        /// wie bei VariantMats/VariantMeshes.</summary>
        private static Material[] BranchMats(Material branchMat1, Material branchMat2, int extraVariants)
        {
            if (branchMat1 == null && branchMat2 == null) return null;
            var mats = new Material[1 + Mathf.Max(0, extraVariants)];
            mats[0] = branchMat1;
            if (mats.Length > 1) mats[1] = branchMat2;
            return mats;
        }

        /// <summary>MODELL10: Zweitmaterial-Liste fuer drei Neubau-Varianten
        /// (Busch1-Ast an 0, Busch2-Ast an 1, Busch3-Ast an 2).</summary>
        private static Material[] BranchMats(Material branchMat1, Material branchMat2,
            Material branchMat3, int extraVariants)
        {
            if (branchMat1 == null && branchMat2 == null && branchMat3 == null) return null;
            var mats = new Material[1 + Mathf.Max(0, extraVariants)];
            mats[0] = branchMat1;
            if (mats.Length > 1) mats[1] = branchMat2;
            if (mats.Length > 2) mats[2] = branchMat3;
            return mats;
        }

        /// <summary>A1: PlaceGrid but variant per Cell, determinist from Seed (Hash01 salt +5).
        /// MODELL8: secondMats (optional, indexgleich zu mats) = Zweitmaterial fuer Meshes mit
        /// 2 Submeshes (Busch1: Ast/Blatt); null-Eintraege = nur ein Material.</summary>
        private int PlaceGridVariants(Transform parent, float size, float step, Mesh[] meshes, Material[] mats,
            Material[] secondMats, PlantPredicate ok, PlantScale scale, int maxCount)
        {
            int n = 0;
            int variants = Mathf.Min(meshes.Length, mats.Length);
            if (variants == 0) return 0;
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
                    int v = Mathf.FloorToInt(World.Hash01(i, j, SaltA(step) + 5) * (float)variants);
                    if (v < 0) v = 0;
                    if (v >= variants) v = variants - 1;
                    float rot = World.Hash01(i, j, SaltA(step) + 4) * 360f;
                    Material second = secondMats != null && v < secondMats.Length ? secondMats[v] : null;
                    AddPlant(parent, meshes[v], mats[v], new Vector3(x, World.Height(x, z) - 0.05f, z), rot, scale(i, j), 0f, second);
                    PlantColliderCount++;
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// Per-Instance-Farbvariation (F1: Urwald wirkt ausgewaschen und eintoenig).
        /// Deterministisch aus dem Seed (World.Hash01) und der Instance-Position. KEINE
        /// neuen Materialinstanzen per Objekt: ein MaterialPropertyBlock per Renderer tints
        /// HDRP-Lit _BaseColor, so Static Batching / Draw Calls stay in den Grueben.
        /// Jitter: Farbton +/- 0.03, Saettigung +/- 0.10, Helligkeit +/- 0.10 (HSV).
        /// </summary>
        private void ApplyInstanceTint(MeshRenderer r, Vector3 worldPos, int salt)
        {
            if (r == null) return;
            int gx = Mathf.FloorToInt(worldPos.x);
            int gz = Mathf.FloorToInt(worldPos.z);
            float hRaw = World.Hash01(gx, gz, salt);
            float sRaw = World.Hash01(gx, gz, salt + 1);
            float vRaw = World.Hash01(gx, gz, salt + 2);
            Color c = ComputeTint(hRaw, sRaw, vRaw);
            MaterialPropertyBlock mp = new MaterialPropertyBlock();
            mp.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mp);
        }

        /// <summary>
        /// Deterministic per-instance tint as a MULTIPLICATIVE near-white factor.
        /// Pure math, testable without the scene: hRaw drives hue (spread), sRaw a small
        /// saturation blend (0..20%), vRaw a brightness factor (+/- 0.10). Result always
        /// in [0..1] and never darker than ~0.8 so the Albedo texture stays legible.
        /// </summary>
        public static Color ComputeTint(float hRaw, float sRaw, float vRaw)
        {
            float hue = hRaw;
            float satBlend = Mathf.Clamp(0.10f + sRaw * 0.10f, 0f, 0.2f);
            float valFactor = 1.0f + (vRaw - 0.5f) * 2f * 0.10f;
            Color chroma = HsvToColor(hue, 1f, 1f);
            Color c = new Color(
                (1f + (chroma.r - 1f) * satBlend) * valFactor,
                (1f + (chroma.g - 1f) * satBlend) * valFactor,
                (1f + (chroma.b - 1f) * satBlend) * valFactor,
                1f);
            c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b);
            return c;
        }

        /// <summary>HSV (h in [0..1]) to RGB-Color. Simple, no external deps.</summary>
        private static Color HsvToColor(float h, float s, float v)
        {
            h = h - Mathf.Floor(h);
            int i = Mathf.FloorToInt(h * 6f);
            float f = h * 6f - i;
            float p = v * (1f - s);
            float q = v * (1f - s * f);
            float t = v * (1f - s * (1f - f));
            switch (i % 6)
            {
                case 0: return new Color(v, t, p, 1f);
                case 1: return new Color(q, v, p, 1f);
                case 2: return new Color(p, v, t, 1f);
                case 3: return new Color(p, q, v, 1f);
                case 4: return new Color(t, p, v, 1f);
                default: return new Color(v, p, q, 1f);
            }
        }

        /// <summary>Mesh-Objekt mit Kind „PflanzenCollider“ erzeugen (Spinne/Netze können andocken).</summary>
        private GameObject AddPlant(Transform parent, Mesh mesh, Material mat, Vector3 pos, float rotY, float scale, float coreHeight = 0f, Material secondMat = null)
        {
            GameObject g = new GameObject();
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            g.transform.localScale = Vector3.one * scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var plantRenderer = g.AddComponent<MeshRenderer>();
            // MODELL8: Meshes mit 2 Submeshes (Busch1-Neubau) bekommen Blatt- und
            // Ast-Material; welcher Submesh das Blatt ist, wurde im Builder gemessen.
            if (secondMat != null && mesh != null && mesh.subMeshCount >= 2)
            {
                Material[] pm = new Material[mesh.subMeshCount];
                for (int mi = 0; mi < pm.Length; mi++) pm[mi] = mi == library.buschLeafSubmesh ? mat : secondMat;
                plantRenderer.sharedMaterials = pm;
            }
            else
            {
                plantRenderer.sharedMaterial = mat;
            }
            ApplyInstanceTint(plantRenderer, pos, 71);

            // Dünnkapsel in der Mesh-Mitte: klein genug, dass die Spinne nicht feststeckt,
            // groß genug, dass Netz-Anker und Beine andocken können.
            GameObject c = new GameObject("PflanzenCollider");
            c.transform.SetParent(g.transform, false);
            c.layer = GameLayers.Plants;
            Bounds b = mesh.bounds;
            Vector3 s = g.transform.localScale;
            var cc = c.AddComponent<CapsuleCollider>();
            cc.direction = 1; // Y-Achse
            // MODELL7: coreHeight > 0 (Schwertfarn) -> duenner Kletter-Kern am
            // Stand fuss hoch; die bogigen Wedel bleiben durchkrabbelbar, die
            // Spinne klettert nicht bis ueber 1 m und kippt (B2-Regression).
            float capH = coreHeight > 0f ? Mathf.Min(b.size.y, coreHeight) : b.size.y;
            cc.height = capH * s.y;
            cc.radius = Mathf.Clamp(0.25f * Mathf.Min(b.size.x * s.x, b.size.z * s.z), 0.015f, 0.12f);
            float capCenterY = coreHeight > 0f ? b.min.y + capH * 0.5f : b.center.y;
            c.transform.position = g.transform.TransformPoint(new Vector3(b.center.x, capCenterY, b.center.z));
            AddPlantLod(g);
            return g;
        }

        /// <summary>
        /// P1: LODGroup (single Level-0 + RecalculateBounds). Below the screen-size Threshold
        /// the group renders nothing -> distant plants get culled (Culling-Distanz). No reduced
        /// meshes exist for these variants, so Level 0 is the full mesh and the LODGroup acts as a
        /// distance-cull. Colliders stay active regardless of LOD (LOD only affects rendering).
        /// </summary>
        private void AddPlantLod(GameObject instance)
        {
            var r = instance.GetComponent<MeshRenderer>();
            if (r == null) return;
            var lod = instance.AddComponent<LODGroup>();
            lod.SetLODs(new LOD[] { new LOD(VegetationLodScreenThreshold, new MeshRenderer[] { r }) });
            lod.fadeMode = LODFadeMode.CrossFade;
            lod.RecalculateBounds();
        }

        /// <summary>Screen-Sizes-Threshold (0..1) at which a plant LODGroup stops rendering.</summary>
        private const float VegetationLodScreenThreshold = 0.03f;

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
            var lianaRenderer = vis.AddComponent<MeshRenderer>();
            lianaRenderer.sharedMaterial = mat;
            AddPlantLod(vis);

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

        /// <summary>Grundfarn (U3b): 2 Submeshes per Mesh (Stem + Leaf), order = groundFernLeafSubmesh.
        /// Real geometry -> opaque mats. Thin CapsuleCollider (Plants-Layer) so the spider can climb/anchor,
        /// but NOT counted in PlantColliderCount (separate 'Grundfarne' root).</summary>
        private void AddGroundFern(Transform parent, Mesh mesh, Vector3 pos, float rotY, float scale)
        {
            GameObject g = new GameObject();
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            g.transform.localScale = Vector3.one * scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            // TEXTUR2: Stiel+Blatt sind jetzt ein Mesh mit 2 Submeshes (Slot 0 Stiel, 1 Blatt).
            int leafIdx = library.groundFernLeafSubmesh;
            if (mesh.subMeshCount >= 2 && library.groundFernStemMat != null)
            {
                Material[] mats = new Material[mesh.subMeshCount];
                for (int mi = 0; mi < mats.Length; mi++)
                    mats[mi] = mi == leafIdx ? library.groundFernLeafMat : library.groundFernStemMat;
                r.sharedMaterials = mats;
            }
            else
            {
                r.sharedMaterials = new Material[] { library.groundFernLeafMat };
            }
            ApplyInstanceTint(r, pos, 72);

            Bounds b = mesh.bounds;
            GameObject c = new GameObject("GrundfarnCollider");
            c.transform.SetParent(g.transform, false);
            c.layer = GameLayers.Plants;
            var cc = c.AddComponent<CapsuleCollider>();
            cc.direction = 1; // Y-Achse
            cc.height = b.size.y * scale;
            cc.radius = Mathf.Clamp(0.25f * Mathf.Min(b.size.x, b.size.z) * scale, 0.02f, 0.14f);
            c.transform.position = g.transform.TransformPoint(b.center);
            AddPlantLod(g);
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
            ApplyInstanceTint(r, pos, 73);

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
            AddPlantLod(g);
        }

        /// <summary>
        /// M1b: Riesenblatt with 2 Submeshes (Blatt-Cutout + Stamm-Bark). The FBX has 2 Material-Slots,
        /// so a single shared material (old AddPlant path) painted the STEM with the leaf texture ->
        /// "trunk merges into foliage". Here materials are assigned in SUBMESH order: leaf at
        /// library.giantLeafLeafSubmesh (measured at build), stem on the other. Falls back to a
        /// single-material AddPlant if stem/leaf-submesh data is missing. Thin capsule collider like AddPlant.
        /// </summary>
        private GameObject AddGiantLeaf(Transform parent, Mesh mesh, Material leafMat, Material stemMat, Vector3 pos, float rotY, float scale)
        {
            int leafSub = library.giantLeafLeafSubmesh;
            if (stemMat == null || mesh.subMeshCount < 2)
                return AddPlant(parent, mesh, leafMat, pos, rotY, scale);

            // sharedMaterials index i = submesh i. Put leaf at leafSub, stem on the remaining slot(s).
            var mats = new Material[Math.Max(1, mesh.subMeshCount)];
            for (int slot = 0; slot < mats.Length; slot++)
                mats[slot] = (slot == leafSub) ? leafMat : stemMat;

            GameObject g = new GameObject();
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            g.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            g.transform.localScale = Vector3.one * scale;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            ApplyInstanceTint(r, pos, 71);

            GameObject c = new GameObject("PflanzenCollider");
            c.transform.SetParent(g.transform, false);
            c.layer = GameLayers.Plants;
            var cc = c.AddComponent<CapsuleCollider>();
            Bounds b = mesh.bounds;
            Vector3 sc = g.transform.localScale;
            cc.direction = 1; // Y-Achse
            cc.height = b.size.y * sc.y;
            cc.radius = Mathf.Clamp(0.25f * Mathf.Min(b.size.x * sc.x, b.size.z * sc.z), 0.015f, 0.12f);
            c.transform.position = g.transform.TransformPoint(b.center);
            AddPlantLod(g);
            return g;
        }

        /// <summary>Stamm/Stein mit Convex-Collider und SurfaceTag erzeugen.</summary>
        private static GameObject CreateProp(Transform parent, Mesh mesh, Material mat, Vector3 pos, float rotY, float scale, SurfaceType surface)
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
            return g;
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
        /// <summary>WALD4: Mammutbaum-Staemme, ca. ein Stamm pro 56 m2, Kapsel-Collider bis in die Erde.</summary>
        private void BuildRedwoods(Terrain terrain, float size)
        {
            if (library.redwoodMat == null) return;
            var meshes = new Mesh[3];
            float[] heights = { 48f, 55f, 62f };
            float[] radii = { 0.9f, 1.1f, 1.3f };
            for (int i = 0; i < 3; i++) meshes[i] = RedwoodFactory.Trunk(Seed * 13 + i, heights[i], radii[i], 0.45f);
            Transform root = new GameObject("Mammutbaeume").transform;
            const float step = 7.5f;
            for (float gx = 4f; gx < size - 3f; gx += step)
            {
                for (float gz = 4f; gz < size - 3f; gz += step)
                {
                    int ix = Mathf.RoundToInt(gx), iz = Mathf.RoundToInt(gz);
                    float x = gx + (World.Hash01(ix, iz, 91) - 0.5f) * step * 0.7f;
                    float z = gz + (World.Hash01(ix, iz, 92) - 0.5f) * step * 0.7f;
                    if (x < 2f || x > size - 2f || z < 2f || z > size - 2f) continue;
                    if (NearSpawn(x, z, 3.0f)) continue;
                    float h = World.Height(x, z);
                    if (h < JungleWorld.WaterLevel + 0.4f) continue;
                    float e = 0.8f;
                    float dh = Mathf.Max(Mathf.Abs(World.Height(x + e, z) - h), Mathf.Abs(World.Height(x, z + e) - h));
                    if (Mathf.Atan2(dh, e) * Mathf.Rad2Deg >= 30f) continue;
                    int v = (int)(World.Hash01(ix, iz, 93) * 3f) % 3;
                    float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
                    var go = new GameObject("Mammutbaum");
                    go.transform.SetParent(root, false);
                    go.transform.position = new Vector3(x, y, z);
                    go.transform.rotation = Quaternion.Euler(0f, World.Hash01(ix, iz, 94) * 360f, 0f);
                    go.AddComponent<MeshFilter>().sharedMesh = meshes[v];
                    go.AddComponent<MeshRenderer>().sharedMaterial = library.redwoodMat;
                    go.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Wood;
                    // Gestapelte Kapseln (je ca. 6 m) statt einer riesigen: Radius folgt dem Stammverlauf (kein unsichtbarer Wand-Kegel oben).
                    const float seg = 6f;
                    for (float y0 = -4f; y0 < heights[v]; y0 += seg)
                    {
                        float yc = Mathf.Min(y0 + seg * 0.5f, heights[v]);
                        var capGo = new GameObject("TrunkCapsule");
                        capGo.transform.SetParent(go.transform, false);
                        capGo.transform.localPosition = new Vector3(0f, yc, 0f);
                        var cap = capGo.AddComponent<CapsuleCollider>();
                        cap.radius = RedwoodFactory.RadiusAt(Mathf.Max(0f, yc), heights[v], radii[v], 0.45f) * 0.92f;
                        cap.height = seg + 1f;
                        cap.direction = 1;
                    }
                    RedwoodCount++;
                }
            }
            UnityEngine.Debug.Log("[Redwood] " + RedwoodCount + " Staemme");
        }

        /// <summary>KLEE1: Klee-Bodenteppich um den Spawnpunkt — Kern 0..6 m dicht (Step 0.16 m),
        /// Ring 6..14 m locker (Step 0.32 m, ~60 %). Kein Klee in Wasser/Sumpf. 5..12 cm hoch,
        /// keine Collider, pro 8-m-Block zu EINEM Mesh vereint (wie BuildCanopy).</summary>
        private void BuildClover(float size)
        {
            CloverCount = 0;
            CloverNearSpawnCount = 0;
            if (library.cloverMat == null) return;

            var variants = new Mesh[6];
            for (int i = 0; i < variants.Length; i++) variants[i] = CloverFactory.Clover(Seed * 17 + i);

            Transform root = new GameObject("Klee").transform;
            var blocks = new Dictionary<Vector2Int, List<CombineInstance>>();
            const float coreR = 6f, outerR = 14f;

            for (int gi = -88; gi <= 88; gi++)
            {
                float gx = _spawnXZ.x + gi * 0.16f;
                for (int gj = -88; gj <= 88; gj++)
                {
                    float gz = _spawnXZ.z + gj * 0.16f;
                    float dx = gx - _spawnXZ.x, dz = gz - _spawnXZ.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 > outerR * outerR) continue;
                    bool core = d2 <= coreR * coreR;
                    // Ring: halbes Raster (nur gerade Zellen) plus Hash-Duenneung; Kern bleibt vollstaendig.
                    if (!core)
                    {
                        if ((gi & 1) != 0 || (gj & 1) != 0) continue;
                        int hi = Mathf.RoundToInt(gx * 100f), hj = Mathf.RoundToInt(gz * 100f);
                        if (World.Hash01(hi, hj, 131) < 0.4f) continue;
                    }
                    float x = gx + (World.Hash01(Mathf.RoundToInt(gx * 512), Mathf.RoundToInt(gz * 512), 132) - 0.5f) * 0.12f;
                    float z = gz + (World.Hash01(Mathf.RoundToInt(gx * 512), Mathf.RoundToInt(gz * 512), 133) - 0.5f) * 0.12f;
                    if (x < 0.5f || x > size - 0.5f || z < 0.5f || z > size - 0.5f) continue;
                    if (!IsDryFlat(World, x, z)) continue;
                    if (World.Height(x, z) < JungleWorld.WaterLevel + 0.1f) continue;

                    int hi2 = Mathf.RoundToInt(x * 100f), hj2 = Mathf.RoundToInt(z * 100f);
                    // KLEE2: Pflanzen 8..13 cm hoch, Blaetter 4..9 cm (L=0.5..0.7 Mesh-Einheit * scale).
                    float scale = 0.08f + World.Hash01(hi2, hj2, 134) * 0.05f; // 8..13 cm
                    var ci = new CombineInstance
                    {
                        mesh = variants[Mathf.FloorToInt(World.Hash01(hi2, hj2, 135) * variants.Length) % variants.Length],
                        transform = Matrix4x4.TRS(
                            new Vector3(x, World.Height(x, z) - 0.01f, z),
                            Quaternion.Euler(0f, World.Hash01(hi2, hj2, 136) * 360f, 0f),
                            Vector3.one * scale)
                    };
                    var key = new Vector2Int(Mathf.FloorToInt(x / 8f), Mathf.FloorToInt(z / 8f));
                    if (!blocks.TryGetValue(key, out var list)) { list = new List<CombineInstance>(); blocks[key] = list; }
                    list.Add(ci);
                    CloverCount++;
                    if (d2 <= 100f) CloverNearSpawnCount++;
                }
            }

            foreach (var kv in blocks)
            {
                var mesh = new Mesh { name = "KleeBlock", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject("KleeBlock");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = library.cloverMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            UnityEngine.Debug.Log(string.Format("[KLEE1] Klee-Instanzen im 10-m-Umkreis={0} -> Kriterium >= 3000 -> {1}; gesamt={2} in {3} Bloecken",
                CloverNearSpawnCount, CloverNearSpawnCount >= 3000 ? "OK" : "NOK", CloverCount, blocks.Count));
        }

        /// <summary>WALD5: Blaetterdach in 38..46 m, Buescheln pro 6-m-Zelle (ca. 10 % Luecken), pro 32-m-Block zu EINEM Mesh vereint.</summary>
        private void BuildCanopy(float size)
        {
            if (library.canopyMat == null) return;
            var clusters = new Mesh[4];
            for (int i = 0; i < clusters.Length; i++) clusters[i] = RedwoodFactory.CanopyCluster(Seed * 7 + i);
            Transform root = new GameObject("Blaetterdach").transform;
            var blocks = new Dictionary<Vector2Int, List<CombineInstance>>();
            for (float gx = -6f; gx < size + 6f; gx += 6f)
            {
                for (float gz = -6f; gz < size + 6f; gz += 6f)
                {
                    int ix = Mathf.RoundToInt(gx), iz = Mathf.RoundToInt(gz);
                    if (World.Hash01(ix, iz, 95) < 0.10f) continue;
                    float x = gx + (World.Hash01(ix, iz, 96) - 0.5f) * 4f;
                    float z = gz + (World.Hash01(ix, iz, 97) - 0.5f) * 4f;
                    float y = 38f + World.Hash01(ix, iz, 98) * 8f;
                    var ci = new CombineInstance
                    {
                        mesh = clusters[(int)(World.Hash01(ix, iz, 99) * clusters.Length) % clusters.Length],
                        transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, World.Hash01(ix, iz, 100) * 360f, 0f), Vector3.one * (0.9f + World.Hash01(ix, iz, 101) * 0.5f))
                    };
                    var key = new Vector2Int(Mathf.FloorToInt(x / 32f), Mathf.FloorToInt(z / 32f));
                    if (!blocks.TryGetValue(key, out var list)) { list = new List<CombineInstance>(); blocks[key] = list; }
                    list.Add(ci);
                    CanopyCount++;
                }
            }
            foreach (var kv in blocks)
            {
                var mesh = new Mesh { name = "RW_CanopyBlock", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject("DachBlock");
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = library.canopyMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            UnityEngine.Debug.Log("[Redwood] Dach " + CanopyCount + " Buescheln in " + blocks.Count + " Bloecken");
        }

        private readonly Dictionary<int, Vector3> _footprints = new Dictionary<int, Vector3>();

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
            // Rohslotnames (LOD0 MeshRenderer.sharedMaterials[i].name, '|' = submesh) for
            // name-based Wood/Leaf classification. LOD1 slot order identical (verified).
            string phSlotNames = library.phTreeSlotNames != null && variant < library.phTreeSlotNames.Length ? library.phTreeSlotNames[variant] : null;
            GameObject tree = new GameObject("PH-Tree");
            tree.transform.SetParent(parent, false);
            Terrain terr = Terrain.activeTerrain;
            float groundAtPos = pos.y;
            if (terr != null)
            {
                Vector3 terrPos = terr.transform.position;
                groundAtPos = terr.SampleHeight(pos) + terrPos.y;
                // Root contract: SampleHeight + 0.15 (VegetationContactTest); planting shift
                // happens on the CHILDREN below (raycast fix), not here.
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
            r0.sharedMaterials = AssignMaterialsPerSubmesh(lod0, woodSub, woodMat, leafMat, phSlotNames);
            ApplyInstanceTint(r0, new Vector3(pos.x, pos.y, pos.z), 75);

            GameObject lod1Node = null;
            if (lod1 != lod0)
            {
                lod1Node = new GameObject("TreeLOD1");
                lod1Node.transform.SetParent(tree.transform, false);
                lod1Node.transform.localPosition = Vector3.zero;
                lod1Node.AddComponent<MeshFilter>().sharedMesh = lod1;
                r1 = lod1Node.AddComponent<MeshRenderer>();
                r1.sharedMaterials = AssignMaterialsPerSubmesh(lod1, PickWoodSubmesh(lod1, variant, scale), woodMat, leafMat, phSlotNames);
                ApplyInstanceTint(r1, new Vector3(pos.x, pos.y, pos.z), 75);
            }

            // Collider on the LOD0-Mesh: LOD1 lacks lower-trunk triangles (measured in TreesTests),
            // so blocking the Spider needs the full scan mesh (concave).
            GameObject colNode = new GameObject("TreeCollider");
            colNode.transform.SetParent(tree.transform, false);
            var mc = colNode.AddComponent<MeshCollider>();
            mc.sharedMesh = lod0;
            mc.convex = false;

            // Planting fix (runtime): measure the TRUE visual bottom from the LOD0 MESH
            // VERTICES (not renderer bounds, which are stale at creation time; not a
            // raycast foot, which grabbed crown columns on slopes and off-terrain edges)
            // and shift the VISUAL+COLLIDER children so the mesh bottom sits 0.03 m BELOW
            // the terrain sample under the tree root. Root stays at SampleHeight + 0.15
            // (VegetationContactTest contract). Works for every species incl. jacaranda
            // (FBX pivot high above wood bottom) and keeps liana crowns consistent.
            if (terr != null && lod0 != null)
            {
                // TRUE visual bottom via the MeshCollider's world-space AABB (mc.bounds),
                // NOT lod0.vertices (PH-FBX isReadable=false -> runtime error spam) and
                // NOT renderer bounds (stale at creation time, floated 2.5 m unmeasured).
                float wy0 = tree.transform.position.y + lod0.bounds.min.y * scale; // Mesh-Bounds statt mc.bounds (im Player ohne lesbares Mesh ungueltig -> Baeume schwebten)
                float centerGround = terr.SampleHeight(tree.transform.position) + terr.transform.position.y;
                float shift = (centerGround - 0.03f) - wy0;
                float meshWorldH = lod0.bounds.size.y * scale;
                if (Mathf.Abs(shift) <= meshWorldH + 5f && shift != 0f)
                {
                    float dropLocal = shift / scale; // local units
                    lod0Node.transform.localPosition += new Vector3(0f, dropLocal, 0f);
                    colNode.transform.localPosition += new Vector3(0f, dropLocal, 0f);
                    if (lod1Node != null)
                        lod1Node.transform.localPosition += new Vector3(0f, dropLocal, 0f);
                }
            }

            // R6: convex Trunk-Blocker. Physics.SphereCast (the movement brake + climb
            // trigger in SpiderMovement) does NOT hit the concave TreeCollider (convex=false),
            // so the Spider walked straight through trunks (minNear 0.032, maxToward > trunkDist).
            // A CapsuleCollider along the trunk axis gives a convex blocker on the SAME layer the
            // sensor/movement mask queries (layer 0). Child of colNode -> inherits the planting
            // dropLocal shift automatically, so it stays glued to the planted trunk.
            _lastTrunkBaseWorld = Vector3.zero; // WALD7: reset per tree, set below when wood valid
            _lastTrunkRadius = -1f;
            if (woodSub >= 0)
            {
                Bounds tb = lod0.GetSubMesh(woodSub).bounds;
                GameObject trunk = new GameObject("TrunkCapsule");
                trunk.transform.SetParent(colNode.transform, false);
                // Fussabdruck aus den echten Stamm-Vertices der untersten 2 Welt-Meter (Mesh ist lesbar): Mitte und Radius.
                Vector3 footC = tb.center; float footR = Mathf.Min(tb.extents.x, tb.extents.z);
                if (!_footprints.TryGetValue(variant, out Vector3 fp))
                {
                    var vs = new List<Vector3>();
                    lod0.GetVertices(vs);
                    var idx = lod0.GetIndices(woodSub);
                    float yLimit = tb.min.y + 2f / Mathf.Max(scale, 0.01f);
                    Vector2 sum = Vector2.zero; int n = 0;
                    foreach (int ii in idx) { var vv = vs[ii]; if (vv.y <= yLimit) { sum += new Vector2(vv.x, vv.z); n++; } }
                    if (n > 8)
                    {
                        Vector2 c = sum / n; float rmax = 0f;
                        foreach (int ii in idx) { var vv = vs[ii]; if (vv.y <= yLimit) rmax = Mathf.Max(rmax, (new Vector2(vv.x, vv.z) - c).magnitude); }
                        fp = new Vector3(c.x, 0f, c.y); fp.y = rmax;
                    }
                    else fp = new Vector3(tb.center.x, Mathf.Min(tb.extents.x, tb.extents.z), tb.center.z);
                    _footprints[variant] = fp;
                }
                footC = new Vector3(fp.x, tb.center.y, fp.z); footR = fp.y;
                trunk.transform.localPosition = footC;
                // Echter Fussabdruck, mindestens aber die Bounds-Huelle bis 2.5 m Welt-Radius (die Gehtests brauchen den Abstand zum Stamm; Aeste gehen nicht ein).
                float tradius = Mathf.Max(footR, Mathf.Min(Mathf.Min(tb.extents.x, tb.extents.z), 2.5f / Mathf.Max(scale, 0.01f)));
                tradius = Mathf.Min(tradius, 3.5f / Mathf.Max(scale, 0.01f));
                float theight = tb.extents.y * 2f;
                CapsuleCollider cap = trunk.AddComponent<CapsuleCollider>();
                cap.radius = tradius;
                cap.height = Mathf.Max(theight, tradius * 2f + 0.01f);
                // Untere Kuppel unter das Gelaende legen: sonst kann die Spinne am Stammfuss (Kapsel nur so hoch wie der nackte Stamm) in die Kuppel laufen.
                cap.height += tradius; cap.center = new Vector3(0f, -tradius * 0.5f, 0f);
                cap.direction = 1; // Y-Axis (Stemachse)
                UnityEngine.Debug.Log("[R6] TrunkCapsule variant=" + variant + " radius->" + tradius.ToString("F3")
                    + " height->" + theight.ToString("F3") + " convex->true");
                // WALD7: Moos-Shell-Anker = real planted trunk base (colNode already carries
                // the planting shift) and the capsule radius.
                _lastTrunkBaseWorld = colNode.transform.TransformPoint(new Vector3(tb.center.x, tb.min.y, tb.center.z));
                // Fix (Alex 2026-10-21): tradius ist die kleinere Halbachse der GANZEN Holz-Submesh (Aeste eingerechnet, mehrere Meter),
                // nicht der Stamm am Fuss. Die Huelle bekommt daher nur einen Bruchteil, begrenzt auf 0.35..1.2 m, sonst entstehen
                // riesige gruene Ringe ("Baender").
                _lastTrunkRadius = Mathf.Clamp(tradius * 0.12f, 0.35f, 1.2f);
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

        /// <summary>
        /// Wood/Leaf per Submesh classified by the FBX SLOT NAME (ground truth; '|' separated,
        /// index = submesh; Unity sorts slots alphabetically so index order is NOT a contract).
        /// trunk|bark|wood|branch(es)|twig -> Wood, leaf(s) -> Leaf. There is NO separate branch
        /// material, so branches MUST take the Wood (opaque bark) shader; the old single-woodSub
        /// rule gave every other submesh the alpha-clipped Leaf shader (jacaranda: brown crown
        /// + invisible trunk/branches). woodSub is only a fallback HINT for slots whose name
        /// matches neither keyword (island_* bare species slug) or when the name list is
        /// missing/stale (length != subMeshCount).
        /// </summary>
        private static Material[] AssignMaterialsPerSubmesh(Mesh mesh, int woodSub, Material woodMat, Material leafMat, string slotNamesRaw)
        {
            int subs = Math.Max(1, mesh.subMeshCount);
            var matPerSub = new Material[subs];
            string[] slotNames = null;
            if (slotNamesRaw != null && slotNamesRaw.Length > 0)
                slotNames = slotNamesRaw.Split('|');
            if (slotNames == null || slotNames.Length != subs)
                slotNames = null; // name list missing or stale vs mesh: fall back to woodSub hint
            for (int s = 0; s < subs; s++)
            {
                string n = slotNames != null ? slotNames[s].ToLower() : "";
                bool isLeaf = n.Contains("leaf") || n.Contains("leaves") || n.Contains("foliage") || n.Contains("frond");
                bool isWood = n.Contains("trunk") || n.Contains("bark") || n.Contains("wood") || n.Contains("branch") || n.Contains("twig");
                if (isLeaf) matPerSub[s] = leafMat;
                else if (isWood) matPerSub[s] = woodMat;
                else matPerSub[s] = s == woodSub ? woodMat : leafMat;
            }
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

        /// <summary>
        /// E1: Builds the water surface as a flat mesh at WaterLevel over WET ground only
        /// (Sumpfe + creek per Seed). 1-m grid cells: a quad wherever Height < WaterLevel.
        /// The patchwork gives a realistic, irregular Shoreline (instead of one big Quad)
        /// and lets the MeshCollider mark exactly the flooded area for the Spinne.
        /// </summary>
        private static Mesh BuildWaterMesh(JungleWorld world, float size)
        {
            // Q5: fein 0.5-m-Gitter + Deterministic-Jitter -> weicher, organic Shoreline
            // statt 1-m-Stair-Stepping mit harter polygonales Rand (warum: kantig Wasser).
            const float cell = 0.5f;
            int n = (int)(size / cell);
            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var tris = new System.Collections.Generic.List<int>();
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            // Gitter-Eckpoints shared between quads, with deterministic jitter (hash of
            // grid coords) so the shoreline looks organic instead of stair-stepped.
            var gridIdx = new System.Collections.Generic.List<int>();
            for (int i = 0; i < (n + 1) * (n + 1); i++) gridIdx.Add(-1);

            for (int gx = 0; gx < n; gx++)
            {
                for (int gz = 0; gz < n; gz++)
                {
                    float x0 = gx * cell, z0 = gz * cell;
                    if (world.Height(x0 + cell * 0.5f, z0 + cell * 0.5f) >= JungleWorld.WaterLevel) continue;

                    int[] corner = new int[4];
                    for (int c = 0; c < 4; c++)
                    {
                        int px = gx + ((c >> 1) & 1);
                        int pz = gz + (c & 1);
                        int gi = pz * (n + 1) + px;
                        int vi = gridIdx[gi];
                        if (vi < 0)
                        {
                            float jx = world.Hash01(px, pz, 11) * 2f - 1f;
                            float jz = world.Hash01(px, pz, 23) * 2f - 1f;
                            float cx = px * cell + jx * cell * 0.28f;
                            float cz = pz * cell + jz * cell * 0.28f;
                            verts.Add(new Vector3(cx, JungleWorld.WaterLevel, cz));
                            uvs.Add(new Vector2(cx - center.x, cz - center.y));
                            vi = verts.Count - 1;
                            gridIdx[gi] = vi;
                        }
                        corner[c] = vi;
                    }
                    // Quad mit Eckpoints c0=(gx,gz) c1=(gx,gz+cell) c2=(gx+cell,gz) c3=(gx+cell,gz+cell);
                    // Winding wie original (oben zeigt nach +Y).
                    tris.Add(corner[0]); tris.Add(corner[3]); tris.Add(corner[2]);
                    tris.Add(corner[0]); tris.Add(corner[1]); tris.Add(corner[3]);
                }
            }

            var mesh = new Mesh();
            mesh.name = "Sumpfwasser";
            if (verts.Count == 0) return mesh;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// T3: Place one frog per water body (Sumpf/Bach), on the bank at the water edge.
        /// Built from primitives: green body + two eyes, no colliders (insects/dangers are
        /// collider-free). Spot selection is deterministic over the Seed via FrogModel.
        /// </summary>
        private void PlaceFrogs(Transform parent)
        {
            var spots = FrogModel.FindFrogSpots(World);
            Material body = Spinnenspiel.Rendering.PipelineMaterials.Lit(new Color(0.18f, 0.45f, 0.2f));
            Material eye = Spinnenspiel.Rendering.PipelineMaterials.Lit(new Color(0.9f, 0.85f, 0.2f));

            int placedFrogs = 0;
            foreach (Vector3 spot in spots)
            {
                GameObject frog = new GameObject("Frosch");
                frog.transform.SetParent(parent, false);
                frog.transform.position = spot + Vector3.up * 0.02f;
                frog.AddComponent<Frog>();

                GameObject koerper = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                koerper.name = "Koerper";
                DropCollider(koerper);
                koerper.transform.SetParent(frog.transform, false);
                koerper.transform.position = frog.transform.position;
                koerper.transform.localScale = new Vector3(0.12f, 0.07f, 0.16f);
                koerper.GetComponent<MeshRenderer>().sharedMaterial = body;

                GameObject eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eyeL.name = "Eye_L";
                DropCollider(eyeL);
                eyeL.transform.SetParent(frog.transform, false);
                eyeL.transform.position = frog.transform.position + new Vector3(-0.04f, 0.05f, 0.06f);
                eyeL.transform.localScale = Vector3.one * 0.03f;
                eyeL.GetComponent<MeshRenderer>().sharedMaterial = eye;

                GameObject eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eyeR.name = "Eye_R";
                DropCollider(eyeR);
                eyeR.transform.SetParent(frog.transform, false);
                eyeR.transform.position = frog.transform.position + new Vector3(0.04f, 0.05f, 0.06f);
                eyeR.transform.localScale = Vector3.one * 0.03f;
                eyeR.GetComponent<MeshRenderer>().sharedMaterial = eye;

                placedFrogs++;
            }

            FrogCount = placedFrogs;
        }

        private static void DropCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 400, 24), "Welt-Seed: " + Seed + "   (F5 = neue Welt)");
        }
    }
}
