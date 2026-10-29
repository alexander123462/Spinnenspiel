using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>MODELL14: Riesenblatt3 ist ein Blender-Neubau (ovales Bananen-
    /// blatt mit eingerissenem Rand: sraege, offen zum Rand verlaufende Seiten-
    /// risse, Mittelrippe und dichte Bogenseitenadern als Relief) statt des
    /// alten Mesh-Flickwerks. Prueft Mesh-Masse (Dreiecke 1500-3000, Aus-
    /// dehnung 1.5-2.5 m, Fuss bei y~0), 2 Submeshes mit Blatt oben (gemessen)
    /// und Atlas am Material.
    /// Ground truth: library.giantLeaves[2] nach UrwaldSceneBuilder.Build.</summary>
    public class Modell14Riesenblatt3Tests
    {
        [UnityTest]
        public IEnumerator M14_Riesenblatt3IsNewBlenderBananaLeaf()
        {
            JungleWorldGenerator.NextSeed = 4242;
            // WALD6 hat Riesenblaetter aus; der Test prueft das Asset -> Override wie in der Galerie.
            JungleWorldGenerator.ForceGiantLeaves = true;
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
            Assert.IsNotNull(lib.giantLeaves, "No giantLeaves in the library.");
            Assert.GreaterOrEqual(lib.giantLeaves.Length, 3, "giantLeaves too short.");
            Mesh leaf = lib.giantLeaves[2];
            Assert.IsNotNull(leaf, "giantLeaves[2] (Riesenblatt3) is null.");

            int tris = 0;
            for (int s = 0; s < leaf.subMeshCount; s++) tris += leaf.GetSubMesh(s).indexCount / 3;
            Bounds b = leaf.bounds;
            float spreadXZ = Mathf.Max(b.size.x, b.size.z);

            Debug.Log(string.Format("[M14] Riesenblatt3: Dreiecke={0} submeshes={1} y {2:0.000}..{3:0.000} spreadXZ={4:0.00} uv={5}",
                tris, leaf.subMeshCount, b.min.y, b.max.y, spreadXZ, leaf.uv.Length));

            // Vorgabe MODELL14: 1500-3000 Dreiecke.
            Assert.GreaterOrEqual(tris, 1500, "Riesenblatt3 too coarse (old mesh still in use?): " + tris);
            Assert.LessOrEqual(tris, 3000, "Riesenblatt3 too dense: " + tris);

            // 2 Slots (Stiel, Blatt) wie im Original.
            Assert.AreEqual(2, leaf.subMeshCount, "Riesenblatt3 must have 2 submeshes (Stiel+Blatt): " + leaf.subMeshCount);

            // Grosses Blatt: Ausdehnung in XZ 1.5-2.5 m.
            Assert.GreaterOrEqual(spreadXZ, 1.5f, "Riesenblatt blade too small: " + spreadXZ);
            Assert.LessOrEqual(spreadXZ, 2.5f, "Riesenblatt blade too big: " + spreadXZ);

            // Fuss am Ursprung (Steckbasis), kein Schweben/Verkehrtherum.
            Assert.GreaterOrEqual(b.min.y, -0.05f, "Riesenblatt origin not at base: min.y=" + b.min.y);
            Assert.LessOrEqual(b.min.y, 0.02f, "Riesenblatt floats above ground: min.y=" + b.min.y);

            // Eingerissener Rand: jeder Riss erzeugt eigene Randloops. Rand-
            // kanten aus den Dreiecken gezaehlt (Kante mit genau einem Dreieck).
            // Neubau misst 520 -> Schwelle 480 (altes Flickwerk: weniger).
            var tri = leaf.triangles;
            var edgeCount = new System.Collections.Generic.Dictionary<long, int>();
            for (int t = 0; t < tri.Length; t += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    int a = tri[t + e], bIdx = tri[t + (e + 1) % 3];
                    long key = a < bIdx ? ((long)a << 32) | (uint)bIdx : ((long)bIdx << 32) | (uint)a;
                    edgeCount.TryGetValue(key, out int n);
                    edgeCount[key] = n + 1;
                }
            }
            int boundaryEdges = 0;
            foreach (var kv in edgeCount) if (kv.Value == 1) boundaryEdges++;
            Debug.Log("[M14] Riesenblatt3: Randkanten gesamt=" + boundaryEdges);
            Assert.GreaterOrEqual(boundaryEdges, 480, "Riesenblatt3 lacks torn-margin loops: " + boundaryEdges);

            // UV vorhanden und Atlas am Blatt-Material verdrahtet (sonst weisses Material).
            Assert.Greater(leaf.uv.Length, 0, "Riesenblatt3 mesh has no UVs.");
            var mat = lib.giantLeafMats[2];
            Assert.IsNotNull(mat, "No leaf material for Riesenblatt3 in the library.");
            Texture baseMap = mat.GetTexture("_BaseColorMap");
            Assert.IsNotNull(baseMap, "MAT_Riesenblatt_3 has no _BaseColorMap (texture not wired).");
            Assert.That(baseMap.name, Does.Contain("Riesenblatt3"), "Wrong base map on MAT_Riesenblatt_3: " + baseMap.name);

            // Der Generator setzt Riesenblaetter weiterhin ins Unterholz.
            Assert.Greater(gen.GiantLeafCount, 0, "Generator placed no giant leaves.");
        }
    }
}
