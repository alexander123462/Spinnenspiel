using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spinnenspiel.EditorTools
{
    /// <summary>
    /// Rendert Kontrollbilder der Testwelt (auch im Batchmode) nach Logs/ci/shot_*.png,
    /// damit Aussehen und Aufbau ohne Play-Modus geprüft werden können.
    /// </summary>
    public static class ScreenshotTool
    {
        private struct View
        {
            public string name;
            public Vector3 position;
            public Vector3 lookAt;

            public View(string name, Vector3 position, Vector3 lookAt)
            {
                this.name = name;
                this.position = position;
                this.lookAt = lookAt;
            }
        }

        [MenuItem("Spinnenspiel/Debug/Kontrollbilder rendern")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(TestWorldBuilder.ScenePath, OpenSceneMode.Single);
            Camera cam = Camera.main;
            GameObject spider = GameObject.Find("Spider");
            if (cam == null || spider == null)
            {
                Debug.LogError("[Spinnenspiel] Screenshot: Kamera oder Spinne fehlt.");
                return;
            }

            Physics.SyncTransforms();
            foreach (SpiderLeg leg in spider.GetComponentsInChildren<SpiderLeg>())
            {
                leg.PoseAtRest();
            }

            Vector3 s = spider.transform.position;
            float streamX = 40f;
            Vector3 stream = new Vector3(streamX, WorldLayout.StreamWaterHeight(streamX), WorldLayout.StreamCenterZ(streamX));
            View[] views =
            {
                new View("spinne_nah", s + new Vector3(0.25f, 0.18f, -0.35f), s + new Vector3(0f, 0.05f, 0f)),
                new View("spielkamera", s + new Vector3(0f, 0.38f, -0.95f), s + new Vector3(0f, 0.12f, 0.4f)),
                new View("parcours", s + new Vector3(-1.2f, 0.9f, -0.6f), s + new Vector3(0.3f, 0f, 2.5f)),
                new View("uebersicht", new Vector3(20f, 14f, 4f), new Vector3(34f, 3f, 34f)),
                new View("bach", stream + new Vector3(-2.5f, 0.8f, -2.2f), stream),
                new View("wald", new Vector3(30f, 5.5f, 44f), new Vector3(38f, 4.5f, 56f))
            };

            VegetationSystem vegetation = Object.FindFirstObjectByType<VegetationSystem>();
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ci");
            Directory.CreateDirectory(folder);

            RenderTexture target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            foreach (View view in views)
            {
                cam.transform.position = view.position;
                cam.transform.LookAt(view.lookAt);
                cam.targetTexture = target;
                if (vegetation != null)
                {
                    vegetation.RenderForCamera(cam);
                }

                cam.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(folder, "shot_" + view.name + ".png"), image.EncodeToPNG());
                Debug.Log("[Spinnenspiel] Kontrollbild: shot_" + view.name + ".png");
            }

            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }

        [MenuItem("Spinnenspiel/Debug/Urwald-Kontrollbilder rendern")]
        public static void CaptureUrwald()
        {
            EditorSceneManager.OpenScene(UrwaldSceneBuilder.ScenePath, OpenSceneMode.Single);
            Camera cam = Camera.main;
            GameObject spider = GameObject.Find("Spider");
            if (cam == null || spider == null)
            {
                Debug.LogError("[Spinnenspiel] Screenshot: Kamera oder Spinne fehlt (Urwald).");
                return;
            }

            Physics.SyncTransforms();
            DayNightCycle dayCycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (dayCycle != null)
            {
                dayCycle.SetDayProgress(0.35f); // batch render: Start() never runs -> midnight fix
            }
            // SetDayProgress only sets the fraction; sun pose is applied in Update() which does NOT
            // run in batch mode -> apply morning sun (elev ~+40 deg) directly.
            Light sunLight = RenderSettings.sun;
            if (sunLight == null)
            {
                foreach (Light l in spider.GetComponentsInChildren<Light>()) { sunLight = l; break; }
            }
            if (sunLight != null)
            {
                float frac = 0.35f;
                float phase = (frac - 0.25f) * Mathf.PI * 2f;
                float elevDeg = Mathf.Asin(Mathf.Sin(phase)) * Mathf.Rad2Deg;
                float azDeg = phase * Mathf.Rad2Deg + 90f;
                sunLight.transform.rotation = Quaternion.Euler(elevDeg, azDeg, 0f);
                DynamicGI.UpdateEnvironment();
            }

            foreach (SpiderLeg leg in spider.GetComponentsInChildren<SpiderLeg>())
            {
                leg.PoseAtRest();
            }

            Vector3 s = spider.transform.position;
            Debug.Log("[Spinnenspiel] Urwald Spider at " + s.ToString("F1"));
            // Absolute views (spider is scaled up in the Urwald; relative offsets land inside its body).
            Vector3 hub = new Vector3(49.5f, 8.4f, 50.8f); // known PH-Tree position (tests)
            View[] views =
            {
                new View("urwald_play", hub + new Vector3(-3f, 1.2f, -3f), hub + new Vector3(1f, 0.5f, 1f)),
                new View("urwald_ueber", hub + new Vector3(-14f, 22f, -26f), hub + new Vector3(0f, 1f, 0f)),
                new View("urwald_west", hub + new Vector3(8f, 2.5f, 2f), hub + new Vector3(-10f, 2f, 6f)),
                new View("urwald_treefoot", hub + new Vector3(2.2f, 0.9f, -2.2f), hub + new Vector3(0f, -0.2f, 0.5f))
            };

            VegetationSystem vegetation = Object.FindFirstObjectByType<VegetationSystem>();
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ci");
            Directory.CreateDirectory(folder);

            RenderTexture target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            foreach (View view in views)
            {
                cam.transform.position = view.position;
                cam.transform.LookAt(view.lookAt);
                cam.targetTexture = target;
                if (vegetation != null)
                {
                    vegetation.RenderForCamera(cam);
                }

                cam.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(folder, "shot_" + view.name + ".png"), image.EncodeToPNG());
                Debug.Log("[Spinnenspiel] Kontrollbild: shot_" + view.name + ".png");
            }

            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }

        /// <summary>
        /// Rendert Kletter-Kontrollbilder der Spinne (Wand, Stamm, Unterseite des Ueberhangs)
        /// aus der Szene "Klettertest" mit einer temporären, schräg seitlichen Kamera nach
        /// Logs/ci/shot_klettern_*.png. Vor jedem Bild: Pose setzen, Physik synchonisieren,
        /// Beine in Ruhepose bringen.
        /// </summary>
        private struct Pose
        {
            public string name;
            public Vector3 spiderPos;
            public Quaternion spiderRot;
            public Vector3 camOffset; // schräg seitlich, ca. 0.6 m Abstand zur Spinne

            public Pose(string name, Vector3 spiderPos, Quaternion spiderRot, Vector3 camOffset)
            {
                this.name = name;
                this.spiderPos = spiderPos;
                this.spiderRot = spiderRot;
                this.camOffset = camOffset;
            }
        }

        [MenuItem("Spinnenspiel/Debug/Kletter-Kontrollbilder rendern")]
        public static void CaptureClimb()
        {
            const string scenePath = "Assets/_Project/Scenes/Klettertest.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject spider = GameObject.Find("Spider");
            if (spider == null)
            {
                Debug.LogError("[Spinnenspiel] Screenshot: Spinne nicht gefunden (Szene: " + scenePath + ").");
                return;
            }

            SpiderLeg[] legs = spider.GetComponentsInChildren<SpiderLeg>();

            Pose[] poses =
            {
                new Pose("shot_klettern_wand", new Vector3(0f, 1.0f, 2.85f), Quaternion.LookRotation(Vector3.up, Vector3.back), new Vector3(0.55f, 0.05f, -0.30f)),
                new Pose("shot_klettern_stamm", new Vector3(-3f, 1.2f, -0.15f), Quaternion.LookRotation(Vector3.up, Vector3.back), new Vector3(0.55f, 0.05f, 0f)),
                new Pose("shot_klettern_decke", new Vector3(0f, 2.0f, 2.2f), Quaternion.LookRotation(Vector3.back, Vector3.down), new Vector3(0.55f, -0.15f, 0.30f))
            };

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ci");
            Directory.CreateDirectory(folder);

            GameObject camGo = new GameObject("KletterShotCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;

            RenderTexture target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            foreach (Pose pose in poses)
            {
                spider.transform.position = pose.spiderPos;
                spider.transform.rotation = pose.spiderRot;
                Physics.SyncTransforms();
                foreach (SpiderLeg leg in legs)
                {
                    leg.PoseAtRest();
                }

                cam.transform.position = pose.spiderPos + pose.camOffset;
                cam.transform.LookAt(pose.spiderPos);
                cam.targetTexture = target;
                cam.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(folder, pose.name + ".png"), image.EncodeToPNG());
                Debug.Log("[Spinnenspiel] Kletter-Kontrollbild: " + pose.name + ".png");
            }

            // Viertes Bild: Spinne hängt mitten an einem Faden zum Stamm (Seide 4).
            var threadGo = new GameObject("SilkThreadShot");
            SilkThread thread = threadGo.AddComponent<SilkThread>();
            Vector3 tA = new Vector3(3f, 0.4f, 0f);
            Vector3 tB = new Vector3(-2.85f, 1.0f, 0f);
            thread.Init(tA, tB, 0.02f * Vector3.Distance(tA, tB), Vector3.up, Vector3.right);

            Vector3 mid = thread.PointAt(0.5f);
            spider.transform.position = mid;
            spider.transform.rotation = Quaternion.LookRotation(thread.TangentAt(0.5f), Vector3.down);
            SpiderMovement sm = spider.GetComponent<SpiderMovement>();
            if (sm != null)
            {
                sm.AttachToThread(thread, 0.5f);
            }

            Physics.SyncTransforms();
            foreach (SpiderLeg leg in legs)
            {
                leg.PoseAtRest();
            }

            cam.transform.position = mid + Vector3.forward * 0.7f;
            cam.transform.LookAt(mid);
            cam.targetTexture = target;
            cam.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(folder, "shot_faden.png"), image.EncodeToPNG());
            Debug.Log("[Spinnenspiel] Kletter-Kontrollbild: shot_faden.png");

            if (sm != null)
            {
                sm.LetGo();
            }

            // Fünftes Bild: Radnetz zwischen den Netzstäben, Spinne am Hub (Netz 2a).
            var webGo = new GameObject("NetzShotWeb");
            OrbWeb web = webGo.AddComponent<OrbWeb>();
            Vector3[] anchors =
            {
                new Vector3(5.02f, 1.15f, 2.0f),
                new Vector3(5.78f, 1.15f, 2.0f),
                new Vector3(5.72f, 0.02f, 2.0f),
                new Vector3(5.08f, 0.02f, 2.0f)
            };
            OrbWebLayout netzLayout = new OrbWebLayout(anchors, 16, 0.02f, 0.03f, 7);
            web.Build(netzLayout);
            web.SetProgress(OrbWeb.WebStage.Done, 1f);

            Vector3 hub = netzLayout.Hub;
            spider.transform.position = hub;
            spider.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
            Physics.SyncTransforms();
            foreach (SpiderLeg leg in legs)
            {
                leg.PoseAtRest();
            }

            // Kamera frontal von -Z, 1.3 m Abstand, leicht von der Seite.
            cam.transform.position = hub + new Vector3(0.30f, 0.08f, -1.3f);
            cam.transform.LookAt(hub);
            cam.targetTexture = target;
            cam.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(folder, "shot_netz.png"), image.EncodeToPNG());
            Debug.Log("[Spinnenspiel] Kletter-Kontrollbild: shot_netz.png");

            Object.DestroyImmediate(webGo);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(threadGo);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }
}
