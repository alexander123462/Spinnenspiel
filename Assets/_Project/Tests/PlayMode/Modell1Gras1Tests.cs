using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// MODELL1: Der Grasbueschel-Typ Gras1 kommt jetzt aus dem Blender-Neubau
    /// (MAT_Gras1, 30 Halme, V-Rinne). Prueft: Mesh vorhanden, Dreiecke und
    /// Groesse im Vorgabenbereich, ein Slot, keine schwebenden Instanzen.
    /// </summary>
    public class Modell1Gras1Tests
    {
        private const string GrasMat = "MAT_Gras1";

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
        public IEnumerator Modell1_Gras1_MeshUndInstanzen()
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

            Assert.IsNotNull(mesh, "MODELL1: kein Mesh mit Material " + GrasMat + " in der Welt.");
            Debug.Log("[MODELL1] Gras1-Instanzen: " + instances);

            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
            bool trisOk = tris >= 300 && tris <= 800;
            bool slotOk = mesh.subMeshCount == 1;
            float h = mesh.bounds.size.y;
            bool heightOk = h >= 0.3f && h <= 0.6f;
            Debug.Log("[MODELL1] Dreiecke " + tris + " -> 300..800 -> " + (trisOk ? "OK" : "NOK"));
            Debug.Log("[MODELL1] Slots " + mesh.subMeshCount + " -> 1 -> " + (slotOk ? "OK" : "NOK"));
            Debug.Log("[MODELL1] Mesh-Hoehe " + h.ToString("F3") + " m -> 0.3..0.6 -> " + (heightOk ? "OK" : "NOK"));

            Assert.That(tris, Is.InRange(300, 800), "MODELL1: Dreiecke ausserhalb 300..800");
            Assert.AreEqual(1, mesh.subMeshCount, "MODELL1: erwartet genau 1 Submesh/Slot");
            Assert.That(h, Is.InRange(0.3f, 0.6f), "MODELL1: Mesh-Hoehe ausserhalb 0.3..0.6 m");

            // Instanzen sitzen am Boden (nicht schwebend): Der Generator setzt die
            // Transform-Position auf Height(x,z)-0.02. BBS-Zentrum ist ungeeignet
            // (bis 0.4 m neben dem Spawn-Punkt -> an Boeschungen falscher Samplewert).
            var terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "MODELL1: kein aktives Terrain.");
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
            Debug.Log("[MODELL1] Instanzen geprueft " + checkedN + ", schwebend " + floating
                      + " -> 0 -> " + (floating == 0 ? "OK" : "NOK"));
            Assert.AreEqual(0, floating, "MODELL1: schwebende Gras-Instanzen gefunden");
        }
    }
}
