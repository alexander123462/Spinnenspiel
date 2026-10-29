using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// MODELL18: Die Blume kommt aus dem Blender-Neubau (Vinca-Typ: 5 windradige
    /// Kronblaetter mit Augenscheibe und Staubblaettern, 3 Stengel mit
    /// lanzettlichen Blaettern). Rosa und gelb/orange teilen sich das Mesh,
    /// die Farbe kommt aus zwei Albedo-Karten. Prueft: Mesh vorhanden,
    /// Dreiecke, Hoehe, ein Slot, UV-Karten, Texturen haengen, beide Varianten
    /// stehen in der Welt.
    /// </summary>
    public class Modell18BlumeTests
    {
        private const string BlueteRosa = "MAT_Bluete_Rosa";
        private const string BlueteGelb = "MAT_Bluete_Gelb";

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

        // StaticBatchingUtility.Combine ersetzt MeshFilter-Meshes durch Batch-
        // Meshes. Deterministisch ueber die Asset-Bibliothek gehen: die Paare
        // mit den Blueten-Materialien holen (gleiches Mesh, zwei Materialien).
        private static void FindBlumeEntries(JungleWorldGenerator gen,
            out Mesh meshR, out Material matR, out Mesh meshG, out Material matG)
        {
            meshR = null; matR = null; meshG = null; matG = null;
            if (gen == null || gen.library == null) return;
            var meshes = gen.library.groundCover;
            var mats = gen.library.groundCoverMats;
            if (meshes == null || mats == null) return;
            for (int i = 0; i < meshes.Length && i < mats.Length; i++)
            {
                if (meshes[i] == null || mats[i] == null) continue;
                if (mats[i].name == BlueteRosa && matR == null) { meshR = meshes[i]; matR = mats[i]; }
                else if (mats[i].name == BlueteGelb && matG == null) { meshG = meshes[i]; matG = mats[i]; }
            }
        }

        [UnityTest]
        public IEnumerator Modell18_Blume_MeshVariantenUndTexturen()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            Mesh meshR, meshG; Material matR, matG;
            FindBlumeEntries(gen, out meshR, out matR, out meshG, out matG);
            Assert.IsNotNull(matR, "MODELL18: kein groundCover-Eintrag mit " + BlueteRosa);
            Assert.IsNotNull(matG, "MODELL18: kein groundCover-Eintrag mit " + BlueteGelb);
            Assert.AreSame(meshR, meshG, "MODELL18: Rosa und Gelb sollen dasselbe Mesh teilen");
            Mesh mesh = meshR;

            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetSubMesh(s).indexCount / 3;
            bool trisOk = tris >= 400 && tris <= 900;
            bool slotOk = mesh.subMeshCount == 1;
            float height = mesh.bounds.size.y;
            float spread = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z);
            bool heightOk = height >= 0.1f && height <= 0.3f;
            bool spreadOk = spread >= 0.1f && spread <= 0.6f;
            Debug.Log("[MODELL18] Dreiecke " + tris + " -> 400..900 -> " + (trisOk ? "OK" : "NOK"));
            Debug.Log("[MODELL18] Slots " + mesh.subMeshCount + " -> 1 -> " + (slotOk ? "OK" : "NOK"));
            Debug.Log("[MODELL18] Hoehe " + height.ToString("F3")
                      + " m -> 0.1..0.3 -> " + (heightOk ? "OK" : "NOK"));
            Debug.Log("[MODELL18] Ausdehnung " + spread.ToString("F3")
                      + " m -> 0.1..0.6 -> " + (spreadOk ? "OK" : "NOK"));

            Assert.That(tris, Is.InRange(400, 900), "MODELL18: Dreiecke ausserhalb 400..900");
            Assert.AreEqual(1, mesh.subMeshCount, "MODELL18: erwartet genau 1 Submesh/Slot");
            Assert.That(height, Is.InRange(0.1f, 0.3f), "MODELL18: Hoehe ausserhalb 0.1..0.3 m");
            Assert.That(spread, Is.InRange(0.1f, 0.6f), "MODELL18: Ausdehnung ausserhalb 0.1..0.6 m");

            // UV-Atlas (8x5): je Zelle eine eigene Insel, Werte in 0..1.
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
            Debug.Log("[MODELL18] UVs " + uvs.Count + "/" + mesh.vertexCount
                      + ", eindeutig " + uniqueU + " -> >=400 -> " + (islandsOk ? "OK" : "NOK"));
            Assert.IsTrue(islandsOk, "MODELL18: UV-Atlas fehlt oder deckt die Zellen nicht ab");

            // Texturen haengen wirklich (Weiss-Material-Falle aus AGENTS.md):
            // beide Albedos unterschiedlich, Normalmap gesetzt.
            var texR = matR.GetTexture("_BaseColorMap");
            var texG = matG.GetTexture("_BaseColorMap");
            Assert.IsNotNull(texR, "MODELL18: " + BlueteRosa + " ohne _BaseColorMap");
            Assert.IsNotNull(texG, "MODELL18: " + BlueteGelb + " ohne _BaseColorMap");
            Assert.AreNotSame(texR, texG, "MODELL18: Rosa und Gelb teilen sich die Albedo");
            Assert.IsNotNull(matR.GetTexture("_NormalMap"), "MODELL18: " + BlueteRosa + " ohne _NormalMap");
            Assert.IsNotNull(matG.GetTexture("_NormalMap"), "MODELL18: " + BlueteGelb + " ohne _NormalMap");

            // Instanzen in der Welt (nach Combine zaehlen die Batch-Materialien).
            int instR = 0, instG = 0;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                foreach (var m in mr.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.name == BlueteRosa) instR++;
                    else if (m.name == BlueteGelb) instG++;
                }
            }
            bool footOk = mesh.bounds.min.y >= -0.05f && mesh.bounds.min.y <= 0.02f;
            Debug.Log("[MODELL18] Fuss y " + mesh.bounds.min.y.ToString("F3")
                      + " -> -0.05..0.02 -> " + (footOk ? "OK" : "NOK")
                      + ", Instanzen Rosa " + instR + " / Gelb " + instG + " -> je >50 -> "
                      + ((instR > 50 && instG > 50) ? "OK" : "NOK"));
            Assert.IsTrue(footOk, "MODELL18: Blume hat Fuss nicht am Ursprung (Schweben/Verkehrtherum)");
            Assert.Greater(instR, 50, "MODELL18: zu wenige Rosa-Instanzen in der Welt");
            Assert.Greater(instG, 50, "MODELL18: zu wenige Gelb-Instanzen in der Welt");
        }
    }
}
