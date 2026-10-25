using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// MODELL2: Der Grasbueschel-Typ Gras2 kommt jetzt aus dem Blender-Neubau
    /// (MAT_Gras2, Seggen-Typ: 20 breite ueberhaengende Blaetter, V-Rinne).
    /// Prueft: Mesh vorhanden, Dreiecke und Groesse im Vorgabenbereich,
    /// ein Slot, keine schwebenden Instanzen.
    /// </summary>
    public class Modell2Gras2Tests
    {
        private const string GrasMat = "MAT_Gras2";

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

        [UnityTest]
        public IEnumerator Modell2_Gras2_MeshUndInstanzen()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            Mesh mesh = null;
            int instances = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                bool hit = false;
                foreach (var m in mr.sharedMaterials)
                    if (m != null && m.name == GrasMat) { hit = true; break; }
                if (!hit) continue;
                instances++;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) mesh = mf.sharedMesh;
            }

            Assert.IsNotNull(mesh, "MODELL2: kein Mesh mit Material " + GrasMat + " in der Welt.");
            Debug.Log("[MODELL2] Gras2-Instanzen: " + instances);

            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
            bool trisOk = tris >= 300 && tris <= 800;
            bool slotOk = mesh.subMeshCount == 1;
            float h = mesh.bounds.size.y;
            bool heightOk = h >= 0.4f && h <= 0.8f;
            Debug.Log("[MODELL2] Dreiecke " + tris + " -> 300..800 -> " + (trisOk ? "OK" : "NOK"));
            Debug.Log("[MODELL2] Slots " + mesh.subMeshCount + " -> 1 -> " + (slotOk ? "OK" : "NOK"));
            Debug.Log("[MODELL2] Mesh-Hoehe " + h.ToString("F3") + " m -> 0.4..0.8 -> " + (heightOk ? "OK" : "NOK"));

            Assert.That(tris, Is.InRange(300, 800), "MODELL2: Dreiecke ausserhalb 300..800");
            Assert.AreEqual(1, mesh.subMeshCount, "MODELL2: erwartet genau 1 Submesh/Slot");
            Assert.That(h, Is.InRange(0.4f, 0.8f), "MODELL2: Mesh-Hoehe ausserhalb 0.4..0.8 m");

            // Instanzen sitzen am Boden (nicht schwebend), wie bei Modell1:
            // Position.y + Bounds.min.y*Scale gegen Terrain-SampleHeight pruefen.
            var terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "MODELL2: kein aktives Terrain.");
            int checkedN = 0, floating = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                bool hit = false;
                foreach (var m in mr.sharedMaterials)
                    if (m != null && m.name == GrasMat) { hit = true; break; }
                if (!hit) continue;
                checkedN++;
                Vector3 p = mr.transform.position;
                float groundY = terrain.transform.position.y + terrain.SampleHeight(p);
                float baseY = p.y + mesh.bounds.min.y * mr.transform.localScale.y;
                if (baseY > groundY + 0.15f) floating++;
            }
            Debug.Log("[MODELL2] Instanzen geprueft " + checkedN + ", schwebend " + floating
                      + " -> 0 -> " + (floating == 0 ? "OK" : "NOK"));
            Assert.AreEqual(0, floating, "MODELL2: schwebende Gras2-Instanzen gefunden");
        }
    }
}
