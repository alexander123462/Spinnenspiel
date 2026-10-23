using System.Collections;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>KLEE1: Klee-Bodenteppich (nur HINZUGEFUEGT; alte Bodendecker bleiben).
    /// Kriterien als Zeilen 'Wert -> Kriterium -> OK/NOK' im Log.</summary>
    public class KleeTests
    {
        [UnityTest]
        public IEnumerator Clover_CarpetAroundSpawnAndNoColliders()
        {
            JungleWorldGenerator.NextSeed = 4242;
            SceneManager.LoadScene("Urwald");
            yield return null;
            JungleWorldGenerator gen = null;
            float timeout = 20f;
            while (timeout > 0f && gen == null)
            {
                var g = Object.FindFirstObjectByType<JungleWorldGenerator>();
                if (g != null && g.IsReady) gen = g;
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(gen != null && gen.IsReady, "World not ready in time.");

            // Kriterium 1: mindestens 3000 Klee-Instanzen im 10-m-Umkreis der Spinne.
            Debug.Log(string.Format("[KLEE1] CloverNearSpawn={0} -> Kriterium >= 3000 -> {1}",
                gen.CloverNearSpawnCount, gen.CloverNearSpawnCount >= 3000 ? "OK" : "NOK"));
            Assert.GreaterOrEqual(gen.CloverNearSpawnCount, 3000, "Too few clover instances near spawn.");

            // Kriterium 2: Draw Calls — Klee besteht aus wenigen Block-Renderern (max 20).
            var klee = GameObject.Find("Klee");
            Assert.IsNotNull(klee, "No Klee root node.");
            var rend = klee.GetComponentsInChildren<MeshRenderer>();
            Debug.Log(string.Format("[KLEE1] KleeBlockRenderer={0} -> Kriterium <= 20 -> {1}",
                rend.Length, rend.Length <= 20 ? "OK" : "NOK"));
            Assert.LessOrEqual(rend.Length, 20, "Clover is not merged into few blocks (too many draw calls).");

            // Ein gemeinsames Material, Alpha-Cutout aktiv.
            var mat = rend[0].sharedMaterial;
            foreach (var r in rend) Assert.AreSame(mat, r.sharedMaterial, "Clover uses more than one material.");
            Debug.Log(string.Format("[KLEE1] AlphaCutoffEnable={0} -> Kriterium = 1 -> {1}",
                mat.HasFloat("_AlphaCutoffEnable") ? mat.GetFloat("_AlphaCutoffEnable") : -1f,
                mat.HasFloat("_AlphaCutoffEnable") && mat.GetFloat("_AlphaCutoffEnable") > 0.5f ? "OK" : "NOK"));

            // Keine Collider an Klee-Objekten.
            Assert.IsEmpty(klee.GetComponentsInChildren<Collider>(), "Clover must not have colliders.");

            // Alte Bodendecker NICHT reduziert: GroundCover > 0 und Bodendecker-Wurzel vorhanden.
            var boden = GameObject.Find("Bodendecker");
            Assert.IsNotNull(boden, "Old ground cover root 'Bodendecker' is gone.");
            Debug.Log(string.Format("[KLEE1] GroundCover={0} -> Kriterium > 0 (nicht reduziert) -> {1}",
                gen.GroundCoverCount, gen.GroundCoverCount > 0 ? "OK" : "NOK"));
            Assert.Greater(gen.GroundCoverCount, 0, "Old ground cover was reduced or removed.");

            // Beweisbild: Bodennahaufnahme mit Klee-Teppich.
            Terrain terrain = Terrain.activeTerrain;
            Assert.IsNotNull(terrain, "No active Terrain for the proof shot.");
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.45f);
            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "No main camera.");
            var spiderCam = cam.GetComponent<SpiderCamera>();
            if (spiderCam != null) spiderCam.enabled = false;

            // Ziel: Mitte der naechsten Klee-Blockbounds (Block-Transforme sitzen bei 0, Meshes sind
            // in Weltkoordinaten kombiniert).
            Bounds bb = rend[0].bounds;
            foreach (var r in rend)
            {
                float d = (r.bounds.center - bb.center).sqrMagnitude;
                if (d < 400f) bb.Encapsulate(r.bounds);
            }
            Vector3 spawn = bb.center;
            foreach (var sr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                bool sp = false;
                foreach (var m in sr.sharedMaterials) if (m != null && m.name.StartsWith("MAT_Spider")) sp = true;
                if (sp) { spawn = sr.bounds.center; break; }
            }
            Debug.Log("[KLEE1] bbCenter=" + bb.center + " bbSize=" + bb.size + " proofTarget=" + spawn);
            float gy = terrain.SampleHeight(spawn) + terrain.transform.position.y;
            // Wie Wald7MoosTests: Auge ~1.1 m ueber Grund, ~3 m Abstand, Blick leicht nach unten.
            Vector3 eye = new Vector3(spawn.x + 2.6f, gy + 1.1f, spawn.z + 1.4f);
            cam.transform.position = eye;
            cam.transform.LookAt(new Vector3(spawn.x, gy + 0.1f, spawn.z));
            Debug.Log(string.Format("[KLEE1] cam eye={0} target=({1:F1},{2:F2},{3:F1}) gy={2:F2}",
                eye, spawn.x, gy, spawn.z));
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
            // Wie die Galerie (Q1): kein TAA/Bewegungsunschaerfe/Nebel, sonst einfarbig grau.
            Q1ObjektGalerieTests.DisableDepthAndMotionBlur();
            Q1ObjektGalerieTests.AddOverrideVolume();
            Q1ObjektGalerieTests.NoTemporalAA(cam);
            Q1ObjektGalerieTests.SetFog(false);
            yield return null;
            for (int rest = 0; rest < 3; rest++) yield return null; // HDRP resting frames
            var veg = Object.FindFirstObjectByType<VegetationSystem>();
            if (veg != null) veg.RenderForCamera(cam);
            string file = "Logs/ci/klee_teppich.png";
            Vector4 st = UrwaldTests.SaveFrameHdr(cam, 1024, 768, file);
            Debug.Log(string.Format("[KLEE1] proof {0}: Mittel={1} Max={2}",
                file, st.x.ToString("F0"), st.y.ToString("F0")));
        }

        [Test]
        public void CloverMesh_IsDeterministicUnitShape()
        {
            var a = CloverFactory.Clover(7);
            Assert.That(a.vertices.Length, Is.GreaterThan(60), "Clover has too few vertices.");
            // Blattdach liegt bei y~1 (leicht geneigte Lappen: gemessen 0.914).
            Assert.That(a.bounds.size.y, Is.EqualTo(1f).Within(0.1f), "Clover not unit height.");
            Assert.That(a.bounds.min.y, Is.GreaterThanOrEqualTo(-0.01f), "Clover dips below its base.");
            var b = CloverFactory.Clover(7);
            Assert.AreEqual(a.vertices.Length, b.vertices.Length, "Clover not deterministic from seed.");
            Assert.AreEqual(a.vertices[0], b.vertices[0], "Clover vertices differ for equal seed.");
            var c = CloverFactory.Clover(1031);
            float sa = 0f, sc = 0f;
            foreach (var v in a.vertices) sa += v.x * 3.1f + v.y * 7.7f + v.z * 13.3f;
            foreach (var v in c.vertices) sc += v.x * 3.1f + v.y * 7.7f + v.z * 13.3f;
            Assert.That(sc, Is.Not.EqualTo(sa), "Clover seed has no effect.");
        }
    }
}
