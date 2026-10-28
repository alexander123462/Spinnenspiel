using Spinnenspiel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.Universal;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Baut die Urwald-Szene "Urwald" (Milestone 10.1b):
    /// Licht, Weltgenerator (Terrain, Sumpfwasser, Bäume, Spinne aus Seed), Kamera, Spinne, Insekten.
    /// Das Gelände erzeugt die JungleWorldGenerator zur Laufzeit – kein statischer Terrain-Builder.
    /// </summary>
    public static class UrwaldSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Urwald.unity";
        private const string LibraryPath = "Assets/_Project/Art/Generated/JungleAssetLibrary.asset";
        private const string MeshFolder = "Assets/_Project/Art/Generated/Meshes/";
        private const string MatFolder = "Assets/_Project/Art/Generated/Materials/";
        private const string LayerFolder = "Assets/_Project/Art/Generated/Terrain/";

        // Q4 Schilf: gellich-gruen (HSV H~70°, S 0.55, V 0.42 -> RGB unten) und matte.
        private const float ReedBaseR = 0.388f;
        private const float ReedBaseG = 0.42f;
        private const float ReedBaseB = 0.189f;
        private const float ReedSmoothness = 0.12f;

        [MenuItem("Spinnenspiel/Build/Urwald")]
        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            WorldLightingBuilder.Build();

            // Asset-Bibliothek für den Laufzeit-Generator anlegen/ersetzen.
            AssetUtil.EnsureFolder("Assets/_Project/Art/Generated");
            JungleAssetLibrary lib = AssetDatabase.LoadAssetAtPath<JungleAssetLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = CreateAsset<JungleAssetLibrary>(LibraryPath, "JungleAssetLibrary");
            }

            lib.trunks = LoadMeshes(new[] { "VM_Trunk0", "VM_Trunk1", "VM_Trunk2" });
            lib.crownsNear = LoadMeshes(new[] { "VM_Crown0", "VM_Crown1", "VM_Crown2" });
            lib.crownsFar = LoadMeshes(new[] { "VM_CrownFar0", "VM_CrownFar1", "VM_CrownFar2" });
            lib.bush = LoadMesh("VM_Bush");
            lib.fern = LoadMesh("VM_Fern");
            lib.reed = LoadMesh("VM_Reed");
            // MODELL11: Blender-Schilf (Horst: 6 Knoten-Halme, ueberhaengende Klingen,
            // 3 Rohrkolben, 1032 Dreiecke, 1.60 m, UV-Atlas) ersetzt den prozeduralen
            // GrassClump. Quelle ArtSource/Schilf/schilf_neu.fbx; fehlt sie, bleibt VM_Reed.
            Mesh schilfNeu = LoadBlenderSchilf();
            if (schilfNeu != null) lib.reed = schilfNeu;
            // MODELL16: Blender-Totholz (ArtSource/Totholz) ersetzt M_Log0..2; fehlt das
            // FBX, bleibt der prozeduralue Fallback aus MeshFactory aktiv.
            Mesh[] totholzNeu = LoadTotholzMeshes();
            if (totholzNeu != null) lib.logs = totholzNeu;
            lib.logHulls = LoadTotholzHulls();
            // MODELL17: Blender-Steine (ArtSource/Steine, 5 Varianten je ~1400 Dreiecke,
            // gebrochene Facetten, Schichtung, Moos als Vertexfarbe) ersetzen M_Rock0..4;
            // fehlt ein FBX, bleibt der prozedurale Fallback aus MeshFactory aktiv.
            Mesh[] steineNeu = LoadSteineMeshes();
            if (steineNeu != null) lib.rocks = steineNeu;
            else lib.rocks = LoadMeshes(new[] { "M_Rock0", "M_Rock1", "M_Rock2", "M_Rock3", "M_Rock4" });

            lib.leaves = LoadMaterial("MAT_Leaves");
            lib.reedMat = ReedMaterial(LoadMaterial("MAT_Reed"));
            lib.water = LoadMaterial("MAT_WaterPond");

            // Scan-Assets (Poly Haven) ersetzen die prozeduralen Klötzchen; fehlt einer, bleibt der bisherige Wert.
            lib.bark = ScanBark(LoadMaterial("MAT_TreeBark"));
            lib.logMat = ScanMaterial("Assets/_Project/Art/Scans/Materials/M_dead_tree_trunk.mat", LoadMaterial("MAT_Bark"));
            // MODELL16: Borke/Holz-Materialien (HDRP/Lit mit Albedo+Normal aus ArtSource-Texturen).
            lib.logMats = CreateTotholzMats();
            lib.rockMat = ScanMaterial("Assets/_Project/Art/Scans/Materials/M_mossy_rock.mat", LoadMaterial("MAT_Rock"));
            // WALD7: ONE shared Moos-Material (Alpha-Cutout with v-gradient) for all shells/cards.
            lib.moosMat = MoosMaterial();
            lib.terrainMat = TerrainMaterial();
            lib.redwoodMat = RedwoodMaterial();
            lib.canopyMat = CanopyMaterial();
            lib.cloverMat = CloverMaterial();
            // MODELL7: Schwertfarn-Neubau (Rosette aus 8 bogigen Wedeln mit je
            // 25 Fiederpaaren, 2964 Dreiecke, 0.87 m) ersetzt den Scan fern_02;
            // fehlt das FBX, bleibt der Scan als Fallback stehen.
            Mesh farnTropisch = LoadBlenderFarnTropisch();
            if (farnTropisch != null) lib.fern = farnTropisch;
            else lib.fern = ScanMesh("Assets/_Project/Art/Scans/Models/fern_02/fern_02_1k.fbx", LoadMesh("VM_Fern"));
            // G4: Scan-Farn/-Strauch wirken mitteleuropäisch-braun → grün getönte Kopien (Originale bleiben unverändert).
            lib.fernMat = TropischMaterial(ScanMaterial("Assets/_Project/Art/Scans/Materials/M_fern_02.mat", LoadMaterial("MAT_Fern")), "MAT_FernTropisch");
            if (farnTropisch != null && lib.fernMat != null) FernTropischOpaque(lib.fernMat);
            // MODELL8: Busch1-Neubau (Blender: 5 Haupttriebe mit Sekundaerzweigen,
            // 172 Blatt-Geometrien, 5156 Dreiecke, 0.90 m, 2 Slots Ast/Blatt) ersetzt
            // den Scan shrub_02; fehlt das FBX, bleibt der Scan als Fallback stehen.
            int buschLeafSub;
            Mesh buschNeu = LoadBlenderBusch1(out buschLeafSub);
            if (buschNeu != null) lib.bush = buschNeu;
            else lib.bush = ScanMesh("Assets/_Project/Art/Scans/Models/shrub_02/shrub_02_2k.fbx", LoadMesh("VM_Bush"));
            // Busch1-Materialien (Blatt + Ast) aus eigener Atlas-Textur; der Scan-Name
            // MAT_BushTropisch bleibt erhalten und dient weiter den Jungnadelbaeumen.
            Material buschLeafMat = CreateBusch1LeafMat();
            lib.buschBranchMat = CreateBusch1BranchMat();
            lib.buschLeafSubmesh = buschLeafSub;
            // Jungnadelbaeume (WALD6) bleiben am gruen getoenten Scan-Material.
            lib.bushScanMat = TropischMaterial(ScanMaterial("Assets/_Project/Art/Scans/Materials/M_shrub_02.mat", LoadMaterial("MAT_Grass")), "MAT_BushTropisch");
            if (buschNeu != null && buschLeafMat != null) lib.bushMat = buschLeafMat;
            else lib.bushMat = lib.bushScanMat;
            // MODELL9: Busch2-Neubau (Blender: 6 Haupttriebe mit Sekundaer- und
            // Tertiaerzweigen, 150 ovale Blaetter als echte Geometrie, 5552 Dreiecke,
            // 1.07 m, 2 Slots Ast/Blatt) ersetzt den Scan shrub_03 als Variante 0;
            // fehlt das FBX, bleibt der Scan als Fallback stehen.
            Mesh busch2Neu = LoadBlenderBusch(out int busch2LeafSub,
                "Assets/_Project/Art/Plants/Busch2/Busch2.fbx", "ArtSource/Busch2/busch2_neu.fbx", "Busch2");
            Material busch2LeafMat = CreateBuschAtlasMat("MAT_Busch2_Leaf",
                "Assets/_Project/Art/Plants/Busch2/Textures/", "Busch2_Albedo.png", "Busch2_Normal.png", 0.15f);
            lib.busch2BranchMat = CreateBuschAtlasMat("MAT_Busch2_Branch",
                "Assets/_Project/Art/Plants/Busch2/Textures/", "Busch2_Albedo.png", "Busch2_Normal.png", 0.2f);
            // A1: extra Busche-Varianten (MODELL9: Busch2-Neubau statt shrub_03;
            // MODELL10: Busch3-Neubau statt shrub_04).
            Mesh busch3Neu = LoadBlenderBusch(out int busch3LeafSub,
                "Assets/_Project/Art/Plants/Busch3/Busch3.fbx", "ArtSource/Busch3/busch3_neu.fbx", "Busch3");
            Material busch3LeafMat = CreateBuschAtlasMat("MAT_Busch3_Leaf",
                "Assets/_Project/Art/Plants/Busch3/Textures/", "Busch3_Albedo.png", "Busch3_Normal.png", 0.15f);
            lib.busch3BranchMat = CreateBuschAtlasMat("MAT_Busch3_Branch",
                "Assets/_Project/Art/Plants/Busch3/Textures/", "Busch3_Albedo.png", "Busch3_Normal.png", 0.2f);
            lib.bushVariants = LoadBushVariantMeshes(busch2Neu, busch3Neu);
            lib.bushVariantMats = CreateBushVariantMats(busch2LeafMat, busch3LeafMat);
            if (busch2Neu == null || busch2LeafMat == null)
                Debug.LogWarning("[Urwald] MODELL9: Busch2-Neubau unvollstaendig, Variante 0 bleibt Scan shrub_03.");
            else if (busch2LeafSub != lib.buschLeafSubmesh)
                Debug.Log("[Urwald] Busch2 Blatt-Submesh " + busch2LeafSub + " (AddPlant nutzt buschLeafSubmesh "
                          + lib.buschLeafSubmesh + ").");
            if (busch3Neu == null || busch3LeafMat == null)
                Debug.LogWarning("[Urwald] MODELL10: Busch3-Neubau unvollstaendig, Variante bleibt Scan shrub_04.");
            else if (busch3LeafSub != lib.buschLeafSubmesh)
                Debug.Log("[Urwald] Busch3 Blatt-Submesh " + busch3LeafSub + " (AddPlant nutzt buschLeafSubmesh "
                          + lib.buschLeafSubmesh + ").");

            // Riesenbläter (Blender, 3 Varianten): tropische Pflanze mit Blattschnitt-Mask und Normal.
            LoadRiesenblatt(lib);

            // Baumfarne (Blender, 3 Varianten): 2 Submeshes pro FBX (0 = Stamm, 1 = Blatt).
            lib.treeFerns = LoadBaumfarnMeshes();
            var baumfarnMats = CreateBaumfarnMats();
            lib.treeFernLeafMat = baumfarnMats[0];
            lib.treeFernStemMat = baumfarnMats[1];

            // U4: Farn-Scatter-Patch (Blender scatter.py, 245 Instances per 8x8 m Patch).
            LoadFernScatter(lib);

            // U3b: Grundfarne (Blender fern_1..5.fbx, 5 Varianten) in die Library.
            LoadGroundFerns(lib);

            // Bodendecker (Scan-Gras/Kraut): nur Optik, keine Collider; um die Spinne herum.
            // A1: more species (nettle, periwinkle) -> variant per Instance via Hash01 below.
            LoadGroundCover(lib);

            // Poly-Haven-Baume (B3): FBX-Meshes (LOD0/1) + HDRP-Materials + Scales in die Library.
            LoadPHTrees(lib);

            // Boden: Scan-TerrainLayer (TileSize 2 m, ca. 1 Kachel pro 2 m); Fallback: prozedurale Layer.
            // Reihenfolge: Earth, Grass, Mud, Rock.
            lib.terrainLayers = ScanLayers(new[]
            {
                "Assets/_Project/Art/Scans/Layers/TL_forest_ground_04.terrainlayer",
                "Assets/_Project/Art/Scans/Layers/TL_sparse_grass.terrainlayer",
                "Assets/_Project/Art/Scans/Layers/TL_brown_mud_leaves_01.terrainlayer",
                "Assets/_Project/Art/Scans/Layers/TL_rocky_trail.terrainlayer",
            }, new[] { "TL_Earth", "TL_Grass", "TL_Mud", "TL_Rock" });

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();

            GameObject generator = new GameObject("Weltgenerator");
            var gen = generator.AddComponent<JungleWorldGenerator>();
            gen.library = lib;
            // WALD6: Vegetation-Konfiguration from WaldLook (ONE place), runtime reads the copy.
            gen.giantLeavesOn = WaldLook.GiantLeavesOn;
            gen.treeFernsOn = WaldLook.TreeFernsOn;
            gen.swordFernFactor = WaldLook.SwordFernFactor;
            gen.groundFernFactor = WaldLook.GroundFernFactor;
            gen.fernScatterFactor = WaldLook.FernScatterFactor;
            gen.youngConifersOn = WaldLook.YoungConifersOn;
            gen.bushFactor = WaldLook.BushFactor;
            // WALD7: Moos config (one shared MAT_Moos for all shells/cards).
            gen.moosOn = WaldLook.MoosOn;
            gen.moosBaseHeightM = WaldLook.MoosBaseHeightM;
            gen.redwoodsOn = WaldLook.RedwoodsOn;
            gen.oldTreeShare = WaldLook.OldTreeShare;
            gen.canopyOn = WaldLook.CanopyOn;

            new GameObject("Wind").AddComponent<WindController>();
            // Weather: deterministic dry/rain cycle driving the audio crossfade.
            new GameObject("Weather").AddComponent<WeatherController>();
            // Z4 Tag/Nacht: Sun rotates slowly (one day = 20 min), ambient light adapts.
            GameObject dayNight = new GameObject("Tag/Nacht");
            DayNightCycle dayCycle = dayNight.AddComponent<DayNightCycle>();
            dayCycle.sun = Object.FindFirstObjectByType<Light>();
            // WALD2a3: daytime sun color from WaldLook (runtime cycle drives the sun each frame).
            dayCycle.daySunColor = WaldLook.SunColor;
            // FARBE1: night brightness from WaldLook (J5 night Mittel was 5 < 10).
            dayCycle.moonLux = WaldLook.MoonIntensityLux;
            // T7 Morgennebel: the cycle drives the fog volume built by WorldLightingBuilder.
            dayCycle.fogVolumeComponent = WorldLightingBuilder.LastBuiltFog;
            // FARBE1a: the cycle drives the sky brightness (day factor) and the tests pin the
            // exposure via the wired Exposure component.
            dayCycle.skyVolume = WorldLightingBuilder.LastBuiltSky;
            dayCycle.exposureVolume = WorldLightingBuilder.LastBuiltExposure;
            new GameObject("Sky Ambient").AddComponent<SkyAmbientUpdater>();
            new GameObject("Jungle Ambience").AddComponent<JungleAmbientAudio>();
            new GameObject("Debug Overlay").AddComponent<DebugOverlay>();

            Camera cam = CameraBuilder.Create();
            UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.fieldOfView = 60f;
            cam.farClipPlane = 300f;

            Vector3 spawn = new Vector3(48f, 20f, 48f);
            GameObject spider = SpiderBuilder.Create(spawn, cam.transform);

            // S5 Tagesbilanz: the sunrise event of the cycle opens the balance window on the
            // spider; bird pre-warnings count as the biggest danger of the day.
            DayBalance dayBalance = spider.GetComponent<Spinnenspiel.DayBalance>();
            if (dayBalance != null)
            {
                dayBalance.SetCycle(dayCycle);
                BirdThreat birdThreat = spider.GetComponent<Spinnenspiel.BirdThreat>();
                if (birdThreat != null)
                {
                    if (birdThreat.WarnFired == null)
                    {
                        birdThreat.WarnFired = new UnityEvent();
                    }
                    birdThreat.WarnFired.AddListener(dayBalance.OnWarning);
                }
            }
            // S8 Tagesziele: the same sunrise event rolls the goals of the new day.
            Spinnenspiel.DailyGoals dailyGoals = spider.GetComponent<Spinnenspiel.DailyGoals>();
            if (dailyGoals != null)
            {
                dailyGoals.SetCycle(dayCycle);
            }

            EffectsBuilder.Build(spider);
            SetLayerRecursive(spider, 2); // "Ignore Raycast": Kamera- und Beinstrahlen ignorieren die Spinne

            // Insekten 1: Spawner mit Vorlage (deaktivierte Fliege), Zentrum = Spinne.
            GameObject insekten = new GameObject("Insekten");
            InsectSpawner insektenSpawner = insekten.AddComponent<InsectSpawner>();
            insektenSpawner.count = 8;
            insektenSpawner.center = spider.transform;
            insektenSpawner.cycle = dayCycle; // J5: Flies only during the day.
            GameObject vorlage = InsectBuilder.CreateFly();
            vorlage.name = "Vorlage";
            vorlage.transform.SetParent(insekten.transform, false);
            vorlage.SetActive(false);
            insektenSpawner.template = vorlage;

            // S2: Muecke-Vorlage → der Tag-Spawner mischt Fliegen und Muecken (~40 % Muecken).
            GameObject mueckeVorlage = InsectBuilder.CreateMosquito();
            if (mueckeVorlage != null)
            {
                mueckeVorlage.name = "Vorlage_Muecke";
                mueckeVorlage.transform.SetParent(insekten.transform, false);
                mueckeVorlage.SetActive(false);
                insektenSpawner.mosquitoTemplate = mueckeVorlage;
            }

            // T1: Kaefer-Vorlage → der Tag-Spawner mischt rare Kaefer in (10 %).
            GameObject kaeferVorlage = InsectBuilder.CreateBeetle();
            if (kaeferVorlage != null)
            {
                kaeferVorlage.name = "Vorlage_Kaefer";
                kaeferVorlage.transform.SetParent(insekten.transform, false);
                kaeferVorlage.SetActive(false);
                insektenSpawner.beetleTemplate = kaeferVorlage;
            }

            // T2: Libelle-Vorlage → der Tag-Spawner mischt sehr rare Libelles in (3 %).
            GameObject libelleVorlage = InsectBuilder.CreateLibelle();
            if (libelleVorlage != null)
            {
                libelleVorlage.name = "Vorlage_Libelle";
                libelleVorlage.transform.SetParent(insekten.transform, false);
                libelleVorlage.SetActive(false);
                insektenSpawner.libelleTemplate = libelleVorlage;
            }

            GameObject insekten2 = new GameObject("Insekten_Motte");
            InsectSpawner motteSpawner = insekten2.AddComponent<InsectSpawner>();
            motteSpawner.count = 4;
            motteSpawner.center = spider.transform;
            motteSpawner.cycle = dayCycle; // J5: Moths only at night.
            motteSpawner.activeAtNight = true;
            GameObject motteVorlage = InsectBuilder.CreateMoth();
            if (motteVorlage != null)
            {
                motteVorlage.name = "Vorlage_Motte";
                motteVorlage.transform.SetParent(insekten2.transform, false);
                motteVorlage.SetActive(false);
                motteSpawner.template = motteVorlage;
            }

            CameraBuilder.Bind(cam, spider.transform);
            cam.transform.position = spawn + new Vector3(0f, 0.35f, -0.95f);
            cam.transform.LookAt(spawn + Vector3.up * 0.1f);

            // Beine schon im Editor in Ruhepose bringen (sonst erst beim ersten Frame im Play-Modus).
            Physics.SyncTransforms();
            foreach (SpiderLeg leg in spider.GetComponentsInChildren<SpiderLeg>())
            {
                leg.PoseAtRest();
            }

            AssetUtil.EnsureFolder("Assets/_Project/Scenes");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);

            // Vorhandene Build-Szenen behalten, Urwald nur ergänzen, falls noch nicht drin.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                scenes.Add(existing);
            }
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Spinnenspiel] Urwald gebaut: " + ScenePath);
        }

        private static T CreateAsset<T>(string path, string label) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Mesh LoadMesh(string name)
        {
            string path = MeshFolder + name + ".asset";
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("[Urwald] Mesh fehlt: " + path);
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static Mesh[] LoadMeshes(string[] names)
        {
            Mesh[] result = new Mesh[names.Length];
            for (int i = 0; i < names.Length; i++)
                result[i] = LoadMesh(names[i]);
            return result;
        }

        private static Material LoadMaterial(string name)
        {
            string path = MatFolder + name + ".mat";
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError("[Urwald] Material fehlt: " + path);
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static TerrainLayer[] LoadLayers(string[] names)
        {
            TerrainLayer[] result = new TerrainLayer[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string path = LayerFolder + names[i] + ".terrainlayer";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogError("[Urwald] TerrainLayer fehlt: " + path);
                    continue;
                }
                result[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            }
            return result;
        }

        /// <summary>Kopie der Scan-Rinde unter Art/Generated mit Tiling gegen Streckung (Scan-Material bleibt unverändert).</summary>
        private static Material ScanBark(Material fallback)
        {
            const string src = "Assets/_Project/Art/Scans/Materials/M_pine_bark.mat";
            if (!System.IO.File.Exists(src))
            {
                Debug.LogWarning("[Urwald] Scan-Material fehlt, prozedurale Rinde bleibt: " + src);
                return fallback;
            }
            string copyPath = MatFolder + "MAT_ScanBark.mat";
            Material scan = AssetDatabase.LoadAssetAtPath<Material>(src);
            Material copy = AssetDatabase.LoadAssetAtPath<Material>(copyPath);
            if (copy == null)
            {
                copy = new Material(scan) { name = "MAT_ScanBark" };
                AssetDatabase.CreateAsset(copy, copyPath);
            }
            copy.CopyPropertiesFromMaterial(scan);
            copy.mainTextureScale = new Vector2(1f, 0.35f); // Rinde gröber: weniger Wiederholungen, längere Fasern
            if (copy.HasProperty("_NormalMap") && copy.GetTexture("_NormalMap") != null)
                copy.SetFloat("_NormalScale", 1.5f);
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();
            return copy;
        }

        /// <summary>
        /// Riesenbläter (Blender, 3 Varianten). Je FBX 2 Material-Slots -> 2 Submeshes: Blatt (Cutout)
        /// + Stamm (opaque Bark). Submesh-Order measured via GetSubMesh(i).bounds (Blatt = broader crown,
        /// XZ-extents), NOT a Blender contract. Fills lib.giantLeaves / giantLeafMats (Blatt) /
        /// giantLeafStemMats (Stamm) / giantLeafLeafSubmesh.
        /// </summary>
        private static void LoadRiesenblatt(JungleAssetLibrary lib)
        {
            const string folder = "Assets/_Project/Art/Plants/Riesenblatt/";
            const string tex = "Assets/_Project/Art/Plants/Riesenblatt/Textures/";
            var meshes = new Mesh[3];
            var leafMats = new Material[3];
            var stemMats = new Material[3];
            int measuredLeafSubmesh = 1;

            for (int i = 0; i < 3; i++)
            {
                string path = folder + "Riesenblatt_" + (i + 1) + ".fbx";
                // MODELL13: Variante 2+ hat eigene Texturen (Riesenblatt2_*), Variante 1
                // behaelt die alten Namen bei. So aendert ein Asset nie die Optik der anderen.
                string texTag = (i == 0) ? "Riesenblatt_" : ("Riesenblatt" + (i + 1) + "_");
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[Urwald] Riesenblatt-FBX fehlt: " + path);
                    continue;
                }
                // MODELL12: M12-Test liest UV und Submesh-Grenzen am Asset.
                ModelImporter rim = AssetImporter.GetAtPath(path) as ModelImporter;
                if (rim != null && !rim.isReadable)
                {
                    rim.isReadable = true;
                    rim.SaveAndReimport();
                }
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is Mesh mesh && mesh.subMeshCount > 0)
                    {
                        meshes[i] = mesh;
                        break;
                    }
                }
                if (meshes[i] == null)
                {
                    Debug.LogWarning("[Urwald] Kein Mesh im Riesenblatt: " + path);
                    continue;
                }

                // Measure submesh order on each variant: Blatt = broader crown (larger XZ-extents),
                // Stamm = schmal. Log so the CI run shows the measured ground truth.
                if (meshes[i].subMeshCount >= 2)
                {
                    Bounds b0 = meshes[i].GetSubMesh(0).bounds;
                    Bounds b1 = meshes[i].GetSubMesh(1).bounds;
                    float ex0 = b0.extents.x * b0.extents.z;
                    float ex1 = b1.extents.x * b1.extents.z;
                    int leafIdx = ex1 > ex0 ? 1 : 0;
                    if (i == 0) measuredLeafSubmesh = leafIdx;
                    Debug.Log("[Urwald] Riesenblatt " + (i + 1) + ": subMeshCount " + meshes[i].subMeshCount
                        + ", Blatt-Submesh measured = " + leafIdx + " (XZ " + ex0 + " vs " + ex1 + ")");
                }
                else
                {
                    Debug.Log("[Urwald] Riesenblatt " + (i + 1) + ": only " + meshes[i].subMeshCount
                        + " Submesh -> single leaf material, no stem split");
                }

                // Blatt-Material (Cutout) — re-use/create MAT_Riesenblatt_<n>.
                {
                    string matPath = MatFolder + "MAT_Riesenblatt_" + (i + 1) + ".mat";
                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat == null)
                    {
                        Shader hdrpLit = Shader.Find("HDRP/Lit");
                        if (hdrpLit == null)
                            throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");
                        mat = new Material(hdrpLit);
                        mat.name = "MAT_Riesenblatt_" + (i + 1);
                        AssetDatabase.CreateAsset(mat, matPath);
                    }
                    mat.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + texTag + "Leaf_Albedo.png"));
                    mat.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + texTag + "Leaf_Mask.png"));
                    mat.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + texTag + "Leaf_Normal.png"));
                    mat.SetFloat("_SurfaceType", 1f); // Cutout (Blattschnitt)
                    mat.SetFloat("_AlphaCutoffEnable", 1f);
                    mat.SetFloat("_AlphaCutoff", 0.5f);
                    mat.SetFloat("_DoubleSidedEnable", 1f);
                    mat.SetFloat("_DoubleSidedNormalMode", 2f); // Mirror
                    mat.SetFloat("_Smoothness", 0.15f);
                    mat.SetFloat("_Metallic", 0f);
                    HDMaterial.ValidateMaterial(mat);
                    EditorUtility.SetDirty(mat);
                    leafMats[i] = mat;
                }

                // Stamm-Material (opaque Bark) — MAT_Riesenblatt_<n>_Stem.
                {
                    string stemPath = MatFolder + "MAT_Riesenblatt_" + (i + 1) + "_Stem.mat";
                    Material stem = AssetDatabase.LoadAssetAtPath<Material>(stemPath);
                    if (stem == null)
                    {
                        Shader hdrpLit = Shader.Find("HDRP/Lit");
                        if (hdrpLit == null)
                            throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");
                        stem = new Material(hdrpLit);
                        stem.name = "MAT_Riesenblatt_" + (i + 1) + "_Stem";
                        AssetDatabase.CreateAsset(stem, stemPath);
                    }
                    stem.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + texTag + "Stem_Albedo.png"));
                    stem.SetFloat("_SurfaceType", 0f); // Opaque
                    stem.SetFloat("_AlphaCutoffEnable", 0f);
                    stem.SetFloat("_DoubleSidedEnable", 0f);
                    stem.SetFloat("_Smoothness", 0.2f);
                    stem.SetFloat("_Metallic", 0f);
                    HDMaterial.ValidateMaterial(stem);
                    EditorUtility.SetDirty(stem);
                    stemMats[i] = stem;
                }
            }

            lib.giantLeaves = meshes;
            lib.giantLeafMats = leafMats;
            lib.giantLeafStemMats = stemMats;
            lib.giantLeafLeafSubmesh = measuredLeafSubmesh;
            AssetDatabase.SaveAssets();
            Debug.Log("[Urwald] Riesenblatt: " + meshes.Length + " Varianten (Blatt-Cutout + Stamm-opaque, 2 Submeshes), Blatt-Submesh=" + measuredLeafSubmesh);
        }

        /// <summary>
        /// HDRP-Lit-Materialien fü die Riesenbläter: Albedo, Blattschnitt-Maske und Normal aus dem
        /// Blender-Export, Opaque mit Cutout. Werden nur angelegt, falls noch nicht vorhanden.
        /// </summary>
        private static Material[] CreateRiesenblattMats()
        {
            const string tex = "Assets/_Project/Art/Plants/Riesenblatt/Textures/";
            var result = new Material[3];
            for (int i = 0; i < 3; i++)
            {
                string path = MatFolder + "MAT_Riesenblatt_" + (i + 1) + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    Shader hdrpLit = Shader.Find("HDRP/Lit");
                    if (hdrpLit == null)
                    {
                        throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");
                    }
                    mat = new Material(hdrpLit);
                    mat.name = "MAT_Riesenblatt_" + (i + 1);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Riesenblatt_Leaf_Albedo.png"));
                mat.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Riesenblatt_Leaf_Mask.png"));
                mat.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Riesenblatt_Leaf_Normal.png"));
                mat.SetFloat("_SurfaceType", 1f); // Cutout (Blattschnitt)
                mat.SetFloat("_AlphaCutoffEnable", 1f);
                mat.SetFloat("_AlphaCutoff", 0.5f);
                mat.SetFloat("_Smoothness", 0.35f);
                mat.color = Color.white;
                EditorUtility.SetDirty(mat);
                result[i] = mat;
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        private static Mesh[] LoadBaumfarnMeshes()
        {
            const string folder = "Assets/_Project/Art/Plants/Baumfarn/";
            var result = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                string path = folder + "Baumfarn_" + (i + 1) + ".fbx";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[Urwald] Baumfarn-FBX fehlt: " + path);
                    continue;
                }
                // MODELL15: Read/Write fuer dieses Mesh aktivieren (Dreiecke/Submeshes im M15-Test).
                ModelImporter bimp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (bimp != null && !bimp.isReadable)
                {
                    bimp.isReadable = true;
                    bimp.SaveAndReimport();
                }
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is Mesh mesh)
                    {
                        result[i] = mesh;
                        Debug.Log("[Urwald] Baumfarn subMeshCount " + (i + 1) + ": " + mesh.subMeshCount
                            + " (erwartet 2: 0 = Stamm, 1 = Blatt)");
                        break;
                    }
                }
                if (result[i] == null)
                    Debug.LogWarning("[Urwald] Kein Mesh im Baumfarn: " + path);
            }
            return result;
        }

        /// <summary>
        /// HDRP-Lit-Materialien für die Baumfarne: Blatt (Albedo/Mask/Normal, Cutout 0.5, doppelseitig)
        /// und Stamm (Stamm-Albedo, Smoothness 0.2). Werden nur angelegt, falls noch nicht vorhanden.
        /// </summary>
        private static Material[] CreateBaumfarnMats()
        {
            const string tex = "Assets/_Project/Art/Plants/Baumfarn/Textures/";
            Shader hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null)
                throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");

            Material leaf = CreateBaumfarnMat("MAT_Baumfarn_Leaf", hdrpLit,
                tex + "Baumfarn_Leaf_Albedo.png", tex + "Baumfarn_Leaf_Mask.png", tex + "Baumfarn_Leaf_Normal.png");
            // Fiederblaetter sind echte Geometrie -> opak, kein Alpha-Cutout (Cutout schnitt die Wedel weg)
            leaf.SetFloat("_SurfaceType", 0f);
            leaf.SetFloat("_AlphaCutoffEnable", 0f);
            leaf.SetFloat("_Smoothness", 0.35f);
            // F3b: Wedel wieder doppelseitig (Rueckseiten sichtbar, Flip-Normalen).
            leaf.SetFloat("_DoubleSidedEnable", 1f);
            leaf.SetFloat("_CullMode", 0f);
            leaf.SetFloat("_DoubleSidedNormalMode", 0f);

            Material stem = CreateBaumfarnMat("MAT_Baumfarn_Stem", hdrpLit,
                tex + "Baumfarn_Stem_Albedo.png", null, null);
            stem.SetFloat("_Smoothness", 0.2f);

            HDMaterial.ValidateMaterial(leaf);
            HDMaterial.ValidateMaterial(stem);

            EditorUtility.SetDirty(leaf);
            EditorUtility.SetDirty(stem);
            AssetDatabase.SaveAssets();
            return new[] { leaf, stem };
        }

        /// <summary>
        /// MODELL6: Farn-Scatter-Patch aus EINZELWEDDELN (ArtSource/UrwaldTest/fern/wedel_build.py
        /// -> fern_wedel.fbx, 3 Varianten je 2 Submeshes) plus Poisson-Disc-Patch-Zusammenstellung
        /// (wedel_patch.py -> fern_patch.txt, 245 Punkte wie U4: Patch 8 m, MIN_D 0.42 m).
        /// Je Instanz wird die im Patch-File gesetzte Variante referenziert: die Library haelt
        /// 245 Mesh-REFERENZEN auf nur 3 Mesh-Assets (geteilte Geometrie statt 245 Kopien).
        /// Blatt-/Stiel-Submesh-Reihenfolge wird gemessen (GetSubMesh(i).indexCount), nie angenommen.
        /// Opaque HDRP-Materialien mit prozeduralem Atlas (FarnWedel_Albedo/Normal, 8x4-Zellen).
        /// </summary>
        private static void LoadFernScatter(JungleAssetLibrary lib)
        {
            const string artFolder = "Assets/_Project/Art/Plants/UrwaldTestFern/";
            const string fbxAsset = artFolder + "fern_wedel.fbx";
            const string patchTxt = artFolder + "fern_patch.txt";

            // FBX und Patch-Datei aus ArtSource in den Asset-Baum kopieren (nur wenn fehlend).
            AssetUtil.EnsureFolder("Assets/_Project/Art/Plants/UrwaldTestFern");
            if (!System.IO.File.Exists(fbxAsset))
            {
                string srcFbx = "ArtSource/UrwaldTest/fern/fern_wedel.fbx";
                if (!System.IO.File.Exists(srcFbx))
                {
                    Debug.LogWarning("[Urwald] Fern-Wedel-FBX missing: " + srcFbx);
                    return;
                }
                System.IO.File.Copy(srcFbx, fbxAsset, true);
                AssetDatabase.ImportAsset(fbxAsset, ImportAssetOptions.ForceUpdate);
            }
            if (!System.IO.File.Exists(patchTxt))
            {
                string srcTxt = "ArtSource/UrwaldTest/fern/fern_patch.txt";
                if (!System.IO.File.Exists(srcTxt))
                {
                    Debug.LogWarning("[Urwald] Fern-Patch-Datei missing: " + srcTxt);
                    return;
                }
                System.IO.File.Copy(srcTxt, patchTxt, true);
                AssetDatabase.ImportAsset(patchTxt, ImportAssetOptions.ForceUpdate);
            }

            // Die HDRP-Materialien baut dieser Builder; die FBX-Slots liefern nur die
            // Submesh-Aufteilung (Stiel/Fiedern). Darum Material-Import AN lassen und
            // Read/Write aktivieren (UV-Zugriff im Test).
            ModelImporter modelImporter = AssetImporter.GetAtPath(fbxAsset) as ModelImporter;
            if (modelImporter == null)
            {
                Debug.LogWarning("[Urwald] Kein ModelImporter für: " + fbxAsset);
                return;
            }
            bool importerDirty = false;
            if (!modelImporter.isReadable)
            {
                modelImporter.isReadable = true;
                importerDirty = true;
            }
            // Material-Import explizit zuruecksetzen: ein frueherer Stand hatte ihn auf
            // None gestellt, was die Submeshes (Stiel/Fiedern) zu einem verschmilzt.
            if (modelImporter.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
            {
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importerDirty = true;
            }
            if (importerDirty)
            {
                modelImporter.SaveAndReimport();
            }

            // Die 3 Varianten-Meshes (je 2 Submeshes: Stiel + Fiedern) aus dem FBX holen.
            var variants = new System.Collections.Generic.List<Mesh>();
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fbxAsset))
            {
                if (obj is Mesh mesh && mesh.subMeshCount >= 2)
                {
                    variants.Add(mesh);
                }
            }
            if (variants.Count == 0)
            {
                Debug.LogWarning("[Urwald] Kein Varianten-Mesh im Fern-Wedel-FBX: " + fbxAsset);
                return;
            }

            // Submesh-Reihenfolge messen: das Fieder-Submesh hat klar mehr Dreiecke als der Stiel.
            Mesh probe = variants[0];
            int idx0 = probe.GetSubMesh(0).indexCount;
            int idx1 = probe.GetSubMesh(1).indexCount;
            lib.fernScatterLeafSubmesh = idx1 > idx0 ? 1 : 0;
            for (int vi = 0; vi < variants.Count; vi++)
            {
                int tris = variants[vi].GetSubMesh(0).indexCount / 3
                    + variants[vi].GetSubMesh(1).indexCount / 3;
                Debug.Log("[Urwald] Fern-Wedel Variante " + vi + ": " + tris + " Dreiecke, Hoehe "
                    + variants[vi].bounds.size.y.ToString("F2") + " m, Breite "
                    + variants[vi].bounds.size.x.ToString("F2") + " m");
            }

            // Patch-Zusammenstellung (Poisson-Disc, 245 Punkte): Zeile = "x z variante".
            var xz = new System.Collections.Generic.List<Vector3>();
            var picked = new System.Collections.Generic.List<Mesh>();
            string[] lines = System.IO.File.ReadAllLines(patchTxt);
            for (int li = 0; li < lines.Length; li++)
            {
                string ln = lines[li].Trim();
                if (ln.Length == 0 || ln.StartsWith("#")) continue;
                string[] parts = ln.Split(' ');
                if (parts.Length < 3) continue;
                try
                {
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    float px = float.Parse(parts[0], ci);
                    float pz = float.Parse(parts[1], ci);
                    int vr = int.Parse(parts[2], ci);
                    xz.Add(new Vector3(px, 0f, pz));
                    picked.Add(variants[((vr % variants.Count) + variants.Count) % variants.Count]);
                }
                catch (System.FormatException fe)
                {
                    Debug.LogWarning("[Urwald] Fern-Patch-Zeile unlesbar (" + li + "): " + ln + " " + fe.Message);
                }
            }
            if (picked.Count == 0)
            {
                Debug.LogWarning("[Urwald] Keine Patch-Punkte gelesen aus: " + patchTxt);
                return;
            }

            // XZ um die Patch-Mitte zentrieren (der Generator addiert sie auf den Patch-Anker).
            float minX = float.MaxValue, maxX = -float.MaxValue;
            float minZ = float.MaxValue, maxZ = -float.MaxValue;
            foreach (Vector3 q in xz)
            {
                if (q.x < minX) minX = q.x;
                if (q.x > maxX) maxX = q.x;
                if (q.z < minZ) minZ = q.z;
                if (q.z > maxZ) maxZ = q.z;
            }
            float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f;
            for (int pi = 0; pi < xz.Count; pi++)
            {
                Vector3 q = xz[pi];
                q.x -= cx;
                q.z -= cz;
                xz[pi] = q;
            }
            lib.fernScatterPatchSize = System.Math.Max(maxX - minX, maxZ - minZ);

            lib.fernScatterMeshes = picked.ToArray();
            lib.fernScatterXZ = xz.ToArray();
            lib.fernScatterCount = picked.Count;

            // Opaque HDRP-Materialien mit dem prozeduralen Wedel-Atlas (8x4 Zellen, 1024 px).
            Shader hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null)
                throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");

            const string texFolder = "Assets/_Project/Art/Plants/UrwaldTestFern/Textures/";
            Material leaf = GetOrCreateMat("MAT_FernScatter_Leaf", hdrpLit, new Color(0.10f, 0.27f, 0.06f));
            leaf.SetFloat("_SurfaceType", 0f);      // Opaque: Form ist echte Geometrie, kein Cutout
            leaf.SetFloat("_AlphaCutoffEnable", 0f);
            leaf.SetFloat("_Smoothness", 0.15f);
            leaf.SetFloat("_DoubleSidedEnable", 1f);
            leaf.SetFloat("_DoubleSidedNormalMode", 2f); // Flip: Rückseiten sichtbar
            leaf.enableInstancing = true;
            SetPHTexture(leaf, texFolder + "FarnWedel_Albedo.png", true);
            SetPHTexture(leaf, texFolder + "FarnWedel_Normal.png", false);

            Material stem = GetOrCreateMat("MAT_FernScatter_Stem", hdrpLit, new Color(0.13f, 0.11f, 0.05f));
            stem.SetFloat("_SurfaceType", 0f);
            stem.SetFloat("_AlphaCutoffEnable", 0f);
            stem.SetFloat("_Smoothness", 0.2f);
            stem.enableInstancing = true;
            SetPHTexture(stem, texFolder + "FarnWedel_Albedo.png", true);
            SetPHTexture(stem, texFolder + "FarnWedel_Normal.png", false);

            HDMaterial.ValidateMaterial(leaf);
            HDMaterial.ValidateMaterial(stem);
            EditorUtility.SetDirty(leaf);
            EditorUtility.SetDirty(stem);
            AssetDatabase.SaveAssets();

            lib.fernScatterLeafMat = leaf;
            lib.fernScatterStemMat = stem;

            Debug.Log("[Urwald] Fern-Scatter (MODELL6): " + lib.fernScatterCount + " Instances, "
                + variants.Count + " Varianten, leaf-submesh "
                + lib.fernScatterLeafSubmesh + " (idx0 " + idx0 + ", idx1 " + idx1 + ")");
        }

        /// <summary>U3b/TEXTUR2: Grundfarne (Blender fern_1..5.fbx) — 5 Varianten, je EIN Mesh mit
        /// 2 Submeshes (Slot 0 Stiel, Slot 1 Blatt). Real geometry -> opaque mats mit Atlas-Textur.
        /// Fills lib.groundFerns / groundFernLeafMat / groundFernStemMat / groundFernLeafSubmesh.</summary>
        private static void LoadGroundFerns(JungleAssetLibrary lib)
        {
            const string artFolder = "Assets/_Project/Art/Plants/UrwaldTestFern/";
            const string texFolder = "Assets/_Project/Art/Plants/UrwaldTestFern/Textures/";
            const string srcFolder = "ArtSource/UrwaldTest/fern/";
            Shader hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null)
                throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");

            var meshes = new System.Collections.Generic.List<Mesh>();
            for (int v = 1; v <= 5; v++)
            {
                string artPath = artFolder + "fern_" + v + ".fbx";
                if (!System.IO.File.Exists(artPath))
                {
                    string src = srcFolder + "fern_" + v + ".fbx";
                    if (System.IO.File.Exists(src))
                    {
                        AssetUtil.EnsureFolder("Assets/_Project/Art/Plants/UrwaldTestFern");
                        System.IO.File.Copy(src, artPath, true);
                        AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceUpdate);
                    }
                }
                if (!System.IO.File.Exists(artPath))
                {
                    Debug.LogWarning("[Urwald] Grundfarn-FBX fehlt: " + artPath);
                    continue;
                }
                // TEXTUR2: Read/Write fuer dieses Mesh aktivieren (UV-Check im Test).
                ModelImporter fimp = AssetImporter.GetAtPath(artPath) as ModelImporter;
                if (fimp != null && !fimp.isReadable)
                {
                    fimp.isReadable = true;
                    fimp.SaveAndReimport();
                }
                Mesh found = null;
                int bestTris = 0;
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(artPath))
                {
                    if (obj is Mesh mesh && mesh != null)
                    {
                        int tris = 0;
                        for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
                        if (tris > bestTris) { bestTris = tris; found = mesh; }
                    }
                }
                if (found == null || bestTris == 0)
                {
                    Debug.LogWarning("[Urwald] Grundfarn ohne Triangles: " + artPath);
                    continue;
                }
                meshes.Add(found);
            }
            if (meshes.Count == 0) { Debug.LogWarning("[Urwald] Kein Grundfarn-Mesh loaded."); return; }

            lib.groundFerns = meshes.ToArray();

            Material leaf = GetOrCreateMat("MAT_Grundfarn_Leaf", hdrpLit, new Color(0.09f, 0.24f, 0.06f));
            leaf.SetFloat("_SurfaceType", 0f);
            leaf.SetFloat("_AlphaCutoffEnable", 0f);
            leaf.SetFloat("_Smoothness", 0.15f);
            leaf.SetFloat("_DoubleSidedEnable", 1f);
            leaf.SetFloat("_DoubleSidedNormalMode", 2f);
            // TEXTUR2: Atlas-Albedo + Normalmap (je Fieder eine UV-Insel, V entlang der
            // Mittelrippe). Weisser Tint: Farbe kommt aus der Karte, Instanz-Tinte ist ein
            // hell-multipikativer Faktor (ApplyInstanceTint).
            SetPHTexture(leaf, texFolder + "Farn_Grund_Atlas_Albedo.png", true);
            SetPHTexture(leaf, texFolder + "Farn_Grund_Atlas_Normal.png", false);
            leaf.SetColor("_BaseColor", Color.white);
            leaf.enableInstancing = true;

            Material stem = GetOrCreateMat("MAT_Grundfarn_Stem", hdrpLit, new Color(0.10f, 0.145f, 0.075f));
            stem.SetFloat("_SurfaceType", 0f);
            stem.SetFloat("_AlphaCutoffEnable", 0f);
            stem.SetFloat("_Smoothness", 0.2f);
            SetPHTexture(stem, texFolder + "Farn_Grund_Atlas_Albedo.png", true);
            SetPHTexture(stem, texFolder + "Farn_Grund_Atlas_Normal.png", false);
            stem.SetColor("_BaseColor", Color.white);
            stem.enableInstancing = true;

            HDMaterial.ValidateMaterial(leaf);
            HDMaterial.ValidateMaterial(stem);
            EditorUtility.SetDirty(leaf);
            EditorUtility.SetDirty(stem);
            AssetDatabase.SaveAssets();

            lib.groundFernLeafMat = leaf;
            lib.groundFernStemMat = stem;
            lib.groundFernLeafSubmesh = 1; // FBX-Slot-Reihenfolge unveraenderlich: 0 Stiel, 1 Blatt
            Debug.Log("[Urwald] Grundfarne: " + lib.groundFerns.Length + " Varianten loaded (opaque, atlas-textured, stem+leaf submeshes)");
        }

        /// <summary>U4: load or create an HDRP/Lit material asset in MatFolder with a base color.</summary>
        private static Material GetOrCreateMat(string name, Shader shader, Color color)
        {
            string path = MatFolder + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            return mat;
        }

        private static Material CreateBaumfarnMat(string name, Shader shader, string albedo, string mask, string normal)
        {
            string path = MatFolder + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            if (albedo != null) mat.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
            if (mask != null) mat.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mask));
            if (normal != null) mat.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normal));
            mat.color = Color.white;
            return mat;
        }

        /// <summary>MODELL16: Blender-Totholz-FBX (3 Varianten, je 2 Submeshes:
        /// 0 = Borke, 1 = Holz/Bruchflaeche; Reihenfolge gemessen ueber indexCount,
        /// Borke ist der deutlich groessere Anteil). Read/Write wie Baumfarn (M16-Test).</summary>
        private static Mesh[] LoadTotholzMeshes()
        {
            const string folder = "Assets/_Project/Art/Plants/Totholz/";
            var result = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                string path = folder + "Totholz_" + (i + 1) + ".fbx";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[Urwald] Totholz-FBX fehlt: " + path);
                    return null;
                }
                ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp != null && !imp.isReadable)
                {
                    imp.isReadable = true;
                    imp.SaveAndReimport();
                }
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is Mesh mesh && !mesh.name.Contains("_Hull"))
                    {
                        result[i] = mesh;
                        break;
                    }
                }
                if (result[i] == null)
                {
                    Debug.LogWarning("[Urwald] Kein Mesh im Totholz-FBX: " + path);
                    return null;
                }
                Mesh m = result[i];
                if (m.subMeshCount >= 2)
                {
                    int a = m.GetSubMesh(0).indexCount, b = m.GetSubMesh(1).indexCount;
                    Debug.Log("[Urwald] Totholz_" + (i + 1) + " Submesh-IndexCounts: 0=" + a + " 1=" + b
                        + " (erwartet: 0=Borke deutlich groesser als 1=Holz)");
                }
            }
            return result;
        }

        /// <summary>MODELL16: konvexe Hull-FBXs (20 Verts je Prisma) zu den Totholz-Varianten.</summary>
        private static Mesh[] LoadTotholzHulls()
        {
            const string folder = "Assets/_Project/Art/Plants/Totholz/";
            var result = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                string path = folder + "Totholz_" + (i + 1) + "_Hull.fbx";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[Urwald] Totholz-Hull-FBX fehlt: " + path);
                    continue;
                }
                ModelImporter himp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (himp != null && !himp.isReadable)
                {
                    himp.isReadable = true;
                    himp.SaveAndReimport();
                }
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is Mesh mesh) { result[i] = mesh; break; }
                }
            }
            return result;
        }

        /// <summary>MODELL17: Blender-Stein-FBX (5 Varianten, je 1 Submesh, Vertexfarbe
        /// 'Col' mit Moos oben). Read/Write wie Totholz (MeshCollider convex noetig).</summary>
        private static Mesh[] LoadSteineMeshes()
        {
            const string folder = "Assets/_Project/Art/Plants/Steine/";
            var result = new Mesh[5];
            for (int i = 0; i < 5; i++)
            {
                string path = folder + "Stein_" + (i + 1) + ".fbx";
                if (!System.IO.File.Exists(path))
                {
                    Debug.LogWarning("[Urwald] Stein-FBX fehlt: " + path);
                    return null;
                }
                ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp != null && !imp.isReadable)
                {
                    imp.isReadable = true;
                    imp.SaveAndReimport();
                }
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is Mesh mesh) { result[i] = mesh; break; }
                }
                if (result[i] == null)
                {
                    Debug.LogWarning("[Urwald] Kein Mesh im Stein-FBX: " + path);
                    return null;
                }
                Mesh m = result[i];
                int tris = 0;
                for (int s = 0; s < m.subMeshCount; s++) tris += m.GetSubMesh(s).indexCount / 3;
                bool hasCol = m.colors32 != null && m.colors32.Length == m.vertexCount;
                Debug.Log("[Urwald] Stein_" + (i + 1) + " Dreiecke=" + tris + " submeshes=" + m.subMeshCount
                    + " vertexColors=" + hasCol + " bounds=" + m.bounds.size);
            }
            return result;
        }

        /// <summary>MODELL16: HDRP-Lit-Materialien MAT_Totholz_Bark / MAT_Totholz_Wood
        /// (opak, Alpha-Cutoff aus, DoubleSided Mirror, Smoothness 0.15 wie PH-Plant-Standard).</summary>
        private static Material[] CreateTotholzMats()
        {
            const string tex = "Assets/_Project/Art/Plants/Totholz/Textures/";
            Shader hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null)
                throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");

            Material bark = CreateBaumfarnMat("MAT_Totholz_Bark", hdrpLit,
                tex + "Totholz_Bark_Albedo.png", null, tex + "Totholz_Bark_Normal.png");
            bark.SetFloat("_SurfaceType", 0f);
            bark.SetFloat("_AlphaCutoffEnable", 0f);
            bark.SetFloat("_DoubleSidedEnable", 1f);
            bark.SetFloat("_DoubleSidedNormalMode", 1f);   // Mirror (Rinde innen sichtbar an Bruchkanten)
            bark.SetFloat("_Smoothness", 0.15f);

            Material wood = CreateBaumfarnMat("MAT_Totholz_Wood", hdrpLit,
                tex + "Totholz_Wood_Albedo.png", null, tex + "Totholz_Wood_Normal.png");
            wood.SetFloat("_SurfaceType", 0f);
            wood.SetFloat("_AlphaCutoffEnable", 0f);
            wood.SetFloat("_DoubleSidedEnable", 1f);
            wood.SetFloat("_DoubleSidedNormalMode", 1f);
            wood.SetFloat("_Smoothness", 0.2f);

            HDMaterial.ValidateMaterial(bark);
            HDMaterial.ValidateMaterial(wood);
            EditorUtility.SetDirty(bark);
            EditorUtility.SetDirty(wood);
            AssetDatabase.SaveAssets();
            return new[] { bark, wood };
        }

        /// <summary>
        /// B3: loading the 5 Poly Haven trees (LOD0/LOD1 per FBX) plus their HDRP materials
        /// (wood opaque; leaves Alpha-Cutout 0.5, double-sided) into the asset library.
        /// Submesh order measured via GetSubMesh(i).bounds (leaf = wider crown), scale from
        /// mesh height (Jacaranda approx. 30 m, island trees 3-5x).
        /// </summary>
        private static void LoadPHTrees(JungleAssetLibrary lib)
        {
            const string root = "Assets/_Project/Art/Plants/PH_Baeume/";
            string[] ids = { "jacaranda_tree", "island_tree_01", "island_tree_02", "island_tree_03", "tree_small_02" };

            lib.phTreeLod0 = new Mesh[ids.Length];
            lib.phTreeLod1 = new Mesh[ids.Length];
            lib.phTreeWoodMats = new Material[ids.Length];
            lib.phTreeLeafMats = new Material[ids.Length];
            lib.phTreeScales = new float[ids.Length];
            lib.phTreeSlotNames = new string[ids.Length];

            for (int i = 0; i < ids.Length; i++)
            {
                string fbx = root + ids[i] + "/" + ids[i] + ".fbx";
                var lodMeshes = new Mesh[2];
                var fbxRenderers = new MeshRenderer[8]; // FBX takes: few Renderers per model
                int nRenders = 0;
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fbx))
                {
                    if (obj is Mesh mesh && mesh.name.Contains("_LOD0")) lodMeshes[0] = mesh;
                    if (obj is Mesh m1 && m1.name.Contains("_LOD1")) lodMeshes[1] = m1;
                    if (obj is MeshRenderer mr && nRenders < fbxRenderers.Length) fbxRenderers[nRenders++] = mr;
                }
                // BUG-1 name ground truth: FBX slot names on the LOD0 renderer, index i =
                // submesh i (Unity sorts slots alphabetically, so order is NOT a contract).
                // MeshRenderer has NO public mesh accessor at all (CS1061 both for .mesh and
                // .sharedMesh) -> select by NAME only: prefer "_LOD0" (case-insensitive), else
                // first collected renderer with a warning (slot-name log stays ground truth).
                MeshRenderer lod0Renderer = null;
                for (int ri = 0; ri < nRenders; ri++)
                {
                    var cand = fbxRenderers[ri];
                    if (lod0Renderer == null && cand.name.ToLower().Contains("_lod0")) lod0Renderer = cand;
                }
                if (lod0Renderer == null && nRenders > 0)
                {
                    lod0Renderer = fbxRenderers[0];
                    Debug.LogWarning("[Urwald] PH-Tree " + ids[i] + ": no renderer named *_LOD0*, using first FBX MeshRenderer for slot names (check the log line below)");
                }

                if (lodMeshes[0] == null || lodMeshes[1] == null)
                {
                    foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fbx))
                    {
                        if (obj is Mesh badMesh)
                        {
                            Debug.Log("[Urwald] PH-Tree Mesh found: " + badMesh.name + " verts=" + badMesh.vertexCount);
                        }
                    }

                    Debug.LogWarning("[Urwald] PH-Tree FBX without LOD0/LOD1: " + fbx);
                    continue;
                }

                lib.phTreeLod0[i] = lodMeshes[0];
                lib.phTreeLod1[i] = lodMeshes[1];
                // Slot names once per species (ground truth for the runtime name-match rule).
                var slotNames = new System.Collections.Generic.List<string>();
                if (lod0Renderer != null && lod0Renderer.sharedMaterials != null)
                {
                    foreach (var sm in lod0Renderer.sharedMaterials)
                        slotNames.Add(sm != null ? sm.name : "");
                }
                lib.phTreeSlotNames[i] = string.Join("|", slotNames.ToArray());
                if (lod0Renderer == null) Debug.LogWarning("[Urwald] PH-Tree FBX without LOD0 MeshRenderer: " + fbx + " (runtime falls back to geometry rule)");
                else Debug.Log(string.Format("[Urwald] PH-Tree {0} slot names: {1}", ids[i], lib.phTreeSlotNames[i]));
                var mats = CreatePHTreeMats(ids[i]);
                lib.phTreeWoodMats[i] = mats[0];
                lib.phTreeLeafMats[i] = mats[1];

                float height = lodMeshes[0].bounds.size.y;
                float target = ids[i] == "jacaranda_tree" ? 30f : 12f;
                float scale = target / Mathf.Max(0.01f, height);
                // MODELL23: island_tree_01 ist jetzt der Blender-Neubau (Mesh selbst 12-15 m)
                // -> Skala ~1. Die 3-5x-Klemme galt nur fuer die kleinen PH-Scans und wuerde
                // den neuen Baum auf ~40 m aufblasen.
                if (ids[i] != "jacaranda_tree" && ids[i] != "island_tree_01")
                {
                    scale = Mathf.Clamp(scale, 3f, 5f);
                }

                lib.phTreeScales[i] = scale;

                // Q3: Bark-UV-Tiling per tree type from mesh size (NOT fixed 4x10): one texture
                // repeat covers about BarkMetersPerRepeat meters of trunk, so the bark reads as
                // fine detail at any tree height instead of diagonal stretched stripes.
                int woodSub = PickEditorWoodSubmesh(lib.phTreeSlotNames[i], lodMeshes[0]);
                Vector2 barkST = ComputeBarkTilingPerMeter(lodMeshes[0], woodSub, scale);
                SetPHUVScale(mats[0], barkST.x, barkST.y);

                Rect uvRect = ComputeSubmeshUvBounds(lodMeshes[0], woodSub);
                Debug.Log(string.Format("[Urwald] PH-Tree {0}: Hoehe {1:0.00} m -> Scale {2:0.00} -> Hoehe {3:0.0} m (Kriterium Jacaranda 25-35, else 9-20); Wood=Sub{4}, UVRect={5:0.00}x{6:0.00}, BarkST={7:0.0}x{8:0.0}",
                    ids[i], height, scale, height * scale, woodSub, uvRect.width, uvRect.height, barkST.x, barkST.y));
            }
        }

        /// <summary>
        /// HDRP-Lit-Materials per PH-Tree: [0]=Holz opak (trunk/branch-Textures),
        /// [1]=Blätter Alpha-Cutout 0.5, doppelseitig (leaves_*-Textures). Existing get reused,
        /// afterwards HDMaterial.ValidateMaterial. Texture-Import: Normal non-sRGB, Mask non-sRGB.
        /// </summary>
        private static Material[] CreatePHTreeMats(string id)
        {
            string tex = "Assets/_Project/Art/Plants/PH_Baeume/" + id + "/Textures/";
            // Wood-Base: Jacaranda "trunk", else "<id>_Albedo".
            string woodBase = tex + id + "_" + (id == "jacaranda_tree" ? "trunk_" : "");

            Material wood = GetOrLoadPHTreeMat("MAT_PHTree_" + id + "_Wood");
            SetPHTexture(wood, woodBase + "Albedo.png", true);
            SetPHTexture(wood, woodBase + "Normal.png", false);
            SetPHTexture(wood, woodBase + "Mask.png", false);
            wood.SetFloat("_SurfaceType", 0f); // opaque
            wood.SetFloat("_AlphaCutoffEnable", 0f);
            wood.SetFloat("_Smoothness", 0.15f);
            // Q3: Wood/Bark UV-TILING. Fixed 4x10 (M1c) was too coarse -> diagonal stretched
            // stripes in the world images. The repeat counts now come from ComputeBarkTilingPerMeter
            // per tree type (about one texture repeat per meter), set in LoadPHTrees where the
            // mesh size and scale are known. Set _ST on BaseColor/Normal/Mask together so all
            // maps stay aligned.

            Material leaf = GetOrLoadPHTreeMat("MAT_PHTree_" + id + "_Leaf");
            SetPHTexture(leaf, tex + id + "_leaves_Albedo.png", true);
            SetPHTexture(leaf, tex + id + "_leaves_Normal.png", false);
            SetPHTexture(leaf, tex + id + "_leaves_Mask.png", false);
            leaf.SetFloat("_SurfaceType", 1f); // Cutout
            leaf.SetFloat("_AlphaCutoffEnable", 1f);
            leaf.SetFloat("_AlphaCutoff", 0.5f);
            leaf.SetFloat("_DoubleSidedEnable", 1f);
            leaf.SetFloat("_CullMode", 0f);
            leaf.SetFloat("_DoubleSidedNormalMode", 2f); // Reversed (Flip-Normals)
            leaf.enableInstancing = true;

            HDMaterial.ValidateMaterial(wood);
            HDMaterial.ValidateMaterial(leaf);
            EditorUtility.SetDirty(wood);
            EditorUtility.SetDirty(leaf);
            return new[] { wood, leaf };
        }

        private static Material GetOrLoadPHTreeMat(string name)
        {
            string path = MatFolder + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader hdrpLit = Shader.Find("HDRP/Lit");
                if (hdrpLit == null)
                {
                    throw new System.InvalidOperationException("[Urwald] Shader nicht gefunden: HDRP/Lit");
                }

                mat = new Material(hdrpLit) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.color = Color.white;
            return mat;
        }

        /// <summary>Texture-Assignment with Import-Settings: sRGB Albedo (alphaTransparency), Normal non-sRGB.</summary>
        private static void SetPHTexture(Material mat, string texPath, bool isAlbedo)
        {
            if (!System.IO.File.Exists(texPath))
            {
                Debug.LogWarning("[Urwald] PH-Tree-Texture fehlt: " + texPath);
                return;
            }

            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                bool isNormal = texPath.EndsWith("Normal.png");
                importer.sRGBTexture = isAlbedo;
                importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.alphaIsTransparency = isAlbedo;
                importer.SaveAndReimport();
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                Debug.LogWarning("[Urwald] PH-Texture not loadable: " + texPath);
                return;
            }

            string prop = texPath.EndsWith("Normal.png") ? "_NormalMap" : texPath.EndsWith("Mask.png") ? "_MaskMap" : "_BaseColorMap";
            mat.SetTexture(prop, tex);
        }

        /// <summary>Sets UV-Tiling (ST) on the Wood/Bark maps so bark repeats instead of one
        /// texture stretched over a tall trunk. Vector4 = tiling.xy, offset.zw.</summary>
        private static void SetPHUVScale(Material mat, float u, float v)
        {
            // Proven pattern (cf. ScanBark): mainTextureScale drives HDRP UV-tiling for the base map.
            mat.mainTextureScale = new UnityEngine.Vector2(u, v);
            var st = new UnityEngine.Vector4(u, v, 0f, 0f);
            if (mat.HasProperty("_BaseColorMap")) mat.SetVector("_BaseColorMap_ST", st);
            if (mat.HasProperty("_NormalMap")) mat.SetVector("_NormalMap_ST", st);
            if (mat.HasProperty("_MaskMap")) mat.SetVector("_MaskMap_ST", st);
        }

        /// <summary>Q3: One bark-texture repeat should cover about this many meters of trunk
        /// (Backlog: "Textur soll rund 1 m pro Wiederholung zeigen"). Single tuning constant.</summary>
        private const float BarkMetersPerRepeat = 1f;

        /// <summary>Q3: Wood-submesh index per FBX slot names ('|' = submesh, ground truth from
        /// LoadPHTrees). bark|trunk wins; island trees have a bare species slug for the trunk ->
        /// geometry fallback: reaches the mesh bottom + smallest footprint (same rule as the
        /// runtime PickWoodSubmesh in JungleWorldGenerator).</summary>
        private static int PickEditorWoodSubmesh(string slotNamesRaw, Mesh lod0)
        {
            int subs = System.Math.Max(1, lod0.subMeshCount);
            if (subs == 1) return 0;
            string[] slotNames = null;
            if (slotNamesRaw != null && slotNamesRaw.Length > 0) slotNames = slotNamesRaw.Split('|');
            float meshMinY = lod0.bounds.min.y;
            int woodSub = -1;
            float bestScore = 1e9f;
            for (int s = 0; s < subs; s++)
            {
                Bounds sb = lod0.GetSubMesh(s).bounds;
                if (sb.size.y < 0.5f) continue; // twig/flower submeshes never win
                bool touchesGround = sb.min.y <= meshMinY + 0.3f;
                if (!touchesGround) continue;
                float score = sb.size.x * sb.size.z; // smallest footprint wins
                if (slotNames != null && s < slotNames.Length)
                {
                    string n = slotNames[s].ToLower();
                    if (n.Contains("bark") || n.Contains("trunk")) score -= 1e6f;
                    else if (n.Contains("leaf") || n.Contains("leaves") || n.Contains("branch")) score += 1e6f;
                }
                if (score < bestScore) { bestScore = score; woodSub = s; }
            }
            return woodSub >= 0 ? woodSub : 0;
        }

        /// <summary>Q3: Bark tiling (repeat counts) from the trunk geometry so one texture repeat
        /// covers ~BarkMetersPerRepeat meters. UV space of the wood submesh is measured from its
        /// triangles' UVs (FBX bark UVs do NOT span 0..1; assuming 1x1 was the reason the fixed
        /// 4x10 still smeared). U = around the trunk circumference, V = along its height.
        /// Clamped to [1, 48] so branches sharing the wood material never explode.</summary>
        private static Vector2 ComputeBarkTilingPerMeter(Mesh lod0, int woodSub, float scale)
        {
            Bounds sb = lod0.GetSubMesh(woodSub).bounds;
            float hW = Mathf.Max(0.01f, sb.size.y * scale);                       // trunk height in meters
            float perim = Mathf.PI * Mathf.Sqrt(Mathf.Max(0.01f, sb.size.x * scale * sb.size.z * scale)); // rough circumference
            Rect uv = ComputeSubmeshUvBounds(lod0, woodSub);
            float uvW = uv.width > 0.05f ? uv.width : 1f; // degenerate/missing UVs -> assume 0..1
            float uvH = uv.height > 0.05f ? uv.height : 1f;
            float u = Mathf.Clamp(perim / (BarkMetersPerRepeat * uvW), 1f, 48f);
            float v = Mathf.Clamp(hW / (BarkMetersPerRepeat * uvH), 1f, 48f);
            return new UnityEngine.Vector2(u, v);
        }

        /// <summary>Q3: UV extent (min/max) of the triangles belonging to one submesh. Measured,
        /// not assumed — the FBX bark UV islands do not fill 0..1.</summary>
        private static Rect ComputeSubmeshUvBounds(Mesh lod0, int sub)
        {
            var uvs = new System.Collections.Generic.List<UnityEngine.Vector2>();
            lod0.GetUVs(0, uvs);
            if (uvs.Count == 0) return new UnityEngine.Rect(0f, 0f, 1f, 1f);
            int[] idx = lod0.GetIndices(sub);
            if (idx.Length == 0) return new UnityEngine.Rect(0f, 0f, 1f, 1f);
            float minU = 1e9f, minV = 1e9f, maxU = -1e9f, maxV = -1e9f;
            for (int k = 0; k < idx.Length; k++)
            {
                UnityEngine.Vector2 uvv = uvs[idx[k]];
                if (uvv.x < minU) minU = uvv.x;
                if (uvv.x > maxU) maxU = uvv.x;
                if (uvv.y < minV) minV = uvv.y;
                if (uvv.y > maxV) maxV = uvv.y;
            }
            return new UnityEngine.Rect(minU, minV, maxU - minU, maxV - minV);
        }

        /// <summary>Lädt Scan-Bodendecker (Gras/Kraut) als Mesh-/Material-Paare in die Asset-Bibliothek.</summary>
        private static void LoadGroundCover(JungleAssetLibrary lib)
        {
            // A1: more species (nettle, periwinkle) -> the variant-per-Instance Hash01 pick
            // below spreads them; groundCover[0] stays grass so older code keeps working.
            // MODELL1: grass_medium_01 is replaced by the procedural Blender clump (Gras1.fbx).
            // MODELL2: grass_medium_02 is replaced by the Blender sedge clump (Gras2.fbx).
            string[] ids = { "grass_medium_01", "grass_medium_02", "weed_plant_02", "nettle_plant", "periwinkle_plant" };
            var meshes = new System.Collections.Generic.List<Mesh>();
            var mats = new System.Collections.Generic.List<Material>();

            Mesh gras1 = LoadBlenderGras1();
            if (gras1 != null)
            {
                Shader hdrpLit = Shader.Find("HDRP/Lit");
                Material grasMat = GetOrCreateMat("MAT_Gras1", hdrpLit, new Color(0.13f, 0.28f, 0.08f));
                grasMat.SetFloat("_SurfaceType", 0f);
                grasMat.SetFloat("_AlphaCutoffEnable", 0f);
                grasMat.SetFloat("_Smoothness", 0.15f);
                grasMat.SetFloat("_DoubleSidedEnable", 1f);
                grasMat.SetFloat("_DoubleSidedNormalMode", 2f);
                // TEXTUR1: Albedo (Scan-Vorlage + Verlauf) und Normalmap je Halm;
                // UV-Raster im FBX, V entlang des Halms. Weisser Tint: Farbe kommt aus der Karte.
                SetPHTexture(grasMat, "Assets/_Project/Art/Plants/Gras1/Textures/Gras1_Albedo.png", true);
                SetPHTexture(grasMat, "Assets/_Project/Art/Plants/Gras1/Textures/Gras1_Normal.png", false);
                grasMat.SetColor("_BaseColor", Color.white);
                grasMat.enableInstancing = true;
                HDMaterial.ValidateMaterial(grasMat);
                EditorUtility.SetDirty(grasMat);
                meshes.Add(gras1);
                mats.Add(grasMat);
            }

            // MODELL2: Seggen-Bueschel (20 breite ueberhaengende Blaetter, 763 Dreiecke).
            Mesh gras2 = LoadBlenderGras2();
            if (gras2 != null)
            {
                Shader hdrpLit2 = Shader.Find("HDRP/Lit");
                Material gras2Mat = GetOrCreateMat("MAT_Gras2", hdrpLit2, new Color(0.14f, 0.27f, 0.10f));
                gras2Mat.SetFloat("_SurfaceType", 0f);
                gras2Mat.SetFloat("_AlphaCutoffEnable", 0f);
                gras2Mat.SetFloat("_Smoothness", 0.15f);
                gras2Mat.SetFloat("_DoubleSidedEnable", 1f);
                gras2Mat.SetFloat("_DoubleSidedNormalMode", 2f);
                gras2Mat.enableInstancing = true;
                HDMaterial.ValidateMaterial(gras2Mat);
                EditorUtility.SetDirty(gras2Mat);
                meshes.Add(gras2);
                mats.Add(gras2Mat);
            }

            // MODELL3: Unkraut-Rosette (12 gezaeckte Blaetter, 624 Dreiecke,
            // Loewenzahn-/Ampfer-Typ) ersetzt den Poly-Haven-Scan weed_plant_02.
            Mesh unkraut = LoadBlenderUnkraut();
            if (unkraut != null)
            {
                Shader hdrpLit3 = Shader.Find("HDRP/Lit");
                Material unkrautMat = GetOrCreateMat("MAT_Unkraut", hdrpLit3, Color.white);
                unkrautMat.SetFloat("_SurfaceType", 0f);
                unkrautMat.SetFloat("_AlphaCutoffEnable", 0f);
                unkrautMat.SetFloat("_Smoothness", 0.15f);
                unkrautMat.SetFloat("_DoubleSidedEnable", 1f);
                unkrautMat.SetFloat("_DoubleSidedNormalMode", 2f);
                // TEXTUR: Albedo/Normal je Blattzelle im 4x3-Atlas, UV im FBX.
                SetPHTexture(unkrautMat, "Assets/_Project/Art/Plants/Unkraut/Textures/Unkraut_Albedo.png", true);
                SetPHTexture(unkrautMat, "Assets/_Project/Art/Plants/Unkraut/Textures/Unkraut_Normal.png", false);
                unkrautMat.SetColor("_BaseColor", Color.white);
                unkrautMat.enableInstancing = true;
                HDMaterial.ValidateMaterial(unkrautMat);
                EditorUtility.SetDirty(unkrautMat);
                meshes.Add(unkraut);
                mats.Add(unkrautMat);
            }

            // MODELL4: Immergruen-Bodendecker (Vinca-Typ: 3 Ranken mit
            // gegenstaendigen Blattpaaren, Rosette; 868 Dreiecke, 0.10 m hoch)
            // ersetzt den Poly-Haven-Scan periwinkle_plant.
            Mesh immergruen = LoadBlenderImmergruen();
            if (immergruen != null)
            {
                Shader hdrpLit4 = Shader.Find("HDRP/Lit");
                Material igMat = GetOrCreateMat("MAT_Immergruen", hdrpLit4, Color.white);
                igMat.SetFloat("_SurfaceType", 0f);
                igMat.SetFloat("_AlphaCutoffEnable", 0f);
                igMat.SetFloat("_Smoothness", 0.42f); // Vinca: glaenzende Blattoberseite
                igMat.SetFloat("_DoubleSidedEnable", 1f);
                igMat.SetFloat("_DoubleSidedNormalMode", 2f);
                // TEXTUR: Albedo/Normal je Blattzelle im 8x4-Atlas (Ranke Zellen
                // 29..31), UV im FBX, V entlang der Mittelrippe.
                SetPHTexture(igMat, "Assets/_Project/Art/Plants/Immergruen/Textures/Immergruen_Albedo.png", true);
                SetPHTexture(igMat, "Assets/_Project/Art/Plants/Immergruen/Textures/Immergruen_Normal.png", false);
                igMat.SetColor("_BaseColor", Color.white);
                igMat.enableInstancing = true;
                HDMaterial.ValidateMaterial(igMat);
                EditorUtility.SetDirty(igMat);
                meshes.Add(immergruen);
                mats.Add(igMat);
            }

            // MODELL5: Brennnessel (eckiger 4-kantiger Stengel mit 5 gegen-
            // staendigen Blattpaaren + Endpaar, gezaeckte herzfoermige Blatt-
            // spreiten, 766 Dreiecke, 0.74 m) ersetzt den Poly-Haven-Scan
            // nettle_plant.
            Mesh brennessel = LoadBlenderBrennnessel();
            if (brennessel != null)
            {
                Shader hdrpLit5 = Shader.Find("HDRP/Lit");
                Material bnMat = GetOrCreateMat("MAT_Brennnessel", hdrpLit5, Color.white);
                bnMat.SetFloat("_SurfaceType", 0f);
                bnMat.SetFloat("_AlphaCutoffEnable", 0f);
                bnMat.SetFloat("_Smoothness", 0.18f);
                bnMat.SetFloat("_DoubleSidedEnable", 1f);
                bnMat.SetFloat("_DoubleSidedNormalMode", 2f);
                // TEXTUR: Albedo/Normal je Blattzelle im 8x4-Atlas (Zellen
                // 0..11 Blaetter, 12..21 Stiele, 24 Stengel), V entlang der Rippe.
                SetPHTexture(bnMat, "Assets/_Project/Art/Plants/Brennnessel/Textures/Brennnessel_Albedo.png", true);
                SetPHTexture(bnMat, "Assets/_Project/Art/Plants/Brennnessel/Textures/Brennnessel_Normal.png", false);
                bnMat.SetColor("_BaseColor", Color.white);
                bnMat.enableInstancing = true;
                HDMaterial.ValidateMaterial(bnMat);
                EditorUtility.SetDirty(bnMat);
                meshes.Add(brennessel);
                mats.Add(bnMat);
            }

            // MODELL18: Blumen (Vinca-Bluete: 5 windradige Kronblaetter mit
            // Augenscheibe und Staubblaettern, 3 Stengel mit lanzettlichen
            // Blaettern; 834 Dreiecke, 0.27 m). Rosa und gelb/orange nutzen
            // dasselbe Mesh, Farbe kommt aus zwei Albedo-Karten.
            Mesh blume = LoadBlenderBlume();
            if (blume != null)
            {
                Shader hdrpLit18 = Shader.Find("HDRP/Lit");
                Material blueteR = GetOrCreateMat("MAT_Bluete_Rosa", hdrpLit18, Color.white);
                blueteR.SetFloat("_SurfaceType", 0f);
                blueteR.SetFloat("_AlphaCutoffEnable", 0f);
                blueteR.SetFloat("_Smoothness", 0.25f);
                blueteR.SetFloat("_DoubleSidedEnable", 1f);
                blueteR.SetFloat("_DoubleSidedNormalMode", 2f);
                // TEXTUR: Albedo/Normal je Zelle im 8x5-Atlas (Stengel 0..2,
                // Blaetter 3..17, Kronblaetter 18..32, Mitte 33..35).
                SetPHTexture(blueteR, "Assets/_Project/Art/Plants/Blume/Textures/Blume_Albedo_Rosa.png", true);
                SetPHTexture(blueteR, "Assets/_Project/Art/Plants/Blume/Textures/Blume_Normal.png", false);
                blueteR.SetColor("_BaseColor", Color.white);
                blueteR.enableInstancing = true;
                HDMaterial.ValidateMaterial(blueteR);
                EditorUtility.SetDirty(blueteR);
                meshes.Add(blume);
                mats.Add(blueteR);

                Material blueteG = GetOrCreateMat("MAT_Bluete_Gelb", hdrpLit18, Color.white);
                blueteG.SetFloat("_SurfaceType", 0f);
                blueteG.SetFloat("_AlphaCutoffEnable", 0f);
                blueteG.SetFloat("_Smoothness", 0.25f);
                blueteG.SetFloat("_DoubleSidedEnable", 1f);
                blueteG.SetFloat("_DoubleSidedNormalMode", 2f);
                SetPHTexture(blueteG, "Assets/_Project/Art/Plants/Blume/Textures/Blume_Albedo_Gelb.png", true);
                SetPHTexture(blueteG, "Assets/_Project/Art/Plants/Blume/Textures/Blume_Normal.png", false);
                blueteG.SetColor("_BaseColor", Color.white);
                blueteG.enableInstancing = true;
                HDMaterial.ValidateMaterial(blueteG);
                EditorUtility.SetDirty(blueteG);
                meshes.Add(blume);
                mats.Add(blueteG);
            }

            foreach (string id in ids)
            {
                if (id == "grass_medium_01" && gras1 != null) continue; // MODELL1: schon ersetzt
                if (id == "grass_medium_02" && gras2 != null) continue; // MODELL2: schon ersetzt
                if (id == "weed_plant_02" && unkraut != null) continue; // MODELL3: schon ersetzt
                if (id == "periwinkle_plant" && immergruen != null) continue; // MODELL4: schon ersetzt
                if (id == "nettle_plant" && brennessel != null) continue; // MODELL5: schon ersetzt
                if (!ScanModels.IsAvailable(id))
                {
                    Debug.LogWarning("[Urwald] Bodendecker nicht verfügbar, übersprungen: " + id);
                    continue;
                }

                try
                {
                    ScanModels.EnsureImported(id);
                    Mesh[] loaded = ScanModels.LoadMeshes(id);
                    if (loaded.Length == 0)
                    {
                        Debug.LogWarning("[Urwald] Bodendecker ohne Mesh, übersprungen: " + id);
                        continue;
                    }

                    Material mat = ScanModels.CreateFoliageMaterial(id);
                    if (mat == null)
                    {
                        Debug.LogWarning("[Urwald] Bodendecker-Material fehlt, übersprungen: " + id);
                        continue;
                    }
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);

                    meshes.Add(loaded[0]);
                    mats.Add(mat);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Urwald] Bodendecker fehlgeschlagen, übersprungen: " + id + " – " + e.Message);
                }
            }

            lib.groundCover = meshes.ToArray();
            lib.groundCoverMats = mats.ToArray();
        }

        /// <summary>MODELL1: prozeduraler Blender-Grasbueschel (30 Halme, V-Rinne, 780 Dreiecke).
        /// Laedt das FBX aus Plants/Gras1 (kopiert aus ArtSource falls dort aelter) und liefert das Mesh.</summary>
        private static Mesh LoadBlenderGras1()
        {
            const string artPath = "Assets/_Project/Art/Plants/Gras1/Gras1.fbx";
            const string srcPath = "ArtSource/Gras1/gras1_neu.fbx";
            if (!System.IO.File.Exists(artPath) && System.IO.File.Exists(srcPath))
            {
                AssetUtil.EnsureFolder("Assets/_Project/Art/Plants/Gras1");
                System.IO.File.Copy(srcPath, artPath, true);
                AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceUpdate);
            }
            // TEXTUR1: Read/Write fuer dieses eine Mesh aktivieren (UV-Check im Test).
            // Nur eine zusaetzliche CPU-Kopie (~30 KB), zur Laufzeit liest sie nichts.
            ModelImporter grim = AssetImporter.GetAtPath(artPath) as ModelImporter;
            if (grim != null && !grim.isReadable)
            {
                grim.isReadable = true;
                grim.SaveAndReimport();
            }
            if (!System.IO.File.Exists(artPath))
            {
                Debug.LogWarning("[Urwald] Gras1-FBX fehlt: " + artPath);
                return null;
            }
            Mesh found = null;
            int bestTris = 0;
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(artPath))
            {
                if (obj is Mesh mesh && mesh != null)
                {
                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
                    if (tris > bestTris) { bestTris = tris; found = mesh; }
                }
            }
            if (found == null || bestTris == 0)
            {
                Debug.LogWarning("[Urwald] Gras1 ohne Triangles: " + artPath);
                return null;
            }
            Debug.Log("[Urwald] Gras1 geladen: " + found.name + ", Dreiecke " + bestTris
                      + ", Groesse " + found.bounds.size.ToString("F3"));
            return found;
        }

        /// <summary>MODELL2: prozeduraler Seggen-Grasbueschel (20 breite ueberhaengende
        /// Blaetter, V-Rinne, 763 Dreiecke). Laedt das FBX aus Plants/Gras2 (kopiert aus
        /// ArtSource falls dort aelter) und liefert das Mesh.</summary>
        private static Mesh LoadBlenderGras2()
        {
            const string artPath = "Assets/_Project/Art/Plants/Gras2/Gras2.fbx";
            const string srcPath = "ArtSource/Gras2/gras2_neu.fbx";
            if (!System.IO.File.Exists(artPath) && System.IO.File.Exists(srcPath))
            {
                AssetUtil.EnsureFolder("Assets/_Project/Art/Plants/Gras2");
                System.IO.File.Copy(srcPath, artPath, true);
                AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceUpdate);
            }
            if (!System.IO.File.Exists(artPath))
            {
                Debug.LogWarning("[Urwald] Gras2-FBX fehlt: " + artPath);
                return null;
            }
            Mesh found = null;
            int bestTris = 0;
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(artPath))
            {
                if (obj is Mesh mesh && mesh != null)
                {
                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
                    if (tris > bestTris) { bestTris = tris; found = mesh; }
                }
            }
            if (found == null || bestTris == 0)
            {
                Debug.LogWarning("[Urwald] Gras2 ohne Triangles: " + artPath);
                return null;
            }
            Debug.Log("[Urwald] Gras2 geladen: " + found.name + ", Dreiecke " + bestTris
                      + ", Groesse " + found.bounds.size.ToString("F3"));
            return found;
        }

        /// <summary>MODELL3: prozedurale Unkraut-Rosette (12 gezaeckte Blaetter,
        /// Mittelrippe, 624 Dreiecke, 0.49 m Durchmesser). Laedt das FBX aus
        /// Plants/Unkraut (kopiert aus ArtSource falls dort aelter) und liefert das Mesh.</summary>
        private static Mesh LoadBlenderUnkraut()
        {
            const string artPath = "Assets/_Project/Art/Plants/Unkraut/Unkraut.fbx";
            const string srcPath = "ArtSource/Unkraut/unkraut_neu.fbx";
            if (!System.IO.File.Exists(artPath) && System.IO.File.Exists(srcPath))
            {
                AssetUtil.EnsureFolder("Assets/_Project/Art/Plants/Unkraut");
                System.IO.File.Copy(srcPath, artPath, true);
                AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceUpdate);
            }
            // MODELL3: Read/Write fuer dieses Mesh aktivieren (UV-Check im Test).
            ModelImporter uim = AssetImporter.GetAtPath(artPath) as ModelImporter;
            if (uim != null && !uim.isReadable)
            {
                uim.isReadable = true;
                uim.SaveAndReimport();
            }
            if (!System.IO.File.Exists(artPath))
            {
                Debug.LogWarning("[Urwald] Unkraut-FBX fehlt: " + artPath);
                return null;
            }
            Mesh found = null;
            int bestTris = 0;
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(artPath))
            {
                if (obj is Mesh mesh && mesh != null)
                {
                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
                    if (tris > bestTris) { bestTris = tris; found = mesh; }
                }
            }
            if (found == null || bestTris == 0)
            {
                Debug.LogWarning("[Urwald] Unkraut ohne Triangles: " + artPath);
                return null;
            }
            Debug.Log("[Urwald] Unkraut geladen: " + found.name + ", Dreiecke " + bestTris
                      + ", Groesse " + found.bounds.size.ToString("F3"));
            return found;
        }

        /// <summary>MODELL4: prozeduraler Immergruen-Bodendecker (Vinca-Typ:
        /// 3 Ranken mit je 4 gegenstaendigen Blattpaaren, Mitteldoppelrosette,
        /// 868 Dreiecke, 0.10 m hoch). Laedt das FBX aus Plants/Immergruen
        /// (kopiert aus ArtSource falls dort aelter) und liefert das Mesh.</summary>
        private static Mesh LoadBlenderImmergruen()
        {
            const string artPath = "Assets/_Project/Art/Plants/Immergruen/Immergruen.fbx";
            const string srcPath = "ArtSource/Immergruen/immergruen_neu.fbx";
            if (!System.IO.File.Exists(artPath) && System.IO.File.Exists(srcPath))
            {
                AssetUtil.EnsureFolder("Assets/_Project/Art/Plants/Immergruen");
                System.IO.File.Copy(srcPath, artPath, true);
                AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceUpdate);
            }
            // MODELL4: Read/Write fuer dieses Mesh aktivieren (UV-Check im Test).
            ModelImporter iim = AssetImporter.GetAtPath(artPath) as ModelImporter;
            if (iim != null && !iim.isReadable)
            {
                iim.isReadable = true;
                iim.SaveAndReimport();
            }
            if (!System.IO.File.Exists(artPath))
            {
                Debug.LogWarning("[Urwald] Immergruen-FBX fehlt: " + artPath);
                return null;
            }
            Mesh found = null;
            int bestTris = 0;
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(artPath))
            {
                if (obj is Mesh mesh && mesh != null)
                {
                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
                    if (tris > bestTris) { bestTris = tris; found = mesh; }
                }
            }
            if (found == null || bestTris == 0)
            {
                Debug.LogWarning("[Urwald] Immergruen ohne Triangles: " + artPath);
                return null;
            }
            Debug.Log("[Urwald] Immergruen geladen: " + found.name + ", Dreiecke " + bestTris
                      + ", Groesse " + found.bounds.size.ToString("F3"));
            return found;
        }

        /// <summary>MODELL5: prozedurale Brennnessel (4-kantiger Stengel, 5 gegen-
        /// staendige Blattpaare + Endpaar, gezaeckte herzfoermige Spreiten,
        /// 766 Dreiecke, 0.74 m). Laedt das FBX aus Plants/Brennnessel
        /// (kopiert aus ArtSource falls dort aelter) und liefert das Mesh.</summary>
        private static Mesh LoadBlenderBrennnessel()
        {
            const string artPath = "Assets/_Project/Art/Plants/Brennnessel/Brennnessel.fbx";
            const string srcPath = "ArtSource/Brennnessel/brennnessel_neu.fbx";
            if (System.IO.File.Exists(srcPath) &&
                (!System.IO.File.Exists(artPath) ||
                 System.IO.File.GetLastWriteTimeUtc(srcPath) > System.IO.File.GetLastWriteTimeUtc(artPath)))
            {
                AssetUtil.EnsureFolder("Assets/_Project/Art/Plants/Brennnessel");
                System.IO.File.Copy(srcPath, artPath, true);
                AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceUpdate);
            }
            // MODELL5: Read/Write fuer dieses Mesh aktivieren (UV-Check im Test).
            ModelImporter bim = AssetImporter.GetAtPath(artPath) as ModelImporter;
            if (bim != null && !bim.isReadable)
            {
                bim.isReadable = true;
                bim.SaveAndReimport();
            }
            if (!System.IO.File.Exists(artPath))
            {
                Debug.LogWarning("[Urwald] Brennnessel-FBX fehlt: " + artPath);
                return null;
            }
            Mesh found = null;
            int bestTris = 0;
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(artPath))
            {
                if (obj is Mesh mesh && mesh != null)
                {
                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
                    if (tris > bestTris) { bestTris = tris; found = mesh; }
                }
            }
            if (found == null || bestTris == 0)
            {
                Debug.LogWarning("[Urwald] Brennnessel ohne Triangles: " + artPath);
                return null;
            }
            Debug.Log("[Urwald] Brennnessel geladen: " + found.name + ", Dreiecke " + bestTris
                      + ", Groesse " + found.bounds.size.ToString("F3"));
            return found;
        }

        /// <summary>MODELL18: prozedurale Blume (Vinca-Typ: 5 windradige
        /// Kronblaetter mit Augenscheibe und 5 Staubblaettern je Bluetenstand,
        /// 3 leicht geneigte Stengel mit lanzettlichen Blaettern; 834 Dreiecke,
        /// 0.27 m, EIN Slot, UV-Atlas 8x5). Laedt das FBX aus Plants/Blume
        /// (kopiert aus ArtSource falls dort aelter) und liefert das Mesh.</summary>
        private static Mesh LoadBlenderBlume()
        {
            const string artPath = "Assets/_Project/Art/Plants/Blume/Blume.fbx";
            const string srcPath = "ArtSource/Blume/blume_neu.fbx";
            if (!System.IO.File.Exists(artPath) && System.IO.File.Exists(srcPath))
            {
                AssetUtil.EnsureFolder("Assets/_Project/Art/Plants/Blume");
                System.IO.File.Copy(srcPath, artPath, true);
                AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceUpdate);
            }
            // Read/Write fuer UV-Check im Test (eine CPU-Kopie, ~25 KB).
            ModelImporter bimp = AssetImporter.GetAtPath(artPath) as ModelImporter;
            if (bimp != null && !bimp.isReadable)
            {
                bimp.isReadable = true;
                bimp.SaveAndReimport();
            }
            if (!System.IO.File.Exists(artPath))
            {
                Debug.LogWarning("[Urwald] Blume-FBX fehlt: " + artPath);
                return null;
            }
            Mesh found = null;
            int bestTris = 0;
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(artPath))
            {
                if (obj is Mesh mesh && mesh != null)
                {
                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
                    if (tris > bestTris) { bestTris = tris; found = mesh; }
                }
            }
            if (found == null || bestTris == 0)
            {
                Debug.LogWarning("[Urwald] Blume ohne Triangles: " + artPath);
                return null;
            }
            Debug.Log("[Urwald] Blume geladen: " + found.name + ", Dreiecke " + bestTris
                      + ", Groesse " + found.bounds.size.ToString("F3"));
            return found;
        }

        /// <summary>MODELL7: Schwertfarn Farn_Tropisch (aufrechte Rosette aus 8
        /// bogigen Wedeln mit je 25 Fiederpaaren, 2 junge Krumme, Rhizom-Kegel;
        /// 2964 Dreiecke, 0.87 m, EIN Slot). Laedt das FBX aus Plants/Farn_Tropisch
        /// (kopiert aus ArtSource falls dort aelter) und liefert das Mesh.</summary>
        private static Mesh LoadBlenderFarnTropisch()
        {
            const string artPath = "Assets/_Project/Art/Plants/Farn_Tropisch/Farn_Tropisch.fbx";
            const string srcPath = "ArtSource/Farn_Tropisch/farn_tropisch_neu.fbx";
            if (System.IO.File.Exists(srcPath) &&
                (!System.IO.File.Exists(artPath) ||
                 System.IO.File.GetLastWriteTimeUtc(srcPath) > System.IO.File.GetLastWriteTimeUtc(artPath)))
            {
                AssetUtil.EnsureFolder("Assets/_Project/Art/Plants/Farn_Tropisch");
                System.IO.File.Copy(srcPath, artPath, true);
                AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceUpdate);
            }
            // MODELL7: Read/Write fuer dieses Mesh aktivieren (UV-Check im Test).
            ModelImporter fmi = AssetImporter.GetAtPath(artPath) as ModelImporter;
            if (fmi != null && !fmi.isReadable)
            {
                fmi.isReadable = true;
                fmi.SaveAndReimport();
            }
            if (!System.IO.File.Exists(artPath))
            {
                Debug.LogWarning("[Urwald] Farn_Tropisch-FBX fehlt: " + artPath);
                return null;
            }
            Mesh found = null;
            int bestTris = 0;
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(artPath))
            {
                if (obj is Mesh mesh && mesh != null)
                {
                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
                    if (tris > bestTris) { bestTris = tris; found = mesh; }
                }
            }
            if (found == null || bestTris == 0)
            {
                Debug.LogWarning("[Urwald] Farn_Tropisch ohne Triangles: " + artPath);
                return null;
            }
            Debug.Log("[Urwald] Farn_Tropisch geladen: " + found.name + ", Dreiecke " + bestTris
                      + ", Groesse " + found.bounds.size.ToString("F3"));
            return found;
        }

        /// <summary>MODELL7: MAT_FernTropisch auf den Blender-Neubau umstellen:
        /// echte Geometrie statt Scan-Cards -> opakes Material (kein Cutout),
        /// Albedo/Normal aus dem 8x4-Zellen-Atlas, weisser Basaton.</summary>
        private static void FernTropischOpaque(Material m)
        {
            m.SetFloat("_SurfaceType", 0f);
            m.SetFloat("_AlphaCutoffEnable", 0f);
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_DoubleSidedEnable", 1f);
            m.SetFloat("_DoubleSidedNormalMode", 2f);
            SetPHTexture(m, "Assets/_Project/Art/Plants/Farn_Tropisch/Textures/FarnTropisch_Albedo.png", true);
            SetPHTexture(m, "Assets/_Project/Art/Plants/Farn_Tropisch/Textures/FarnTropisch_Normal.png", false);
            m.color = Color.white;
            m.SetColor("_BaseColor", Color.white);
            m.enableInstancing = true;
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
        }

        /// <summary>MODELL8/9: generischer Lader fuer Blender-Busch-FBX (Busch1/Busch2).
        /// Laedt das FBX aus Plants/<label> (kopiert aus ArtSource falls dort neuer), misst
        /// den Blatt-Submesh anhand der Bounds (Blatt sitzt hoeher als Ast) per out.</summary>
        private static Mesh LoadBlenderBusch(out int leafSubmesh, string artPath, string srcPath, string label)
        {
            leafSubmesh = 1;
            if (System.IO.File.Exists(srcPath) &&
                (!System.IO.File.Exists(artPath) ||
                 System.IO.File.GetLastWriteTimeUtc(srcPath) > System.IO.File.GetLastWriteTimeUtc(artPath)))
            {
                AssetUtil.EnsureFolder(System.IO.Path.GetDirectoryName(artPath).Replace('\\', '/'));
                System.IO.File.Copy(srcPath, artPath, true);
                AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceUpdate);
            }
            ModelImporter bimp = AssetImporter.GetAtPath(artPath) as ModelImporter;
            if (bimp != null && !bimp.isReadable)
            {
                bimp.isReadable = true;
                bimp.SaveAndReimport();
            }
            if (!System.IO.File.Exists(artPath))
            {
                Debug.LogWarning("[Urwald] " + label + "-FBX fehlt: " + artPath);
                return null;
            }
            Mesh found = null;
            int bestTris = 0;
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(artPath))
            {
                if (obj is Mesh mesh && mesh != null)
                {
                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
                    if (tris > bestTris) { bestTris = tris; found = mesh; }
                }
            }
            if (found == null || bestTris == 0)
            {
                Debug.LogWarning("[Urwald] " + label + " ohne Triangles: " + artPath);
                return null;
            }
            // Blatt-Submesh messen: der Blatthugel sitzt deutlich hoeher als das Astgeruest.
            if (found.subMeshCount >= 2)
            {
                Bounds b0 = found.GetSubMesh(0).bounds;
                Bounds b1 = found.GetSubMesh(1).bounds;
                leafSubmesh = (b1.center.y > b0.center.y) ? 1 : 0;
            }
            Debug.Log("[Urwald] Busch1 geladen: " + found.name + ", Dreiecke " + bestTris
                      + ", Groesse " + found.bounds.size.ToString("F3")
                      + ", Blatt-Submesh " + leafSubmesh);
            return found;
        }

        private static Mesh LoadBlenderBusch1(out int leafSubmesh)
            => LoadBlenderBusch(out leafSubmesh,
                "Assets/_Project/Art/Plants/Busch1/Busch1.fbx", "ArtSource/Busch1/busch1_neu.fbx", "Busch1");

        /// <summary>MODELL11: Schilf-Horst aus Blender (Einzelobjekt, ein Slot MAT_Reed,
        /// UV-Atlas). Liefert das Mesh mit den meisten Dreiecken; fehlt die Quelle, null.</summary>
        private static Mesh LoadBlenderSchilf()
        {
            const string artPath = "Assets/_Project/Art/Plants/Schilf/Schilf.fbx";
            const string srcPath = "ArtSource/Schilf/schilf_neu.fbx";
            if (System.IO.File.Exists(srcPath) &&
                (!System.IO.File.Exists(artPath) ||
                 System.IO.File.GetLastWriteTimeUtc(srcPath) > System.IO.File.GetLastWriteTimeUtc(artPath)))
            {
                AssetUtil.EnsureFolder(System.IO.Path.GetDirectoryName(artPath).Replace('\\', '/'));
                System.IO.File.Copy(srcPath, artPath, true);
                AssetDatabase.ImportAsset(artPath, ImportAssetOptions.ForceUpdate);
            }
            ModelImporter simp = AssetImporter.GetAtPath(artPath) as ModelImporter;
            if (simp != null && !simp.isReadable)
            {
                simp.isReadable = true;
                simp.SaveAndReimport();
            }
            if (!System.IO.File.Exists(artPath))
            {
                Debug.LogWarning("[Urwald] MODELL11: Schilf-FBX fehlt: " + artPath);
                return null;
            }
            Mesh found = null;
            int bestTris = 0;
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(artPath))
            {
                if (obj is Mesh mesh && mesh != null)
                {
                    int tris = 0;
                    for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
                    if (tris > bestTris) { bestTris = tris; found = mesh; }
                }
            }
            if (found == null || bestTris == 0)
            {
                Debug.LogWarning("[Urwald] MODELL11: Schilf ohne Triangles: " + artPath);
                return null;
            }
            Debug.Log("[Urwald] Schilf geladen: " + found.name + ", Dreiecke " + bestTris
                      + ", Groesse " + found.bounds.size.ToString("F3"));
            return found;
        }

        /// <summary>MODELL8: MAT_Busch1_Leaf — opakes Blatt-Material (echte Geometrie,
        /// kein Cutout) mit Atlas-Albedo/Normal, DoubleSided Flip wie Grundfarn.</summary>
        private static Material CreateBusch1LeafMat()
            => CreateBuschAtlasMat("MAT_Busch1_Leaf", "Assets/_Project/Art/Plants/Busch1/Textures/",
                "Busch1_Albedo.png", "Busch1_Normal.png", 0.15f);

        /// <summary>MODELL8/9: opakes Busch-Material (Blatt oder Ast) mit eigener
        /// Atlas-Albedo/Normal; echte Geometrie statt Cutout, DoubleSided Flip wie
        /// Grundfarn (Q2: Mirror macht Rueckseite schwarz), Instancing an.</summary>
        private static Material CreateBuschAtlasMat(string matName, string texFolder,
            string albedoName, string normalName, float smoothness)
        {
            Shader hdrpLit = Shader.Find("HDRP/Lit");
            if (hdrpLit == null) { Debug.LogWarning("[Urwald] HDRP/Lit nicht gefunden."); return null; }
            Material m = GetOrCreateMat(matName, hdrpLit, Color.white);
            m.SetFloat("_SurfaceType", 0f);
            m.SetFloat("_AlphaCutoffEnable", 0f);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_DoubleSidedEnable", 1f);
            m.SetFloat("_DoubleSidedNormalMode", 2f); // Flip (Q2: Mirror macht Rueckseite schwarz)
            SetPHTexture(m, texFolder + albedoName, true);
            SetPHTexture(m, texFolder + normalName, false);
            m.SetColor("_BaseColor", Color.white);
            m.enableInstancing = true;
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>MODELL8: MAT_Busch1_Branch — opakes Ast-Material (Rindenband der
        ///selben Atlas-Textur), DoubleSided an (duenne, offene Rohrenden).</summary>
        private static Material CreateBusch1BranchMat()
            => CreateBuschAtlasMat("MAT_Busch1_Branch", "Assets/_Project/Art/Plants/Busch1/Textures/",
                "Busch1_Albedo.png", "Busch1_Normal.png", 0.2f);

        /// <summary>A1/MODELL9+10: Busche-Varianten-Meshes. Variante 0 ist der Busch2-Neubau
        /// (Blender), Variante 1 der Busch3-Neubau; fehlt ein Neubau, bleibt der jeweilige
        /// Scan (shrub_03/shrub_04) als Fallback stehen.</summary>
        private static Mesh[] LoadBushVariantMeshes(Mesh busch2, Mesh busch3)
        {
            var meshes = new System.Collections.Generic.List<Mesh>();
            if (busch2 != null) meshes.Add(busch2);
            if (busch3 != null) meshes.Add(busch3);
            string[] fallback = busch3 != null
                ? (busch2 != null ? new string[0] : new[] { "shrub_03" })
                : (busch2 != null ? new[] { "shrub_04" } : new[] { "shrub_03", "shrub_04" });
            foreach (string id in fallback)
            {
                if (!ScanModels.IsAvailable(id))
                {
                    Debug.LogWarning("[Urwald] Busche-Variante nicht verfügbar: " + id);
                    continue;
                }
                ScanModels.EnsureImported(id);
                Mesh[] loaded = ScanModels.LoadMeshes(id);
                if (loaded.Length == 0)
                {
                    Debug.LogWarning("[Urwald] Busche-Variante ohne Mesh: " + id);
                    continue;
                }
                meshes.Add(loaded[0]);
            }
            return meshes.ToArray();
        }

        /// <summary>A1/MODELL9+10: Materialien fuer die Busche-Varianten, indexgleich zu
        /// LoadBushVariantMeshes: Variante 0 = Busch2-Blatt-Material (Neubau),
        /// Variante 1 = Busch3-Blatt-Material (Neubau); sonst gruen getoente Scans.</summary>
        private static Material[] CreateBushVariantMats(Material busch2Leaf, Material busch3Leaf)
        {
            var mats = new System.Collections.Generic.List<Material>();
            if (busch2Leaf != null) mats.Add(busch2Leaf);
            if (busch3Leaf != null) mats.Add(busch3Leaf);
            string[] fallback = busch3Leaf != null
                ? (busch2Leaf != null ? new string[0] : new[] { "shrub_03" })
                : (busch2Leaf != null ? new[] { "shrub_04" } : new[] { "shrub_03", "shrub_04" });
            foreach (string id in fallback)
            {
                Material src = ScanMaterial("Assets/_Project/Art/Scans/Materials/M_" + id + ".mat", null);
                if (src == null)
                {
                    Debug.LogWarning("[Urwald] Busche-Variante ohne Scan-Material: " + id);
                    continue;
                }
                mats.Add(TropischMaterial(src, "MAT_" + id + "Tropisch"));
            }
            return mats.ToArray();
        }

        /// <summary>WALD7: MAT_Moos — ONE shared Alpha-Cutout Material (hellgruen T_Moos with v-gradient
        /// alpha). Shells and cards all point at this single material; no per-object mats.</summary>
        private static Material MoosMaterial()
        {
            string path = MatFolder + "MAT_Moos.mat";
            Texture2D albedo = TextureFactory.Moos();
            Material fresh = Spinnenspiel.Rendering.PipelineMaterials.Lit(WaldLook.MoosColor, albedo);
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(fresh, 0.15f); // matte like bark
            Spinnenspiel.Rendering.PipelineMaterials.SetAlphaClip(fresh, 0.5f);   // cutout shape from texture
            Spinnenspiel.Rendering.PipelineMaterials.SetDoubleSided(fresh, true);
            fresh.SetFloat("_DoubleSidedNormalMode", 2f); // Flip (see Q2: Mirror lit backside black)
            fresh.SetVector("_DoubleSidedConstants", new Vector4(1f, 1f, 1f, 0f));
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.CopyPropertiesFromMaterial(fresh);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }
            AssetUtil.EnsureFolder("Assets/_Project/Art/Generated/Materials");
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        private static Material SaveMat(string name, Material fresh)
        {
            string path = MatFolder + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.CopyPropertiesFromMaterial(fresh);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }
            AssetUtil.EnsureFolder("Assets/_Project/Art/Generated/Materials");
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        /// <summary>WALD4: rotbraune Mammutbaum-Rinde mit Normalmap, deckend, matt.</summary>
        private static Material RedwoodMaterial()
        {
            Material m = Spinnenspiel.Rendering.PipelineMaterials.Lit(Color.white, TextureFactory.RedwoodBark());
            Spinnenspiel.Rendering.PipelineMaterials.SetNormalMap(m, TextureFactory.RedwoodBarkNormal(), 1.4f);
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(m, 0.08f);
            return SaveMat("MAT_Redwood", m);
        }

        /// <summary>WALD5: Dach-Blaetter, Alpha-Cutout, doppelseitig.</summary>
        private static Material CanopyMaterial()
        {
            Material m = Spinnenspiel.Rendering.PipelineMaterials.Lit(Color.white, TextureFactory.CanopyLeaves());
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(m, 0.1f);
            Spinnenspiel.Rendering.PipelineMaterials.SetAlphaClip(m, 0.5f);
            Spinnenspiel.Rendering.PipelineMaterials.SetDoubleSided(m, true);
            m.SetFloat("_DoubleSidedNormalMode", 2f);
            m.SetVector("_DoubleSidedConstants", new Vector4(1f, 1f, 1f, 0f));
            return SaveMat("MAT_Canopy", m);
        }

        /// <summary>KLEE1: heller Klee-Teppich — Alpha-Cutout-Blatttextur, doppelseitig (Flip), matt.</summary>
        private static Material CloverMaterial()
        {
            Material m = Spinnenspiel.Rendering.PipelineMaterials.Lit(Color.white, TextureFactory.CloverLeaf());
            Spinnenspiel.Rendering.PipelineMaterials.SetSmoothness(m, 0.12f);
            Spinnenspiel.Rendering.PipelineMaterials.SetAlphaClip(m, 0.5f);
            Spinnenspiel.Rendering.PipelineMaterials.SetDoubleSided(m, true);
            m.SetFloat("_DoubleSidedNormalMode", 2f);
            m.SetVector("_DoubleSidedConstants", new Vector4(1f, 1f, 1f, 0f));
            return SaveMat("MAT_Klee", m);
        }

        /// <summary>Terrain-Material (HDRP/TerrainLit) als echtes Asset, das die Bibliothek referenziert. Ohne dieses Asset loest sich
        /// terrain.materialTemplate im gebauten Spiel zu "Hidden/InternalErrorShader" auf (Boden unsichtbar, Alex 2026-10-21).</summary>
        private static Material TerrainMaterial()
        {
            string path = MatFolder + "MAT_UrwaldTerrain.mat";
            Shader sh = Shader.Find("HDRP/TerrainLit");
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // Varianten mit Normal-/Maskenkarte muessen im Build vorhanden sein (sonst gestrippt).
                existing.EnableKeyword("_NORMALMAP");
                existing.EnableKeyword("_MASKMAP");
                EditorUtility.SetDirty(existing);
                return existing;
            }
            if (sh == null)
            {
                Debug.LogError("[Terrain] Shader HDRP/TerrainLit nicht gefunden.");
                return null;
            }
            Material m = new Material(sh) { name = "MAT_UrwaldTerrain" };
            m.EnableKeyword("_NORMALMAP");
            m.EnableKeyword("_MASKMAP");
            AssetUtil.EnsureFolder("Assets/_Project/Art/Generated/Materials");
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material TropischMaterial(Material src, string name)
        {
            if (src == null)
            {
                return null;
            }
            string path = "Assets/_Project/Art/Generated/Materials/" + name + ".mat";
            AssetUtil.EnsureFolder("Assets/_Project/Art/Generated/Materials");
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Color tint = new Color(0.55f, 0.95f, 0.40f, 1f);
            if (existing != null)
            {
                existing.CopyPropertiesFromMaterial(src);
                existing.color = tint;
                NormalizeDoubleSidedFlip(existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                return existing;
            }
            Material copy = new Material(src) { name = name };
            copy.color = tint;
            NormalizeDoubleSidedFlip(copy);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        /// <summary>
        /// Q2: Normalen-Modus der Laubseiten eingebeidseitig Flip (mode 2) anstatt Mirror
        /// (mode 1) abfangen. Mirror lit die Blatt-Rueckside normal-inward -> tiefschwarz.
        /// Only touches materials that are already double-sided enabled.
        /// </summary>
        private static void NormalizeDoubleSidedFlip(Material m)
        {
            if (m == null || m.GetFloat("_DoubleSidedEnable") < 0.5f)
            {
                return;
            }
            m.SetFloat("_DoubleSidedNormalMode", 2f); // 2 = Flip
            m.SetVector("_DoubleSidedConstants", new Vector4(1f, 1f, 1f, 0f));
        }

        // Q4 Schilf: MAT_Reed wirkt cremeweiss/bleached in der Galerie (Schilf_vorne/Schilf_seite)
        // -> gellich-gruen (HSV H~70°, S~0.6, V~0.42) und matte (Smoothness 0.12 statt 0.35,
        // die Specular-Highlights bleached die Blatte unter der Galerie-Licht).
        private static Material ReedMaterial(Material reed)
        {
            if (reed == null)
            {
                Debug.LogWarning("[Urwald] MAT_Reed fehlt, Schilf-Ton nicht adapt.");
                return reed;
            }
            reed.SetColor("_BaseColor", new Color(ReedBaseR, ReedBaseG, ReedBaseB, 1f));
            reed.SetFloat("_Smoothness", ReedSmoothness);
            // MODELL11: UV-Atlas-Textur (Halm-/Blatt-/Kolben-Baender) auf die neue
            // Blender-Form. Der Q4-Ton bleibt als Multiplikator erhalten, deshalb ist
            // die Textur selbst heller (~0.6-0.85) ausgelegt.
            const string texDir = "Assets/_Project/Art/Plants/Schilf/Textures/";
            if (System.IO.File.Exists(texDir + "Schilf_Albedo.png"))
            {
                SetPHTexture(reed, texDir + "Schilf_Albedo.png", true);
                SetPHTexture(reed, texDir + "Schilf_Normal.png", false);
            }
            else
            {
                Debug.LogWarning("[Urwald] MODELL11: Schilf-Atlas fehlt, MAT_Reed bleibt einfarbig: " + texDir);
            }
            HDMaterial.ValidateMaterial(reed);
            EditorUtility.SetDirty(reed);
            return reed;
        }

        private static Material ScanMaterial(string path, Material fallback)
        {
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning("[Urwald] Scan-Material fehlt, prozedurales bleibt: " + path);
                return fallback;
            }
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogWarning("[Urwald] Scan-Material nicht lesbar, prozedurales bleibt: " + path);
                return fallback;
            }
            return mat;
        }

        private static Mesh ScanMesh(string fbxPath, Mesh fallback)
        {
            if (!System.IO.File.Exists(fbxPath))
            {
                Debug.LogWarning("[Urwald] Scan-Modell fehlt, prozedurales bleibt: " + fbxPath);
                return fallback;
            }
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (obj is Mesh mesh)
                    return mesh;
            }
            Debug.LogWarning("[Urwald] Kein Mesh im Scan-Modell: " + fbxPath);
            return fallback;
        }

        private static TerrainLayer[] ScanLayers(string[] paths, string[] fallbackNames)
        {
            TerrainLayer[] result = new TerrainLayer[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                TerrainLayer layer = null;
                if (System.IO.File.Exists(paths[i]))
                    layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(paths[i]);
                if (layer == null)
                {
                    Debug.LogWarning("[Urwald] Scan-TerrainLayer fehlt, prozeduraler bleibt: " + paths[i]);
                    layer = LoadLayers(new[] { fallbackNames[i] })[0];
                }
                result[i] = layer;
            }
            return result;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }
    }
}
