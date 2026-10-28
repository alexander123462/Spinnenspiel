using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>MODELL25: Baum_Island03 (island_tree_03, Index 3 in phTree-Listen) neu als
    /// prozedurales Blender-Asset (anderer Wuchs: geneigter Leittrieb, horizontale
    /// Schirmaeste, flaches Blattdach). Misst am geladenen Asset: LOD0 15000-30000
    /// Dreiecke, Mesh-Hoehe 12-15 m, genau 2 Submeshes, Slot-Namen mit trunk/leaves,
    /// LOD1 25-60 % von LOD0, Blatt-Material mit Basistextur, Builder-Skala ~1,
    /// Ausladung mind. 90 % der Höhe (Schirmform != schlanker Wuchs von Insel 1/2).</summary>
    public class Modell25BaumIsland03Tests
    {
        private const int BaumIndex = 3; // ids-Reihenfolge in UrwaldSceneBuilder.LoadPHTrees
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
        public IEnumerator BaumIsland03_Asvermessung()
        {
            yield return LadeWelt(4242);
            Mesh lod0 = _gen.library.phTreeLod0[BaumIndex];
            Mesh lod1 = _gen.library.phTreeLod1[BaumIndex];
            Assert.IsNotNull(lod0, "phTreeLod0[3] (island_tree_03) fehlt.");
            Assert.IsNotNull(lod1, "phTreeLod1[3] fehlt.");

            int tris0 = Dreiecke(lod0), tris1 = Dreiecke(lod1);
            float hoehe = lod0.bounds.size.y;
            float scale = _gen.library.phTreeScales[BaumIndex];
            string slots = _gen.library.phTreeSlotNames != null &&
                BaumIndex < _gen.library.phTreeSlotNames.Length
                    ? _gen.library.phTreeSlotNames[BaumIndex] : "";
            Debug.Log(string.Format(
                "[MODELL25] Baum_Island03: Tris LOD0 {0}, LOD1 {1}, Hoehe {2:0.00} m, Scale {3:0.00} " +
                "(Welt {4:0.0} m), SubMeshes {5}, Slots [{6}], Breite {7:0.0} x Tiefe {8:0.0}",
                tris0, tris1, hoehe, scale, hoehe * scale, lod0.subMeshCount, slots,
                lod0.bounds.size.x, lod0.bounds.size.z));

            Assert.GreaterOrEqual(tris0, 15000, "LOD0 unter 15000 Dreiecken.");
            Assert.LessOrEqual(tris0, 30000, "LOD0 ueber 30000 Dreiecken.");
            Assert.GreaterOrEqual(hoehe, 12f, "Asset unter 12 m hoch.");
            Assert.LessOrEqual(hoehe, 15f, "Asset ueber 15 m hoch: Builder-Skala pruefen.");
            Assert.AreEqual(2, lod0.subMeshCount, "LOD0 braucht genau 2 Submeshes (Stamm, Blatt).");

            string slotsLower = (slots ?? "").ToLower();
            Assert.That(slotsLower, Does.Contain("trunk"), "Slot-Namen ohne 'trunk': " + slots);
            Assert.That(slotsLower, Does.Contain("leaves"), "Slot-Namen ohne 'leaves': " + slots);

            // Neubau in Weltgroesse: Builder skaliert ~1x (Ziel 12 m), nicht mehr 3-5x wie beim Scan.
            Assert.GreaterOrEqual(scale, 0.7f, "Builder-Skala < 0.7: Klemme 3-5 greift noch?");
            Assert.LessOrEqual(scale, 1.3f, "Builder-Skala > 1.3: Klemme 3-5 greift noch?");
            float weltH = hoehe * scale;
            Assert.GreaterOrEqual(weltH, 9f, "Welt-Hoehe unter 9 m (Kriterium 9-20).");
            Assert.LessOrEqual(weltH, 20f, "Welt-Hoehe ueber 20 m (Kriterium 9-20).");

            float anteil = (float)tris1 / Mathf.Max(1, tris0);
            Assert.GreaterOrEqual(anteil, 0.25f, "LOD1 zu duenn (<25 % von LOD0).");
            Assert.LessOrEqual(anteil, 0.6f, "LOD1 zu fett (>60 % von LOD0).");

            // Anderer Wuchs: flacher Schirm = breite Ausladung (schlanke Inselbaeume 1/2
            // liegen deutlich unter diesem Wert).
            float spreizung = Mathf.Max(lod0.bounds.size.x, lod0.bounds.size.z) / Mathf.Max(0.01f, hoehe);
            Assert.GreaterOrEqual(spreizung, 0.9f,
                "Ausladung/Hoehe < 0.9: kein breiter Schirm (Schwache des Altmodells)?");

            var leafMat = _gen.library.phTreeLeafMats[BaumIndex];
            Assert.IsNotNull(leafMat, "Blatt-Material fehlt.");
            Assert.IsNotNull(leafMat.GetTexture("_BaseColorMap"), "Blatt-Basistextur fehlt.");
        }
    }
}
