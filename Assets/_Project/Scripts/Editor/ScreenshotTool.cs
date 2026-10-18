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
                // U2: Hindernisse are only 5-30 cm tall -> cam sits ~0.3 m up and looks along the course (z+0.9..4.6).
                new View("parcours", s + new Vector3(-0.30f, 0.30f, -0.50f), s + new Vector3(0.1f, 0.08f, 2.2f)),
                // Fog is Exp2 density 0.022 -> uebersicht cam must stay within ~20 m of its target; 9 m height.
                new View("uebersicht", s + new Vector3(-10f, 9f, -10f), s + new Vector3(0f, 0.2f, 2f)),
                new View("bach", stream + new Vector3(-2.5f, 0.8f, -2.2f), stream),
                new View("wald", new Vector3(30f, 5.5f, 44f), new Vector3(38f, 4.5f, 56f))
            };

            VegetationSystem vegetation = Object.FindFirstObjectByType<VegetationSystem>();
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ci");
            Directory.CreateDirectory(folder);

            // HDR RT: cam.Render() skips HDRP post-processing, so ACES tonemap is applied manually.
            RenderTexture target = new RenderTexture(1600, 900, 0, RenderTextureFormat.ARGBHalf);
            target.antiAliasing = 4; // U2: MSAA fixes jagged edges in the control shots
            Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            Texture2D hdrTex = new Texture2D(1600, 900, TextureFormat.RGBAHalf, false);
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
                hdrTex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                hdrTex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                TonemapToPng(hdrTex, image);
                File.WriteAllBytes(Path.Combine(folder, "shot_" + view.name + ".png"), image.EncodeToPNG());
                Debug.Log("[Spinnenspiel] Kontrollbild: shot_" + view.name + ".png");
            }

            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(hdrTex);
        }

        // Filmic/ACES-Annäherung plus Auto-Exposure: CI-Shots ohne Post-Processing-Pipeline.
        private static void TonemapToPng(Texture2D hdr, Texture2D ldr)
        {
            Color[] px = hdr.GetPixels();
            int n = px.Length;
            float sumLog = 0f;
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                float lum = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                sumLog += Mathf.Log(Mathf.Max(lum, 1e-5f));
            }
            // Geometric mean -> target mid-grey (0.35), clamped so night shots do not explode.
            float exposure = 0.35f / Mathf.Exp(sumLog / n);
            exposure = Mathf.Clamp(exposure, 0.0005f, 50f);
            Color[] outPx = new Color[n];
            for (int i = 0; i < n; i++)
            {
                Color c = px[i];
                float r = Mathf.Max(c.r * exposure, 0f);
                float g = Mathf.Max(c.g * exposure, 0f);
                float b = Mathf.Max(c.b * exposure, 0f);
                outPx[i] = new Color(AcesTone(r), AcesTone(g), AcesTone(b), 1f);
            }
            ldr.SetPixels(outPx);
            ldr.Apply();
        }

        private static float AcesTone(float x)
        {
            x = Mathf.Max(x - 0.003f, 0f);
            return (x * (2.51f * x + 0.03f)) / ((x * (2.43f * x + 0.59f) + 0.14f));
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
            // Trees are GameObjects named "PH-Tree" (JungleWorldGenerator); each has a child
            // "TreeCollider" with a MeshCollider. Ground level = trunk collider bounds min.y.
            Vector3 hub = s;
            float groundY = s.y - 12f; // fallback: spider was ~12 m above terrain at spawn
            GameObject treeGo = GameObject.Find("PH-Tree");
            if (treeGo != null)
            {
                hub = treeGo.transform.position;
                Debug.Log("[Spinnenspiel] Urwald PH-Tree at " + hub.ToString("F1"));
            }
            GameObject colGo = GameObject.Find("TreeCollider");
            if (colGo != null)
            {
                MeshCollider mc = colGo.GetComponent<MeshCollider>();
                if (mc != null)
                {
                    groundY = mc.bounds.center.y - mc.bounds.extents.y;
                    Debug.Log("[Spinnenspiel] Urwald trunk bottom y=" + groundY.ToString("F1"));
                }
            }

            View[] views =
            {
                new View("urwald_ground_east", hub + new Vector3(26f, 0f, 0f) + Vector3.up * (groundY - hub.y + 1.6f), hub + Vector3.up * (groundY - hub.y + 1.2f)),
                new View("urwald_treefoot", hub + new Vector3(2.5f, 0f, -2.5f) + Vector3.up * (groundY - hub.y + 1.4f), hub + Vector3.up * (groundY - hub.y + 0.6f)),
                new View("urwald_diag", hub + new Vector3(20f, 0f, -22f) + Vector3.up * (groundY - hub.y + 8f), hub + Vector3.up * (groundY - hub.y + 2f)),
                new View("urwald_west", hub + new Vector3(-24f, 0f, 6f) + Vector3.up * (groundY - hub.y + 2f), hub + Vector3.up * (groundY - hub.y + 1.5f))
            };

            VegetationSystem vegetation = Object.FindFirstObjectByType<VegetationSystem>();
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ci");
            Directory.CreateDirectory(folder);

            // HDR RT: cam.Render() skips HDRP post-processing, so ACES tonemap is applied manually.
            RenderTexture target = new RenderTexture(1600, 900, 0, RenderTextureFormat.ARGBHalf);
            target.antiAliasing = 4; // U2: MSAA fixes jagged edges in the control shots
            Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            Texture2D hdrTex = new Texture2D(1600, 900, TextureFormat.RGBAHalf, false);
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
                hdrTex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                hdrTex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                TonemapToPng(hdrTex, image);
                File.WriteAllBytes(Path.Combine(folder, "shot_" + view.name + ".png"), image.EncodeToPNG());
                Debug.Log("[Spinnenspiel] Kontrollbild: shot_" + view.name + ".png");
            }

            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(hdrTex);
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

        /// <summary>
        /// T1: Proof image of the Kaefer (Beetle). Builds the Klettertest scene, activates the
        /// Kaefer-Vorlage (beetleTemplate) as a real insect and renders it close-up to
        /// Logs/ci/shot_kaefer.png so its primitive model (oval dark body + wing covers) can be verified.
        /// </summary>
        [MenuItem("Spinnenspiel/Debug/Kaefer-Kontrolldbild rendern")]
        public static void CaptureBeetle()
        {
            const string scenePath = "Assets/_Project/Scenes/Klettertest.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            InsectSpawner spawner = Object.FindFirstObjectByType<InsectSpawner>();
            if (spawner == null || spawner.beetleTemplate == null)
            {
                Debug.LogError("[Spinnenspiel] Screenshot: Spawner oder Kaefer-Vorlage fehlt (Klettertest).");
                return;
            }

            GameObject kaefer = Object.Instantiate(spawner.beetleTemplate, new Vector3(5.4f, 0.7f, 1.0f), Quaternion.identity);
            kaefer.name = "ShotKaefer";
            kaefer.SetActive(true);
            FlyingInsect insect = kaefer.GetComponent<FlyingInsect>();
            if (insect != null)
            {
                insect.home = kaefer.transform.position; // stay put for the shot
            }

            GameObject camGo = new GameObject("BeetleShotCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;

            RenderTexture target = new RenderTexture(1200, 900, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            Texture2D image = new Texture2D(1200, 900, TextureFormat.RGB24, false);

            Vector3 c = kaefer.transform.position;
            cam.transform.position = c + new Vector3(0.06f, 0.05f, -0.12f);
            cam.transform.LookAt(c);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 20f;
            cam.targetTexture = target;
            cam.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ci");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "shot_kaefer.png"), image.EncodeToPNG());
            Debug.Log("[Spinnenspiel] Kaefer-Kontrolldbild: shot_kaefer.png");

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }

        /// <summary>
        /// T2: Proof image of the Libelle (Dragonfly). Builds the Klettertest scene, activates the
        /// Libelle-Vorlage (libelleTemplate) as a real insect and renders it close-up to
        /// Logs/ci/shot_libelle.png so its primitive model (long slim body + head + 4 wings) can be verified.
        /// </summary>
        [MenuItem("Spinnenspiel/Debug/Libelle-Kontrolldbild rendern")]
        public static void CaptureLibelle()
        {
            const string scenePath = "Assets/_Project/Scenes/Klettertest.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            InsectSpawner spawner = Object.FindFirstObjectByType<InsectSpawner>();
            if (spawner == null || spawner.libelleTemplate == null)
            {
                Debug.LogError("[Spinnenspiel] Screenshot: Spawner oder Libelle-Vorlage fehlt (Klettertest).");
                return;
            }

            GameObject libelle = Object.Instantiate(spawner.libelleTemplate, new Vector3(5.4f, 0.7f, 1.0f), Quaternion.identity);
            libelle.name = "ShotLibelle";
            libelle.SetActive(true);
            FlyingInsect insect = libelle.GetComponent<FlyingInsect>();
            if (insect != null)
            {
                insect.home = libelle.transform.position; // stay put for the shot
            }

            GameObject camGo = new GameObject("LibelleShotCamera");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;

            RenderTexture target = new RenderTexture(1200, 900, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            Texture2D image = new Texture2D(1200, 900, TextureFormat.RGB24, false);

            Vector3 c = libelle.transform.position;
            cam.transform.position = c + new Vector3(0.05f, 0.16f, -0.05f); // high 3/4-top view: all 4 wings spread out
            cam.transform.LookAt(c);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 20f;
            cam.targetTexture = target;
            cam.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;

            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "ci");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "shot_libelle.png"), image.EncodeToPNG());
            Debug.Log("[Spinnenspiel] Libelle-Kontrolldbild: shot_libelle.png");

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }
    }
}
