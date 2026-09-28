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
    }
}
