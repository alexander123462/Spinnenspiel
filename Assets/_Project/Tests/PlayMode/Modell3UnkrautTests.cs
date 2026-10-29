using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// MODELL3: Das Unkraut kommt jetzt aus dem Blender-Neubau (MAT_Unkraut,
    /// Loewenzahn-/Ampfer-Rosette: 12 gezaeckte Blaetter mit Mittelrippe).
    /// Prueft: Mesh vorhanden, Dreiecke und Groesse im Vorgabenbereich,
    /// ein Slot, UV-Karten vorhanden, keine schwebenden Instanzen.
    /// </summary>
    public class Modell3UnkrautTests
    {
        private const string UnkrautMat = "MAT_Unkraut";

        private static IEnumerator LoadWorld(System.Action<JungleWorldGenerator> done)
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            float timeout = 60f;
            JungleWorldGenerator gen = null;
            while (timeout > 0f && gen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) gen = g;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(gen, "Welt nicht fertig.");
            done(gen);
        }

        private static Mesh FindUnkrautMesh(out int instances)
        {
            // Achtung: StaticBatchingUtility.Combine baut Batch-Meshes mit vielen
            // Submeshes, die MAT_Unkraut im Material-Array haben. Deterministisch
            // das echte FBX-Mesh (Name 'Unkraut') holen, sonst das kleinste Match.
            Mesh mesh = null;
            float bestSize = float.MaxValue;
            instances = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                bool hit = false;
                foreach (var m in mr.sharedMaterials)
                    if (m != null && m.name == UnkrautMat) { hit = true; break; }
                if (!hit) continue;
                instances++;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var sh = mf.sharedMesh;
                if (sh.name == "Unkraut") return sh;
                float s = sh.bounds.size.magnitude;
                if (s < bestSize) { bestSize = s; mesh = sh; }
            }
            return mesh;
        }

        [UnityTest]
        public IEnumerator Modell3_Unkraut_MeshUndInstanzen()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            int instances;
            Mesh mesh = FindUnkrautMesh(out instances);
            Assert.IsNotNull(mesh, "MODELL3: kein Mesh mit Material " + UnkrautMat + " in der Welt.");
            Debug.Log("[MODELL3] Unkraut-Instanzen: " + instances);

            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
            bool trisOk = tris >= 400 && tris <= 900;
            bool slotOk = mesh.subMeshCount == 1;
            float spread = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
            bool sizeOk = spread >= 0.2f && spread <= 0.5f;
            Debug.Log("[MODELL3] Dreiecke " + tris + " -> 400..900 -> " + (trisOk ? "OK" : "NOK"));
            Debug.Log("[MODELL3] Slots " + mesh.subMeshCount + " -> 1 -> " + (slotOk ? "OK" : "NOK"));
            Debug.Log("[MODELL3] Rosetten-Durchmesser " + spread.ToString("F3")
                      + " m -> 0.2..0.5 -> " + (sizeOk ? "OK" : "NOK"));

            Assert.That(tris, Is.InRange(400, 900), "MODELL3: Dreiecke ausserhalb 400..900");
            Assert.AreEqual(1, mesh.subMeshCount, "MODELL3: erwartet genau 1 Submesh/Slot");
            Assert.That(spread, Is.InRange(0.2f, 0.5f), "MODELL3: Rosetten-Durchmesser ausserhalb 0.2..0.5 m");

            // UV-Atlas: je Blatt eine Insel, Werte innerhalb 0..1 (TEXTUR haengt an UV).
            // Hinweis: Unity tesselliert Quads (1243 V), die UV-Inseln zahlen sich
            // nach Original-Ecken (504) — Schwelle 400 laesst Verscharfungen zu.
            var uvs = new System.Collections.Generic.List<Vector2>(mesh.vertexCount);
            mesh.GetUVs(0, uvs);
            bool uvOk = uvs.Count == mesh.vertexCount;
            int uniqueU = 0;
            if (uvOk)
            {
                var seen = new System.Collections.Generic.HashSet<long>();
                foreach (var uv in uvs)
                {
                    if (uv.x < -0.01f || uv.x > 1.01f || uv.y < -0.01f || uv.y > 1.01f) { uvOk = false; break; }
                    seen.Add((long)(uv.x * 1000) * 100003 + (long)(uv.y * 1000));
                }
                uniqueU = seen.Count;
            }
            bool islandsOk = uvOk && uniqueU >= 400;
            Debug.Log("[MODELL3] UVs " + uvs.Count + "/" + mesh.vertexCount
                      + ", eindeutig " + uniqueU + " -> >=400 -> " + (islandsOk ? "OK" : "NOK"));
            Assert.IsTrue(islandsOk, "MODELL3: UV-Atlas fehlt oder deckt die Blatt-Inseln nicht ab");

            // Instanzen sitzen am Boden (nicht schwebend), Muster wie Modell2.
            var terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "MODELL3: kein aktives Terrain.");
            int checkedN = 0, floating = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                bool hit = false;
                foreach (var m in mr.sharedMaterials)
                    if (m != null && m.name == UnkrautMat) { hit = true; break; }
                if (!hit) continue;
                checkedN++;
                Vector3 p = mr.transform.position;
                float groundY = terrain.transform.position.y + terrain.SampleHeight(p);
                float baseY = p.y + mesh.bounds.min.y * mr.transform.localScale.y;
                if (baseY > groundY + 0.15f) floating++;
            }
            Debug.Log("[MODELL3] Instanzen geprueft " + checkedN + ", schwebend " + floating
                      + " -> 0 -> " + (floating == 0 ? "OK" : "NOK"));
            Assert.AreEqual(0, floating, "MODELL3: schwebende Unkraut-Instanzen gefunden");
        }
    }
}
