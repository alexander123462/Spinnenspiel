using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>MODELL22: Baum_Klein02 (tree_small_02, Index 4 in phTree-Listen) neu als
    /// prozedurales Blender-Asset. Misst am geladenen Asset: LOD0 8000-20000 Dreiecke,
    /// Hoehe 6-9 m, genau 2 Submeshes, Slot-Namen mit trunk/leaves, LOD1 25-60 % von LOD0,
    /// Blatt-Material mit Basistextur.</summary>
    public class Modell22BaumKlein02Tests
    {
        private const int BaumIndex = 4; // ids-Reihenfolge in UrwaldSceneBuilder.LoadPHTrees
        private static JungleWorldGenerator _gen;

        private static IEnumerator LadeWelt(int seed)
        {
            JungleWorldGenerator.NextSeed = seed;
            SceneManager.LoadScene("Urwald");
            yield return null;
            float timeout = 60f;
            _gen = null;
            while (timeout > 0f && _gen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) _gen = g;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(_gen, "Welt nicht fertig.");
            for (int i = 0; i < 30; i++) yield return null;
        }

        private static int Dreiecke(Mesh m)
        {
            int n = 0;
            for (int i = 0; i < m.subMeshCount; i++) n += m.GetSubMesh(i).indexCount / 3;
            return n;
        }

        [UnityTest]
        public IEnumerator BaumKlein02_Asvermessung()
        {
            yield return LadeWelt(4242);
            Mesh lod0 = _gen.library.phTreeLod0[BaumIndex];
            Mesh lod1 = _gen.library.phTreeLod1[BaumIndex];
            Assert.IsNotNull(lod0, "phTreeLod0[4] (tree_small_02) fehlt.");
            Assert.IsNotNull(lod1, "phTreeLod1[4] fehlt.");

            int tris0 = Dreiecke(lod0), tris1 = Dreiecke(lod1);
            float hoehe = lod0.bounds.size.y;
            string slots = _gen.library.phTreeSlotNames != null &&
                BaumIndex < _gen.library.phTreeSlotNames.Length
                    ? _gen.library.phTreeSlotNames[BaumIndex] : "";
            Debug.Log(string.Format(
                "[MODELL22] Baum_Klein02: Tris LOD0 {0}, LOD1 {1}, Hoehe {2:0.00} m, SubMeshes {3}, Slots [{4}]",
                tris0, tris1, hoehe, lod0.subMeshCount, slots));

            Assert.GreaterOrEqual(tris0, 8000, "LOD0 unter 8000 Dreiecken.");
            Assert.LessOrEqual(tris0, 20000, "LOD0 ueber 20000 Dreiecken.");
            Assert.GreaterOrEqual(hoehe, 6f, "Asset unter 6 m hoch.");
            Assert.LessOrEqual(hoehe, 9f, "Asset ueber 9 m hoch: Builder-Skala pruefen.");
            Assert.AreEqual(2, lod0.subMeshCount, "LOD0 braucht genau 2 Submeshes (Stamm, Blatt).");

            string slotsLower = (slots ?? "").ToLower();
            Assert.That(slotsLower, Does.Contain("trunk"), "Slot-Namen ohne 'trunk': " + slots);
            Assert.That(slotsLower, Does.Contain("leaves"), "Slot-Namen ohne 'leaves': " + slots);

            float anteil = (float)tris1 / Mathf.Max(1, tris0);
            Assert.GreaterOrEqual(anteil, 0.25f, "LOD1 zu duenn (<25 % von LOD0).");
            Assert.LessOrEqual(anteil, 0.6f, "LOD1 zu fett (>60 % von LOD0).");

            var leafMat = _gen.library.phTreeLeafMats[BaumIndex];
            Assert.IsNotNull(leafMat, "Blatt-Material fehlt.");
            Assert.IsNotNull(leafMat.GetTexture("_BaseColorMap"), "Blatt-Basistextur fehlt.");
        }
    }
}
