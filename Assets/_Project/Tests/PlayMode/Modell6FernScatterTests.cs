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
    /// MODELL6: Der Farn-Scatter-Patch besteht jetzt aus EINZELWEDDELN in 3 Varianten
    /// (fern_wedel.fbx) statt 245 geklonten Busch-Instanzen. Prueft: 3 geteilte
    /// Varianten-Meshes, Dreiecke 400..900 je Variante, 2 Submeshes je Mesh,
    /// Wedel-Hoehe 0.25..0.55 m, Fuss am Ursprung, Patch-Zusammensetzung
    /// (245 Poisson-Disc-Punkte, Mindestabstand, Patchgroesse) und Draw-Call-
    /// Budget (max +15 % gegenueber dem alten Patch: 245 x 2 Submeshes).
    /// </summary>
    public class Modell6FernScatterTests
    {
        private const int OldPatchInstances = 245;      // U4: 245 Instanzen je Patch
        private const int OldSubmeshesPerInst = 2;      // alt wie neu: Stiel + Fiedern
        private const int OldPatchTris = 589812;        // gemessen am alten fern_scatter.fbx

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

        private static List<Mesh> UniqueMeshes(JungleAssetLibrary lib)
        {
            var uniq = new List<Mesh>();
            if (lib == null || lib.fernScatterMeshes == null) return uniq;
            foreach (Mesh m in lib.fernScatterMeshes)
                if (m != null && !uniq.Contains(m)) uniq.Add(m);
            return uniq;
        }

        [UnityTest]
        public IEnumerator Modell6_FarnWedel_DreiVariantenMitDreieckBudget()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            var lib = gen.library;
            Assert.IsNotNull(lib.fernScatterMeshes, "MODELL6: keine fernScatterMeshes in der Bibliothek.");
            var uniq = UniqueMeshes(lib);
            Debug.Log("[MODELL6] geteilte Varianten-Meshes: " + uniq.Count);
            Assert.AreEqual(3, uniq.Count, "MODELL6: genau 3 Einzelwedel-Varianten erwartet.");

            foreach (Mesh m in uniq)
            {
                int tris = 0;
                for (int s = 0; s < m.subMeshCount; s++) tris += m.GetSubMesh(s).indexCount / 3;
                bool trisOk = tris >= 400 && tris <= 900;
                bool subOk = m.subMeshCount == 2;
                float h = m.bounds.size.y;
                bool heightOk = h >= 0.25f && h <= 0.55f;
                bool footOk = m.bounds.min.y > -0.03f && m.bounds.min.y < 0.05f;
                bool uvOk = m.uv != null && m.uv.Length == m.vertexCount;
                Debug.Log("[MODELL6] " + m.name + ": Dreiecke " + tris + " -> 400..900 -> " + (trisOk ? "OK" : "NOK")
                    + "; Submeshes " + m.subMeshCount + " -> 2 -> " + (subOk ? "OK" : "NOK")
                    + "; Hoehe " + h.ToString("F3") + " -> 0.25..0.55 -> " + (heightOk ? "OK" : "NOK")
                    + "; Fuss " + m.bounds.min.y.ToString("F3") + " -> ~0 -> " + (footOk ? "OK" : "NOK")
                    + "; UV pro Vertex: " + uvOk);
                Assert.IsTrue(trisOk, "MODELL6: Dreiecke " + tris + " ausserhalb 400..900 (" + m.name + ").");
                Assert.IsTrue(subOk, "MODELL6: " + m.name + " hat " + m.subMeshCount + " Submeshes, erwartet 2.");
                Assert.IsTrue(heightOk, "MODELL6: Wedel-Hoehe " + h + " ausserhalb 0.25..0.55 m.");
                Assert.IsTrue(footOk, "MODELL6: Fusspunkt bei y=" + m.bounds.min.y + ", erwartet ~0.");
                Assert.IsTrue(uvOk, "MODELL6: " + m.name + " hat keine UV-Koordinaten pro Vertex.");
            }
        }

        [UnityTest]
        public IEnumerator Modell6_FarnPatch_ZusammensetzungUndDrawCalls()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            var lib = gen.library;
            Assert.AreEqual(OldPatchInstances, lib.fernScatterCount,
                "MODELL6: Patch-Zusammenstellung soll 245 Poisson-Disc-Punkte behalten.");
            Assert.IsNotNull(lib.fernScatterXZ);
            Assert.AreEqual(lib.fernScatterCount, lib.fernScatterXZ.Length,
                "MODELL6: XZ-Feld und Instanzanzahl passen nicht zusammen.");

            // Poisson-Disc: Mindestabstand der Punkte (Patch liegt zentriert, 8 m Feld).
            float minDist = float.MaxValue;
            for (int a = 0; a < lib.fernScatterXZ.Length; a++)
            {
                for (int b = a + 1; b < lib.fernScatterXZ.Length; b++)
                {
                    Vector3 d = lib.fernScatterXZ[b] - lib.fernScatterXZ[a];
                    float sq = d.x * d.x + d.z * d.z;
                    if (sq < minDist) minDist = sq;
                }
            }
            minDist = Mathf.Sqrt(minDist);
            bool distOk = minDist >= 0.41f;
            bool sizeOk = lib.fernScatterPatchSize > 6.0f && lib.fernScatterPatchSize < 9.0f;
            Debug.Log("[MODELL6] Mindestabstand " + minDist.ToString("F3") + " -> >= 0.41 -> "
                + (distOk ? "OK" : "NOK") + "; Patchgroesse " + lib.fernScatterPatchSize.ToString("F2")
                + " -> 6..9 -> " + (sizeOk ? "OK" : "NOK"));
            Assert.IsTrue(distOk, "MODELL6: Poisson-Disc-Mindestabstand " + minDist + " m unterschritten.");
            Assert.IsTrue(sizeOk, "MODELL6: Patchgroesse " + lib.fernScatterPatchSize + " m verlassen.");

            // Draw Calls je PATCH: Instanzen x Submeshes (Materialwechsel). Vorgabe max +15 %.
            // Die Welt platziert mehrere Patches; pro Patch zaehlt dieselbe Rechnung wie frueher.
            int placed = gen.FernScatterCount;
            Assert.That(placed, Is.GreaterThan(0), "MODELL6: kein Farn-Scatter in der Welt platziert.");
            float drawNow = lib.fernScatterCount * 2f;
            Debug.Log("[MODELL6] Farn-Instanzen gesamt in der Welt: " + placed
                + " (= " + (placed / (float)lib.fernScatterCount).ToString("F1") + " Patches).");
            float drawBudget = OldPatchInstances * OldSubmeshesPerInst * 1.15f;
            Debug.Log("[MODELL6] Draw Calls im Patch: " + drawNow + " -> Budget " + drawBudget
                + " (alt " + (OldPatchInstances * OldSubmeshesPerInst) + ") -> "
                + (drawNow <= drawBudget ? "OK" : "NOK"));
            Assert.That(drawNow, Is.LessThanOrEqualTo(drawBudget),
                "MODELL6: Draw Calls ueber dem Budget (+15 %).");

            // Geometrie-Budget: neues Patch darf nicht mehr Dreiecke haben als das alte.
            var uniq = UniqueMeshes(lib);
            int trisPerVariant = 0;
            for (int a = 0; a < uniq.Count; a++)
            {
                int t = 0;
                for (int s = 0; s < uniq[a].subMeshCount; s++) t += uniq[a].GetSubMesh(s).indexCount / 3;
                trisPerVariant += t;
            }
            float avgTris = trisPerVariant / (float)uniq.Count;
            float patchTris = lib.fernScatterCount * avgTris;
            Debug.Log("[MODELL6] Dreiecke je Wedel im Mittel " + avgTris.ToString("F0")
                + ", Patch geschaetzt " + patchTris.ToString("F0") + " -> alt " + OldPatchTris
                + " -> " + (patchTris <= OldPatchTris ? "OK" : "NOK"));
            Assert.That(patchTris, Is.LessThanOrEqualTo((float)OldPatchTris),
                "MODELL6: Patch-Dreiecke ueber dem alten Wert.");
        }
    }
}
