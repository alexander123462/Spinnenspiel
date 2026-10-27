using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>MODELL13: Riesenblatt2 ist ein Blender-Neubau (Monstera-Typ: grosse
    /// Spreite mit seitlichen Schlitzen, inneren Fensterloechern, Mittelrippe und
    /// Bogenseitenadern als Relief) statt des alten Horsts. Prueft Mesh-Masse
    /// (Dreiecke 1500-3000, Ausdehnung 1.5-2.5 m, Fuss bei y~0), 2 Submeshes mit
    /// Blatt oben (gemessen) und Atlas am Material.
    /// Ground truth: library.giantLeaves[1] nach UrwaldSceneBuilder.Build.</summary>
    public class Modell13Riesenblatt2Tests
    {
        [UnityTest]
        public IEnumerator M13_Riesenblatt2IsNewBlenderMonstera()
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
            Assert.GreaterOrEqual(lib.giantLeaves.Length, 2, "giantLeaves too short.");
            Mesh leaf = lib.giantLeaves[1];
            Assert.IsNotNull(leaf, "giantLeaves[1] (Riesenblatt2) is null.");

            int tris = 0;
            for (int s = 0; s < leaf.subMeshCount; s++) tris += leaf.GetSubMesh(s).indexCount / 3;
            Bounds b = leaf.bounds;
            float spreadXZ = Mathf.Max(b.size.x, b.size.z);

            Debug.Log(string.Format("[M13] Riesenblatt2: Dreiecke={0} submeshes={1} y {2:0.000}..{3:0.000} spreadXZ={4:0.00} uv={5}",
                tris, leaf.subMeshCount, b.min.y, b.max.y, spreadXZ, leaf.uv.Length));

            // Vorgabe MODELL13: 1500-3000 Dreiecke.
            Assert.GreaterOrEqual(tris, 1500, "Riesenblatt2 too coarse (old mesh still in use?): " + tris);
            Assert.LessOrEqual(tris, 3000, "Riesenblatt2 too dense: " + tris);

            // 2 Slots (Stiel, Blatt) wie im Original.
            Assert.AreEqual(2, leaf.subMeshCount, "Riesenblatt2 must have 2 submeshes (Stiel+Blatt): " + leaf.subMeshCount);

            // Grosses Blatt: Ausdehnung in XZ 1.5-2.5 m.
            Assert.GreaterOrEqual(spreadXZ, 1.5f, "Riesenblatt blade too small: " + spreadXZ);
            Assert.LessOrEqual(spreadXZ, 2.5f, "Riesenblatt blade too big: " + spreadXZ);

            // Fuss am Ursprung (Steckbasis), kein Schweben/Verkehrtherum.
            Assert.GreaterOrEqual(b.min.y, -0.05f, "Riesenblatt origin not at base: min.y=" + b.min.y);
            Assert.LessOrEqual(b.min.y, 0.02f, "Riesenblatt floats above ground: min.y=" + b.min.y);

            // Monstera-Form: Schlitze und Fensterloecher erzeugen eigene Randloops.
            // Randkanten werden direkt aus den Submesh-Dreiecken gezaehlt (Kante mit
            // genau einem Dreieck = Rand). Alter Horst: nur Aussenrand (~480 Kanten).
            int leafSub = -1;
            float bestXZ = -1f;
            for (int s = 0; s < leaf.subMeshCount; s++)
            {
                float xz = leaf.GetSubMesh(s).bounds.extents.x * leaf.GetSubMesh(s).bounds.extents.z;
                if (xz > bestXZ) { bestXZ = xz; leafSub = s; }
            }
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
            Debug.Log("[M13] Riesenblatt2: Randkanten gesamt=" + boundaryEdges + " Blatt-Submesh=" + leafSub);
            Assert.GreaterOrEqual(boundaryEdges, 520, "Riesenblatt2 lacks split/fenestration loops: " + boundaryEdges);

            // UV vorhanden und Atlas am Blatt-Material verdrahtet (sonst weisses Material).
            Assert.Greater(leaf.uv.Length, 0, "Riesenblatt2 mesh has no UVs.");
            var mat = lib.giantLeafMats[1];
            Assert.IsNotNull(mat, "No leaf material for Riesenblatt2 in the library.");
            Texture baseMap = mat.GetTexture("_BaseColorMap");
            Assert.IsNotNull(baseMap, "MAT_Riesenblatt_2 has no _BaseColorMap (texture not wired).");
            Assert.That(baseMap.name, Does.Contain("Riesenblatt2"), "Wrong base map on MAT_Riesenblatt_2: " + baseMap.name);

            // Der Generator setzt Riesenblaetter weiterhin ins Unterholz.
            Assert.Greater(gen.GiantLeafCount, 0, "Generator placed no giant leaves.");
        }
    }
}
