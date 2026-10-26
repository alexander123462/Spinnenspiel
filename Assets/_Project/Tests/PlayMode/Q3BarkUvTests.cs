using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>Q3: The bark UV-tiling of the PH-Tree Wood materials is computed per meter
    /// from the trunk size (NOT fixed 4x10): one texture repeat covers about 1 m of trunk,
    /// so no diagonal stretched stripes remain. Ground truth via library.phTreeWoodMats
    /// (_BaseColorMap_ST) versus library.phTreeLod0/phTreeScales (measured in Build-Log).</summary>
    public class Q3BarkUvTests
    {
        [UnityTest]
        public IEnumerator Q3_BarkTilingIsPerMeterNotFixed()
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
            Assert.IsNotEmpty(lib.phTreeWoodMats, "No PH-Tree wood materials.");
            Assert.AreEqual(lib.phTreeLod0.Length, lib.phTreeWoodMats.Length, "Mesh/Material count mismatch.");

            for (int i = 0; i < lib.phTreeWoodMats.Length; i++)
            {
                var wood = lib.phTreeWoodMats[i];
                Assert.IsNotNull(wood, "Wood material " + i + " null.");
                var st = wood.GetVector("_BaseColorMap_ST");

                // Hoehe des Holzes: Holz-Submesh (Slot-Name trunk/wood/bark), sonst ganzer Baum. Der Jacaranda hat im Holz-Slot nur den nackten Stamm.
                float worldH = lib.phTreeLod0[i].bounds.size.y * lib.phTreeScales[i];
                string[] slots = (lib.phTreeSlotNames != null && i < lib.phTreeSlotNames.Length && lib.phTreeSlotNames[i] != null) ? lib.phTreeSlotNames[i].Split('|') : new string[0];
                for (int k = 0; k < slots.Length && k < lib.phTreeLod0[i].subMeshCount; k++)
                {
                    string sn = slots[k].ToLowerInvariant();
                    if (sn.Contains("trunk") || sn.Contains("wood") || sn.Contains("bark")) { worldH = lib.phTreeLod0[i].GetSubMesh(k).bounds.size.y * lib.phTreeScales[i]; break; }
                }
                float metersPerRepeatV = worldH / Mathf.Max(0.01f, st.y);
                Debug.Log(string.Format("[Q3] {0}: worldH={1:0.0} m ST=({2:0.0}x{3:0.0}) V-Meter/Repeat={4:0.00}",
                    wood.name, worldH, st.x, st.y, metersPerRepeatV));

                // The old fix was fixed 4x10 -> must be gone (per-meter values differ per species).
                Assert.IsFalse(Mathf.Abs(st.x - 4f) < 0.01f && Mathf.Abs(st.y - 10f) < 0.01f, wood.name + " still has the old fixed 4x10 tiling.");

                // One repeat must cover roughly one meter of trunk height (Backlog criterion),
                // so worldH / ST.y stays in a sane band around 1 m (not 30 m as with ST=1).
                Assert.GreaterOrEqual(metersPerRepeatV, 0.2f, wood.name + ": repeat covers > 5 m (too coarse).");
                Assert.Less(metersPerRepeatV, 3f, wood.name + ": repeat < 0.33 m (too fine).");
            }
        }
    }
}
