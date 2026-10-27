using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>MODELL16: Totholz ist ein Blender-Neubau (3 Staemme 1.5-3 m, Borke mit
    /// Rissen, abgebrochene Aeststummel, Bruchenden mit Jahresringen, je 2 Submeshes
    /// 0=Borke 1=Holz) statt der prozeduralen M_Log0..2 (je 616 Dreiecke). Prueft ausserdem
    /// die konvexen Hull-Collider und die Materialverdrahtung.
    /// Ground truth: library.logs/logHulls/logMats nach UrwaldSceneBuilder.Build.</summary>
    public class Modell16TotholzTests
    {
        [UnityTest]
        public IEnumerator M16_TotholzIsNewBlenderLog()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;

            JungleWorldGenerator gen = null;
            float timeout = 15f;
            while (timeout > 0f && gen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) gen = g;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(gen, "JungleWorldGenerator not ready after 15 s.");

            var lib = gen.library;
            Assert.IsNotNull(lib, "No JungleAssetLibrary on the generator.");
            Assert.IsNotNull(lib.logs, "No logs in the library.");
            Assert.GreaterOrEqual(lib.logs.Length, 3, "logs too short.");

            for (int i = 0; i < 3; i++)
            {
                Mesh log = lib.logs[i];
                Assert.IsNotNull(log, "logs[" + i + "] (Totholz_" + (i + 1) + ") is null.");

                int tris = 0;
                for (int s = 0; s < log.subMeshCount; s++) tris += log.GetSubMesh(s).indexCount / 3;
                Bounds b = log.bounds;

                Debug.Log(string.Format("[M16] Totholz_{0}: Dreiecke={1} submeshes={2} y {3:0.00}..{4:0.00} x {5:0.00}..{6:0.00}",
                    i + 1, tris, log.subMeshCount, b.min.y, b.max.y, b.min.x, b.max.x));

                // Vorgabe MODELL16: 2000-5000 Dreiecke (alt: 616 -> Fallback waere durch).
                Assert.GreaterOrEqual(tris, 2000, "Totholz_" + (i + 1) + " too coarse (old mesh still in use?): " + tris);
                Assert.LessOrEqual(tris, 5000, "Totholz_" + (i + 1) + " too dense: " + tris);

                // 2 Slots fester Reihenfolge (0 Borke, 1 Holz).
                Assert.AreEqual(2, log.subMeshCount, "Totholz_" + (i + 1) + " must have 2 submeshes (Borke+Holz): " + log.subMeshCount);

                // Vorgabe: 1.5-3 m Stammachse. Der Stamm LIEGT (wie die alten M_Log):
                // Achse auf Unity-X, Fuss bei y=0 (Hoehe = Durchmesser). Der Generator
                // dreht nur um Y und versenkt den Stamm 0.05 m ein.
                Assert.GreaterOrEqual(b.size.x, 1.5f, "Totholz_" + (i + 1) + " too short: " + b.size.x);
                Assert.LessOrEqual(b.size.x, 3.15f, "Totholz_" + (i + 1) + " too long: " + b.size.x);
                Assert.GreaterOrEqual(b.min.y, -0.05f, "Totholz_" + (i + 1) + " sinks below origin: " + b.min.y);
                Assert.LessOrEqual(b.min.y, 0.02f, "Totholz_" + (i + 1) + " floats: " + b.min.y);
                Assert.Less(b.size.y, 1.2f, "Totholz_" + (i + 1) + " height (diameter) implausible: " + b.size.y);
                Assert.Less(b.size.z, 1.2f, "Totholz_" + (i + 1) + " depth implausible: " + b.size.z);

                // Submesh 0 = Borke (grosser Anteil), 1 = Holz (Bruchenden + Stummelkappen).
                int barkCount = log.GetSubMesh(0).indexCount;
                int woodCount = log.GetSubMesh(1).indexCount;
                Assert.Greater(barkCount, woodCount * 2,
                    "Totholz_" + (i + 1) + " submesh order wrong? Bark=" + barkCount + " Holz=" + woodCount);

                // UV vorhanden (sonst weisses Material).
                Assert.Greater(log.uv.Length, 0, "Totholz_" + (i + 1) + " mesh has no UVs.");
            }

            // Konvexe Hulls: kleines Prisma pro Variante (fallbackfaehig: null erlaubt).
            if (lib.logHulls != null)
            {
                for (int i = 0; i < 3 && i < lib.logHulls.Length; i++)
                {
                    Mesh hull = lib.logHulls[i];
                    if (hull == null) continue;
                    Assert.LessOrEqual(hull.vertexCount, 255, "Totholz_" + (i + 1) + " hull too many verts for convex: " + hull.vertexCount);
                    Assert.AreEqual(36, hull.triangles.Length / 3, "Totholz_" + (i + 1) + " hull tris: " + (hull.triangles.Length / 3));
                }
            }

            // Materialien mit Texturen verdrahtet (sonst weisses Material).
            Assert.IsNotNull(lib.logMats, "No logMats in the library.");
            Assert.AreEqual(2, lib.logMats.Length, "logMats must have 2 entries (Borke, Holz).");
            Texture barkMap = lib.logMats[0].GetTexture("_BaseColorMap");
            Assert.IsNotNull(barkMap, "MAT_Totholz_Bark has no _BaseColorMap.");
            Assert.That(barkMap.name, Does.Contain("Totholz_Bark"), "Wrong base map on bark: " + barkMap.name);
            Texture woodMap = lib.logMats[1].GetTexture("_BaseColorMap");
            Assert.IsNotNull(woodMap, "MAT_Totholz_Wood has no _BaseColorMap.");
            Assert.That(woodMap.name, Does.Contain("Totholz_Wood"), "Wrong base map on wood: " + woodMap.name);

            // Generator setzt Totholz und jeder Stamm hat einen konvexen MeshCollider.
            Assert.Greater(gen.LogCount, 0, "Generator placed no Totholz.");
            var totholz = GameObject.Find("Totholz");
            Assert.IsNotNull(totholz, "No Totholz root node.");
            MeshCollider[] cols = totholz.GetComponentsInChildren<MeshCollider>();
            Assert.Greater(cols.Length, 0, "No MeshCollider on Totholz.");
            int convex = 0;
            foreach (MeshCollider mc in cols) if (mc.convex) convex++;
            Assert.AreEqual(cols.Length, convex, "Totholz colliders must be convex: " + convex + "/" + cols.Length);
        }
    }
}

