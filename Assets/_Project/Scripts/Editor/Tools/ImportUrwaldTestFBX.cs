using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Spinnenspiel.EditorTools
{
    // T7a: Import der Urwald-Test-FBX (Blender T2) via proven ScanModels importer settings,
    // then log mesh count + bounds so scale/roundtrip can be checked against the Blender report.
    public static class ImportUrwaldTestFBX
    {
        public static void ImportTest()
        {
            string src = "C:/Users/vorau/dev/Spinnenspiel/ArtSource/UrwaldTest/urwald_test.fbx";
            string targetFolder = "Assets/_Project/Art/Plants/UrwaldTest";
            string target = targetFolder + "/urwald_test.fbx";

            if (!File.Exists(src))
            {
                Debug.LogError("[Spinnenspiel] Urwald-Test-FBX not found at: " + src);
                return;
            }

            AssetUtil.EnsureFolder(targetFolder);
            if (!File.Exists(target))
            {
                File.Copy(src, target, true);
            }
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);

            // EXACT ScanModels.cs importer block (lines 120-131)
            ModelImporter modelImporter = AssetImporter.GetAtPath(target) as ModelImporter;
            if (modelImporter == null)
            {
                Debug.LogWarning("[Spinnenspiel] Kein ModelImporter für: " + target);
                return;
            }
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
            modelImporter.meshCompression = ModelImporterMeshCompression.Medium;
            modelImporter.useFileScale = false; // FBX-Einheit ignorieren, Vertices im Maßstab der Datei
            modelImporter.SaveAndReimport();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(target);
            if (prefab == null)
            {
                Debug.LogError("[Spinnenspiel] Import produced no prefab at: " + target);
                return;
            }

            System.Collections.Generic.List<MeshFilter> filterList = new System.Collections.Generic.List<MeshFilter>();
            prefab.GetComponentsInChildren<MeshFilter>(true, filterList);
            int nMeshes = filterList.Count;
            if (nMeshes == 0)
            {
                Debug.LogError("[Spinnenspiel] No MeshFilter components in imported prefab");
                return;
            }

            Vector3 minB = new Vector3(1e9f, 1e9f, 1e9f);
            Vector3 maxB = new Vector3(-1e9f, -1e9f, -1e9f);

            Debug.Log("[UrwaldFBX] Imported " + target + " -> meshes=" + nMeshes);
            for (int i = 0; i < nMeshes; i++)
            {
                Mesh m = filterList[i].sharedMesh;
                Bounds b = m.bounds; // local mesh bounds
                Vector3 s = b.size;
                Debug.Log("[UrwaldFBX] mesh[" + i + "] name=" + (m.name == null ? "?" : m.name)
                    + " subs=" + m.subMeshCount
                    + " size x=" + s.x.ToString("0.###") + " y=" + s.y.ToString("0.###") + " z=" + s.z.ToString("0.###"));
            }

            System.Collections.Generic.List<Renderer> rendererList = new System.Collections.Generic.List<Renderer>();
            prefab.GetComponentsInChildren<Renderer>(true, rendererList);
            int nR = rendererList.Count;
            for (int i = 0; i < nR; i++)
            {
                Bounds wb = rendererList[i].bounds; // world AABB
                minB = Vector3.Min(minB, wb.min);
                maxB = Vector3.Max(maxB, wb.max);
            }
            if (nR > 0)
            {
                Vector3 total = maxB - minB;
                Debug.Log("[UrwaldFBX] combined renderers=" + nR
                    + " totalBounds x=" + total.x.ToString("0.###")
                    + " y=" + total.y.ToString("0.###")
                    + " z=" + total.z.ToString("0.###")
                    + " min=(" + minB.x.ToString("0.##") + "," + minB.y.ToString("0.##") + "," + minB.z.ToString("0.##") + ")");
            }
        }
    }
}
