using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Spinnenspiel;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Spinnenspiel.Tests
{
    /// <summary>
    /// Q1 Objekt-Galerie: saubere Bilder je Objekttyp (Studio hoch ueber der Welt, grauer Boden,
    /// drei Ansichten) plus ein Bild aus der Spielperspektive. Katalog aller Materialien separat.
    /// Ausgabe: Logs/ci/galerie/*.png, index.txt, katalog.txt.
    /// </summary>
    public class Q1ObjektGalerieTests
    {
        private struct Typ
        {
            public string Name;
            public string Tag;
            public bool ParentRoot;
            public Typ(string name, string tag, bool parentRoot = false) { Name = name; Tag = tag; ParentRoot = parentRoot; }
        }

        private static readonly Typ[] Typen =
        {
            new Typ("Baum_Island01", "MAT_PHTree_island_tree_01_Wood"),
            new Typ("Baum_Island02", "MAT_PHTree_island_tree_02_Wood"),
            new Typ("Baum_Island03", "MAT_PHTree_island_tree_03_Wood"),
            new Typ("Baum_Klein02", "MAT_PHTree_tree_small_02_Wood"),
            new Typ("Baum_Jacaranda", "MAT_PHTree_jacaranda_tree_Wood"),
            new Typ("Farn_Grund", "MAT_Grundfarn_Leaf"),
            new Typ("Farn_Baum", "MAT_Baumfarn_Leaf"),
            new Typ("Farn_Tropisch", "MAT_FernTropisch"),
            new Typ("Farn_Scatter", "MAT_FernScatter_Leaf"),
            new Typ("Riesenblatt1", "MAT_Riesenblatt_1"),
            new Typ("Riesenblatt2", "MAT_Riesenblatt_2"),
            new Typ("Riesenblatt3", "MAT_Riesenblatt_3"),
            new Typ("Gras1", "MAT_Gras1"),  // MODELL1: Blender-Neubau ersetzt Scan grass_medium_01
            new Typ("Gras2", "MAT_Gras2"),  // MODELL2: Blender-Segge ersetzt Scan grass_medium_02
            new Typ("Unkraut", "MAT_Unkraut"),  // MODELL3: Blender-Rosette ersetzt Scan weed_plant_02
            new Typ("Brennnessel", "M_nettle_plant"),
            new Typ("Immergruen", "M_periwinkle_plant"),
            new Typ("Busch1", "MAT_BushTropisch"),
            new Typ("Busch2", "MAT_shrub_03Tropisch"),
            new Typ("Busch3", "MAT_shrub_04Tropisch"),
            new Typ("Schilf", "MAT_Reed"),
            new Typ("Fliege", "MAT_FlyBody", true),
            new Typ("Motte", "MAT_MothBody", true),
        };

        private const int W = 1024;
        private const int H = 768;
        private readonly StringBuilder _index = new StringBuilder();
        private int _images;

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
            for (int i = 0; i < 120; i++) yield return null;
            done(gen);
        }

        [UnityTest]
        public IEnumerator Q1_Katalog()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            var counts = new Dictionary<string, int>();
            var sizes = new Dictionary<string, Vector3>();
            var samples = new Dictionary<string, string>();
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var mf = mr.GetComponent<MeshFilter>();
                string mesh = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "-";
                foreach (var m in mr.sharedMaterials)
                {
                    string key = (m != null ? m.name : "null") + " | " + mesh;
                    counts.TryGetValue(key, out int c);
                    counts[key] = c + 1;
                    if (!sizes.ContainsKey(key)) { sizes[key] = mr.bounds.size; samples[key] = mr.gameObject.name; }
                }
            }
            var sb = new StringBuilder();
            sb.AppendLine("Material | Mesh | Anzahl | Groesse | Beispielobjekt");
            foreach (var kv in counts)
                sb.AppendLine(kv.Key + " | " + kv.Value + " | " + sizes[kv.Key].ToString("F2") + " | " + samples[kv.Key]);
            System.IO.Directory.CreateDirectory("Logs/ci/galerie");
            System.IO.File.WriteAllText("Logs/ci/galerie/katalog.txt", sb.ToString());
            Debug.Log("[Q1] Katalog: " + counts.Count + " Material|Mesh-Kombinationen");
        }

        internal static void DisableDepthAndMotionBlur()
        {
            foreach (var volume in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
            {
                if (volume.sharedProfile == null) continue;
                foreach (var comp in volume.sharedProfile.components)
                {
                    string n = comp.GetType().Name;
                    if (n.EndsWith("DepthOfField") || n.EndsWith("MotionBlur")) comp.active = false;
                }
            }
        }

        internal static void NoTemporalAA(Camera cam)
        {
            // HDRP-TAA/Verlauf verschmiert Offscreen-Aufnahmen nach Kamerasprung (Radialstreifen): ausschalten.
            var hd = cam.GetComponent("HDAdditionalCameraData");
            if (hd == null) return;
            var prop = hd.GetType().GetProperty("antialiasing");
            if (prop != null) prop.SetValue(hd, System.Enum.ToObject(prop.PropertyType, 0));
        }

        private static UnityEngine.Rendering.VolumeComponent _fogOverride;

        private static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private static void SetParam(UnityEngine.Rendering.VolumeComponent comp, string field, object value)
        {
            var f = comp.GetType().GetField(field);
            if (f == null) { Debug.Log("[Q1] Parameter fehlt: " + comp.GetType().Name + "." + field); return; }
            var p = f.GetValue(comp) as UnityEngine.Rendering.VolumeParameter;
            p.overrideState = true;
            p.GetType().GetProperty("value").SetValue(p, value);
        }

        // Globales Override-Volume: Bewegungsunschaerfe aus (die HDRP-Default-Volume-Profile ist kein Szenen-Volume).
        internal static void AddOverrideVolume()
        {
            var go = new GameObject("GalerieOverride");
            var vol = go.AddComponent<UnityEngine.Rendering.Volume>();
            vol.isGlobal = true;
            vol.priority = 1000f;
            vol.weight = 1f;
            var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            vol.sharedProfile = prof;
            var mb = FindType("UnityEngine.Rendering.HighDefinition.MotionBlur");
            if (mb != null) SetParam(prof.Add(mb, false), "intensity", 0f);
            var fog = FindType("UnityEngine.Rendering.HighDefinition.Fog");
            if (fog != null) _fogOverride = prof.Add(fog, false);
        }

        internal static void SetFog(bool on)
        {
            if (_fogOverride == null) return;
            var f = _fogOverride.GetType().GetField("enabled");
            if (f == null) return;
            var p = f.GetValue(_fogOverride) as UnityEngine.Rendering.VolumeParameter;
            p.overrideState = !on;
            p.GetType().GetProperty("value").SetValue(p, false);
        }

        private static Bounds SpiderBounds(GameObject go)
        {
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                bool spiderMat = false;
                foreach (var m in r.sharedMaterials) if (m != null && m.name.StartsWith("MAT_Spider")) spiderMat = true;
                if (!spiderMat) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in rs)
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        private static MeshRenderer FindNearest(string tag, Vector3 from)
        {
            MeshRenderer best = null;
            float bestD = float.MaxValue;
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (mr.bounds.center.y > 900f || mr.bounds.size.magnitude > 150f) continue;
                bool hit = false;
                foreach (var m in mr.sharedMaterials) if (m != null && m.name == tag) { hit = true; break; }
                if (!hit) continue;
                float d = (mr.bounds.center - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = mr; }
            }
            return best;
        }

        private IEnumerator Shot(Camera cam, string name, string view, VegetationSystem veg)
        {
            // Kamera ruht: 3 Frames warten, damit HDRP-Verlauf/Velocity nicht verschmiert (Radialstreifen).
            yield return null; yield return null; yield return null;
            if (veg != null) veg.RenderForCamera(cam);
            string file = "Logs/ci/galerie/" + name + "_" + view + ".png";
            Vector4 st = UrwaldTests.SaveFrameHdr(cam, W, H, file);
            float magPct = 100f * st.z / (W * H);
            bool ok = st.x >= 30f && st.x <= 220f && magPct < 0.5f;
            _index.AppendLine(file + ";" + name + ";mittel=" + st.x.ToString("F0") + ";max=" + st.y.ToString("F0")
                              + ";magenta%=" + magPct.ToString("F2") + ";SharpFrac=" + st.w.ToString("P1") + ";" + (ok ? "OK" : "NOK"));
            _images++;
        }

        private static void PlaceCam(Camera cam, Bounds b, Vector3 dir)
        {
            float r = Mathf.Max(b.extents.magnitude, 0.005f);
            float dist = r / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.85f;
            cam.nearClipPlane = Mathf.Max(0.005f, dist * 0.02f);
            cam.farClipPlane = dist * 8f + 200f;
            cam.transform.position = b.center + dir.normalized * dist;
            cam.transform.LookAt(b.center);
        }

        private IEnumerator Views(Camera cam, string name, Bounds b, VegetationSystem veg)
        {
            PlaceCam(cam, b, new Vector3(0f, 0.25f, -1f)); yield return Shot(cam, name, "vorne", veg);
            PlaceCam(cam, b, new Vector3(1f, 0.25f, 0f)); yield return Shot(cam, name, "seite", veg);
            PlaceCam(cam, b, new Vector3(0f, 1f, -0.05f)); yield return Shot(cam, name, "oben", veg);
        }

        private static void Ground(GameObject floor, Bounds b)
        {
            floor.transform.position = new Vector3(b.center.x, b.min.y - 0.005f, b.center.z);
            float fs = Mathf.Max(b.size.x, b.size.z, 0.2f) * 6f;
            floor.transform.localScale = new Vector3(fs, 0.01f, fs);
        }

        [UnityTest]
        public IEnumerator Q1_Galerie()
        {
            JungleWorldGenerator gen = null;
            yield return LoadWorld(g => gen = g);

            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null) cycle.SetDayProgress(0.45f);
            DisableDepthAndMotionBlur();
            AddOverrideVolume();
            yield return null;

            Camera cam = Camera.main;
            NoTemporalAA(cam);
            SetFog(false);
            var spiderCam = cam.GetComponent<SpiderCamera>();
            if (spiderCam != null) spiderCam.enabled = false;
            var veg = Object.FindFirstObjectByType<VegetationSystem>();
            var spider = Object.FindFirstObjectByType<SpiderMovement>();
            Assert.IsNotNull(spider, "Spinne fehlt.");
            Vector3 sp = spider.transform.position;
            System.IO.Directory.CreateDirectory("Logs/ci/galerie");
            foreach (var f in System.IO.Directory.GetFiles("Logs/ci/galerie", "*.png")) System.IO.File.Delete(f);

            // Studio hoch ueber der Welt: grauer Boden, nur Himmel dahinter.
            float half = gen.World.Size / 2f;
            var studioPos = new Vector3(half, 1000f, half);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "GalerieBoden";
            Object.Destroy(floor.GetComponent<Collider>());
            var gray = new Material(Shader.Find("HDRP/Lit"));
            gray.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.5f, 1f));
            floor.GetComponent<MeshRenderer>().sharedMaterial = gray;

            int typCount = 0;

            foreach (var t in Typen)
            {
                var mr = FindNearest(t.Tag, sp);
                if (mr == null)
                {
                    _index.AppendLine("FEHLT;" + t.Name + ";kein Objekt mit Material " + t.Tag);
                    continue;
                }
                Bounds worldB = mr.bounds;
                GameObject src = t.ParentRoot ? mr.transform.parent.gameObject : mr.gameObject;
                var clone = Object.Instantiate(src);
                clone.name = "Galerie_" + t.Name;
                clone.SetActive(true);
                clone.isStatic = false;
                foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
                foreach (var lg in clone.GetComponentsInChildren<LODGroup>()) lg.enabled = false;
                foreach (var rr in clone.GetComponentsInChildren<Renderer>()) { rr.enabled = true; rr.forceRenderingOff = false; }
                clone.transform.SetParent(null);
                clone.transform.localScale = src.transform.lossyScale;
                clone.transform.rotation = Quaternion.identity;
                clone.transform.position = studioPos;
                yield return null;
                Bounds b = BoundsOf(clone);
                clone.transform.position += new Vector3(studioPos.x - b.center.x, studioPos.y - b.min.y, studioPos.z - b.center.z);
                b = BoundsOf(clone);
                Ground(floor, b);
                yield return Views(cam, t.Name, b, null);
                Object.Destroy(clone);
                typCount++;

                Vector3 target = worldB.center;
                Vector3 toSp = sp - target; toSp.y = 0f;
                if (toSp.sqrMagnitude < 0.001f) toSp = Vector3.back;
                float d = Mathf.Max(0.8f, worldB.extents.magnitude * 2.2f);
                cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
                cam.transform.position = target + toSp.normalized * d + Vector3.up * Mathf.Min(0.4f + worldB.extents.y * 0.3f, 3f);
                cam.transform.LookAt(target);
                SetFog(true); yield return Shot(cam, t.Name, "spiel", veg); SetFog(false);
                yield return null;
            }

            var lib = gen.library;
            var libTypen = new List<(string n, Mesh m, Material mat)>();
            if (lib != null && lib.logs != null && lib.logs.Length > 0) libTypen.Add(("Totholz", lib.logs[0], lib.logMat));
            if (lib != null && lib.rocks != null && lib.rocks.Length > 0) libTypen.Add(("Stein", lib.rocks[0], lib.rockMat));
            foreach (var lt in libTypen)
            {
                var go = new GameObject("Galerie_" + lt.n);
                go.AddComponent<MeshFilter>().sharedMesh = lt.m;
                go.AddComponent<MeshRenderer>().sharedMaterial = lt.mat;
                go.transform.position = studioPos;
                Bounds b = BoundsOf(go);
                go.transform.position += new Vector3(studioPos.x - b.center.x, studioPos.y - b.min.y, studioPos.z - b.center.z);
                b = BoundsOf(go);
                Ground(floor, b);
                yield return null;
                yield return Views(cam, lt.n, b, null);
                Object.Destroy(go);
                typCount++;
            }
            Object.Destroy(floor);
            SetFog(true);

            {
                Bounds b = SpiderBounds(spider.gameObject);
                yield return Views(cam, "Spinne", b, veg);
                PlaceCam(cam, b, new Vector3(0.7f, 0.5f, -1f));
                yield return Shot(cam, "Spinne", "spiel", veg);
                typCount++;
            }

            {
                Vector3 hub = sp + new Vector3(1.2f, 0.8f, 0f);
                Vector3[] anchors =
                {
                    hub + new Vector3(0f, 1.1f, -1.1f), hub + new Vector3(0f, 1.1f, 1.1f),
                    hub + new Vector3(0f, -1.1f, 1.1f), hub + new Vector3(0f, -1.1f, -1.1f),
                };
                OrbWeb web = WebBuildDemo.CreateWebAt(anchors, null, "GalerieNetz");
                if (web != null)
                {
                    web.SetProgress(OrbWeb.WebStage.Done, 1f);
                    yield return null;
                    var b = new Bounds(hub, new Vector3(0.4f, 2.4f, 2.4f));
                    PlaceCam(cam, b, new Vector3(-1f, 0.1f, 0f)); yield return Shot(cam, "Netz", "vorne", veg);
                    PlaceCam(cam, b, new Vector3(-0.3f, 0.1f, -1f)); yield return Shot(cam, "Netz", "seite", veg);
                    PlaceCam(cam, b, new Vector3(-0.2f, 1f, 0f)); yield return Shot(cam, "Netz", "oben", veg);
                    PlaceCam(cam, b, new Vector3(-1f, 0.3f, -0.8f)); yield return Shot(cam, "Netz", "spiel", veg);
                    typCount++;
                }
            }

            {
                MeshRenderer wr = null;
                foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    foreach (var m in mr.sharedMaterials) if (m != null && m.name == "MAT_WaterPond") wr = mr;
                if (wr != null)
                {
                    Bounds wb = wr.bounds;
                    cam.nearClipPlane = 0.05f; cam.farClipPlane = 500f;
                    // Q5: niedrige Kamera ueber der Wasserflaeche, Blick HORIZONTIELL zum Ufer.
                    // Sky/vegetation oben, water below -> image not upside-down anymore.
                    Vector3 pc = wb.center;
                    float rad = Mathf.Max(wb.extents.x, wb.extents.z);
                    // Cam 1: shore cam just above the surface, looking ACROSS the pond.
                    // Water surface fills the lower half, far shoreline mid, canopy top -> upright,
                    // no longer "on the head" (the old shot had the pale water/fog on top).
                    Vector3 c = new Vector3(pc.x - rad * 0.9f, wb.max.y + 1.2f, pc.z);
                    cam.transform.position = c;
                    cam.transform.LookAt(new Vector3(pc.x + rad * 0.5f, wb.max.y - 0.1f, pc.z));
                    yield return Shot(cam, "Wasser", "spiel", veg);
                    // Cam 2: from above, looking down at surface -> shows organic jittered shoreline.
                    cam.transform.position = new Vector3(pc.x, wb.max.y + 7f, pc.z - rad * 0.4f);
                    cam.transform.LookAt(new Vector3(pc.x, wb.max.y, pc.z));
                    yield return Shot(cam, "Wasser", "oben", veg);
                    typCount++;
                }
            }

            System.IO.File.WriteAllText("Logs/ci/galerie/index.txt", _index.ToString());
            Debug.Log("[Q1] Galerie: " + typCount + " Typen, " + _images + " Bilder");
            Assert.Greater(_images, 20, "Zu wenige Galerie-Bilder: " + _images);
        }
    }
}
