using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Milestone 4: Wald und Wiese.
    /// Kleinvegetation (Gras, Blumen, Farne, Laub, Büsche) läuft über das instanzierte VegetationSystem.
    /// Bäume sind echte GameObjects mit LODGroup und Stamm-Kollider, weil die Spinne mit ihnen interagiert.
    /// </summary>
    public static class VegetationBuilder
    {
        private const string MaterialFolder = AssetUtil.GeneratedRoot + "/Materials";

        public static void Build(Terrain terrain, int seed)
        {
            Shader foliage = Shader.Find("HDRP/Lit");
            if (foliage == null)
            {
                Debug.LogError("[Spinnenspiel] Shader 'HDRP/Lit' nicht gefunden – Vegetation wird nicht gebaut.");
                return;
            }

            Texture2D leafAtlas = TextureFactory.LeafCluster();

            Material grass = FoliageMaterial("MAT_Grass", null, new Color(0.20f, 0.33f, 0.10f));
            Material grassDry = FoliageMaterial("MAT_GrassDry", null, new Color(0.33f, 0.33f, 0.14f));
            Material flower = FoliageMaterial("MAT_Flower", null, Color.white);
            Material fern = FoliageMaterial("MAT_Fern", null, new Color(0.14f, 0.28f, 0.08f));
            Material litter = FoliageMaterial("MAT_Litter", null, Color.white);
            Material leaves = FoliageMaterial("MAT_Leaves", leafAtlas, new Color(0.75f, 0.85f, 0.7f));
            Material reed = FoliageMaterial("MAT_Reed", null, new Color(0.25f, 0.33f, 0.14f));

            Mesh grassFine = VegetationMeshFactory.GrassClump("VM_GrassFine", 16, 0.025f, 0.07f, 7);
            Mesh grassA = VegetationMeshFactory.GrassClump("VM_GrassA", 10, 0.06f, 0.13f, 1);
            Mesh grassB = VegetationMeshFactory.GrassClump("VM_GrassB", 12, 0.1f, 0.22f, 2);
            Mesh reedMesh = VegetationMeshFactory.GrassClump("VM_Reed", 7, 0.35f, 0.6f, 3);
            Mesh flowerWhite = VegetationMeshFactory.Flower("VM_FlowerWhite", new Color(0.95f, 0.95f, 0.92f), new Color(0.95f, 0.8f, 0.2f), 0.16f, 8, 1);
            Mesh flowerYellow = VegetationMeshFactory.Flower("VM_FlowerYellow", new Color(0.98f, 0.82f, 0.15f), new Color(0.8f, 0.55f, 0.1f), 0.2f, 5, 2);
            Mesh flowerViolet = VegetationMeshFactory.Flower("VM_FlowerViolet", new Color(0.55f, 0.35f, 0.75f), new Color(0.95f, 0.9f, 0.4f), 0.13f, 6, 3);
            Mesh fernMesh = VegetationMeshFactory.Fern("VM_Fern", 7, 0.32f, 4);
            Mesh litterMesh = VegetationMeshFactory.LeafLitter("VM_Litter", 14, 5);
            Mesh bushMesh = VegetationMeshFactory.LeafCards("VM_Bush", 70, new Vector3(0.45f, 0.35f, 0.45f), new Vector3(0f, 0.32f, 0f), 0.13f, 6);

            List<VegetationLayer> layers = new List<VegetationLayer>
            {
                Layer("Gras kurz", grassA, grass, 14f, VegetationBiome.Meadow, 0.8f, 1.3f, 22f, false, 11),
                Layer("Gras hoch", grassB, grass, 3f, VegetationBiome.Meadow, 0.8f, 1.4f, 26f, false, 12),
                Layer("Gras trocken", grassA, grassDry, 2f, VegetationBiome.Meadow, 0.9f, 1.5f, 22f, false, 13),
                Layer("Waldgras", grassA, grass, 1.5f, VegetationBiome.Forest, 0.6f, 1.0f, 18f, false, 14)
            };

            // Gescannte Pflanzen (echte Größe, deshalb nur Skalierung 0.8–1.2) ersetzen die prozeduralen;
            // fehlt der Scan, bleibt die bisherige prozedurale Pflanze.
            if (!AddScanLayers(layers, "periwinkle_plant", 0.35f, VegetationBiome.Meadow, 0.8f, 1.2f, 24f, false, 15))
            {
                layers.Add(Layer("Blume weiß", flowerWhite, flower, 0.35f, VegetationBiome.Meadow, 0.8f, 1.2f, 24f, false, 15));
            }

            if (!AddScanLayers(layers, "dandelion_01", 0.25f, VegetationBiome.Meadow, 0.8f, 1.2f, 24f, false, 16))
            {
                layers.Add(Layer("Blume gelb", flowerYellow, flower, 0.25f, VegetationBiome.Meadow, 0.8f, 1.2f, 24f, false, 16));
            }

            if (!AddScanLayers(layers, "weed_plant_02", 0.2f, VegetationBiome.Meadow, 0.8f, 1.2f, 24f, false, 17))
            {
                layers.Add(Layer("Blume violett", flowerViolet, flower, 0.2f, VegetationBiome.Meadow, 0.8f, 1.2f, 24f, false, 17));
            }

            if (!AddScanLayers(layers, "fern_02", 0.6f, VegetationBiome.Forest, 0.8f, 1.2f, 30f, true, 18))
            {
                layers.Add(Layer("Farn", fernMesh, fern, 0.6f, VegetationBiome.Forest, 0.7f, 1.5f, 30f, true, 18));
            }

            layers.Add(Layer("Laub", litterMesh, litter, 2.5f, VegetationBiome.Forest, 0.8f, 1.3f, 20f, false, 19));
            layers.Add(Layer("Schilf", reedMesh, reed, 3f, VegetationBiome.WaterBank, 0.8f, 1.3f, 30f, true, 20));

            if (!AddScanLayers(layers, "shrub_02", 0.05f, VegetationBiome.Any, 0.8f, 1.2f, 45f, true, 21))
            {
                layers.Add(Layer("Busch", bushMesh, leaves, 0.05f, VegetationBiome.Any, 0.8f, 1.8f, 45f, true, 21));
            }

            layers.Add(Layer("Rasen", grassFine, grass, 30f, VegetationBiome.Meadow, 0.8f, 1.4f, 13f, false, 22));
            layers.Add(Layer("Moos/Waldrasen", grassFine, fern, 8f, VegetationBiome.Forest, 0.6f, 1.1f, 12f, false, 23));

            // Neu: weitere gescannte Kräuter und Büsche (je halbe Dichte der ersetzten Pflanze, gleiche Biome).
            AddScanLayers(layers, "nettle_plant", 0.3f, VegetationBiome.Forest, 0.8f, 1.2f, 30f, true, 30);
            AddScanLayers(layers, "shrub_03", 0.025f, VegetationBiome.Any, 0.8f, 1.2f, 45f, true, 31);
            AddScanLayers(layers, "shrub_04", 0.025f, VegetationBiome.Any, 0.8f, 1.2f, 45f, true, 32);

            // Scan-Layer verschieben die Indizes, deshalb nach Name statt Index einstellen:
            // Uferpflanzen dürfen näher ans Wasser, Büsche (auch Scan-Büsche) nicht in den Bach.
            WaterClearanceOf(layers, "Schilf", -0.1f);
            WaterClearanceOf(layers, "Busch", 1.2f);
            WaterClearanceOf(layers, "Scan shrub_0", 1.2f);

            // Farne (Scan oder prozedural) liegen flach am Hang an.
            foreach (VegetationLayer layer in layers)
            {
                if (layer.name == "Farn" || layer.name.StartsWith("Scan fern_02", System.StringComparison.Ordinal))
                {
                    layer.alignToNormal = true;
                    layer.sinkDepth = 0f;
                }
            }

            GameObject vegetation = new GameObject("Vegetation");
            vegetation.AddComponent<VegetationSystem>().Configure(terrain, layers);

            BuildTrees(terrain, seed, leaves);
        }

        private static void BuildTrees(Terrain terrain, int seed, Material leaves)
        {
            Material bark = AssetUtil.CreateLitMaterial(MaterialFolder + "/MAT_TreeBark.mat", Color.white, 0.08f, TextureFactory.Bark(), TextureFactory.BarkNormal());

            const int variants = 3;
            Mesh[] trunks = new Mesh[variants];
            Mesh[] crownsNear = new Mesh[variants];
            Mesh[] crownsFar = new Mesh[variants];
            for (int v = 0; v < variants; v++)
            {
                float height = 7f + v * 1.5f;
                List<Vector3> tips;
                trunks[v] = VegetationMeshFactory.Trunk("VM_Trunk" + v, height, 0.22f + 0.05f * v, 5 + v, 30 + v, out tips);
                tips.Add(Vector3.up * height);
                Vector3 crownRadii = new Vector3(1.4f, 1.1f, 1.4f);
                crownsNear[v] = VegetationMeshFactory.LeafCards("VM_Crown" + v, 260, crownRadii, Vector3.zero, 0.55f, 40 + v, tips);
                crownsFar[v] = VegetationMeshFactory.LeafCards("VM_CrownFar" + v, 70, crownRadii, Vector3.zero, 1.0f, 50 + v, tips);
            }

            Transform root = new GameObject("Trees").transform;
            System.Random random = new System.Random(seed);
            int placed = 0;
            for (int attempt = 0; attempt < 2000 && placed < 38; attempt++)
            {
                float x = 3f + (float)random.NextDouble() * (WorldLayout.Size - 6f);
                float z = 3f + (float)random.NextDouble() * (WorldLayout.Size - 6f);
                float forest = WorldLayout.ForestFactor(x, z);
                bool meadowTree = forest < 0.2f && random.NextDouble() < 0.02f;
                if ((forest < 0.6f && !meadowTree) || WorldLayout.Wetness(x, z) > 0.05f)
                {
                    continue;
                }

                if (new Vector2(x - WorldLayout.SpawnPoint.x, z - WorldLayout.SpawnPoint.z).magnitude < 5f || TooClose(root, x, z, 3.2f))
                {
                    continue;
                }

                int v = random.Next(0, variants);
                float scale = 0.8f + (float)random.NextDouble() * 0.45f;
                // FIX: + terrain.transform.position.y, konsistent mit EnvironmentBuilder/HorizonBuilder/TestWorldBuilder.
                float height = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y + 0.15f;
                CreateTree(root, new Vector3(x, height, z), (float)random.NextDouble() * 360f, scale, trunks[v], crownsNear[v], crownsFar[v], bark, leaves);
                placed++;
            }
        }

        private static void CreateTree(Transform parent, Vector3 position, float yaw, float scale, Mesh trunk, Mesh crownNear, Mesh crownFar, Material bark, Material leaves)
        {
            GameObject tree = new GameObject("Tree");
            tree.transform.SetParent(parent, false);
            tree.transform.position = position;
            tree.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            tree.transform.localScale = Vector3.one * scale;

            MeshRenderer trunkRenderer = AddMesh(tree.transform, "Trunk", trunk, bark);
            MeshRenderer crownNearRenderer = AddMesh(tree.transform, "CrownLOD0", crownNear, leaves);
            MeshRenderer crownFarRenderer = AddMesh(tree.transform, "CrownLOD1", crownFar, leaves);

            // MeshCollider folgt dem sichtbaren, sich verjüngenden Stamm – die Spinne kann nicht in die Kuppe einer festen Kapsel laufen.
            MeshCollider trunkCollider = trunkRenderer.gameObject.AddComponent<MeshCollider>();
            trunkCollider.sharedMesh = trunk;
            trunkCollider.convex = false;
            tree.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Wood;
            trunkRenderer.gameObject.AddComponent<SurfaceTag>().surfaceType = SurfaceType.Wood;

            LODGroup lod = tree.AddComponent<LODGroup>();
            lod.fadeMode = LODFadeMode.CrossFade;
            lod.SetLODs(new[]
            {
                new LOD(0.18f, new Renderer[] { trunkRenderer, crownNearRenderer }),
                new LOD(0.04f, new Renderer[] { trunkRenderer, crownFarRenderer })
            });
            lod.RecalculateBounds();
            AssetUtil.SetStaticRecursive(tree);
        }

        private static MeshRenderer AddMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static bool TooClose(Transform root, float x, float z, float distance)
        {
            foreach (Transform t in root)
            {
                if (new Vector2(t.position.x - x, t.position.z - z).magnitude < distance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Erzeugt aus einem Scan-Modell je Variante einen Vegetationslayer (Dichte geteilt auf die Varianten,
        /// eigener salt). Liefert false, wenn der Scan fehlt oder keine Varianten lädt – dann bleibt das Prozedurale.
        /// </summary>
        private static bool AddScanLayers(List<VegetationLayer> layers, string id, float densityTotal, VegetationBiome biome,
            float minScale, float maxScale, float cull, bool shadows, int salt)
        {
            if (!ScanModels.IsAvailable(id))
            {
                return false;
            }

            ScanModels.EnsureImported(id);
            Mesh[] variants = ScanModels.LoadVariants(id);
            if (variants.Length == 0)
            {
                return false;
            }

            Material material = ScanModels.CreateFoliageMaterial(id);
            for (int i = 0; i < variants.Length; i++)
            {
                layers.Add(Layer("Scan " + id + " " + variants[i].name, variants[i], material, densityTotal / variants.Length, biome,
                    minScale, maxScale, cull, shadows, salt + i));
            }

            return true;
        }

        /// <summary>Setzt waterClearance für alle Layer, deren Name mit dem Präfix beginnt.</summary>
        private static void WaterClearanceOf(List<VegetationLayer> layers, string namePrefix, float clearance)
        {
            foreach (VegetationLayer layer in layers)
            {
                if (layer.name.StartsWith(namePrefix, System.StringComparison.Ordinal))
                {
                    layer.waterClearance = clearance;
                }
            }
        }

        private static VegetationLayer Layer(string name, Mesh mesh, Material material, float density, VegetationBiome biome,
            float minScale, float maxScale, float cull, bool shadows, int salt)
        {
            return new VegetationLayer
            {
                name = name,
                mesh = mesh,
                material = material,
                density = density,
                biome = biome,
                minScale = minScale,
                maxScale = maxScale,
                cullDistance = cull,
                castShadows = shadows,
                salt = salt
            };
        }

        /// <summary>
        /// Erzeugt ein Foliage-Material als HDRP/Lit (Ersatz für „Spinnenspiel/Foliage") über
        /// PipelineMaterials.Foliage: Tönung, optionale Albedo-Textur, Alpha-Clip, beidseitig.
        /// </summary>
        private static Material FoliageMaterial(string name, Texture2D texture, Color baseColor)
        {
            Material material = Spinnenspiel.Rendering.PipelineMaterials.Foliage(texture, null, baseColor);
            material.enableInstancing = true;
            return AssetUtil.ReplaceAsset(material, MaterialFolder + "/" + name + ".mat");
        }
    }
}
