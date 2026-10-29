using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// K4b: Netze tropfen bei Regen. Kleine Klasse, KEEINE Umbau von OrbWeb:
    /// ein GameObject mit dieser组件 liest den OrbWeb (Layout + LineRenderer) und
    /// spawnt single Tropfen (small Meshes, kee Collider) at the thread positions of the web as long as it rains.
    /// Deterministic: no RNG, spawn order follows a fixed integer sequence.
    /// </summary>
    public class WebRainDrips : MonoBehaviour
    {
        [SerializeField, Tooltip("Maximal Tropfen pro Netz (K4b: hoechstens 12).")]
        private int maxTropfen = 12;

        [SerializeField, Tooltip("Interval between two Tropfen in s while raining.")]
        private float spawnIntervalSeconds = 0.5f;

        [SerializeField, Tooltip("Fall speed of a Tropfen in m/s.")]
        private float fallSpeed = 0.25f;

        [SerializeField, Tooltip("Lifetime of a Tropfen in s (then removed).")]
        private float lifeSeconds = 6f;

        [SerializeField, Tooltip("Rain01 threshold that counts as raining.")]
        private float rainThreshold = 0.35f;

        [SerializeField, Tooltip("Diameter of one Tropfen in m.")]
        public float dropScale = 0.012f;

        [SerializeField, Tooltip("Test-Hook: &gt;=0 forces the rain value instead of WeatherController (deterministic tests).")]
        public float forcedRain01 = -1f;

        public OrbWeb web;

        private readonly List<GameObject> tropfen = new List<GameObject>();
        private readonly List<float> birthTime = new List<float>();
        private float spawnTimer = 0f;
        private int nextSlot = 0;

        private static Material sharedMaterial;

        /// <summary>Anzahl aktiver Tropfen — public for tests and debug overlay.</summary>
        public int TropfenCount => tropfen.Count;

        private void Update()
        {
            AdvanceTropfen();

            if (web == null || !web.IsBuilt)
            {
                return;
            }

            float rain = forcedRain01 >= 0f ? forcedRain01
                : (WeatherController.Instance != null ? WeatherController.Instance.Rain01 : 0f);

            if (rain < rainThreshold)
            {
                spawnTimer = 0f;
                return;
            }

            if (tropfen.Count >= maxTropfen)
            {
                return;
            }

            spawnTimer += Time.deltaTime;
            if (spawnTimer < spawnIntervalSeconds)
            {
                return;
            }

            spawnTimer = 0f;
            SpawnOneDrop();
        }

        private void SpawnOneDrop()
        {
            List<Vector3> pts = web.Layout.CaptureSpiral;
            if (pts.Count == 0)
            {
                pts = web.Layout.Frame;
                if (pts.Count == 0)
                {
                    return;
                }
            }

            const float goldenStep = 0.618034f;
            nextSlot %= 1009;
            int idx = Mathf.FloorToInt((nextSlot * goldenStep) % 1f * pts.Count);
            nextSlot++;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Tropfen";
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            go.transform.SetParent(transform, false);
            go.transform.position = pts[Mathf.Clamp(idx, 0, pts.Count - 1)];
            go.transform.localScale = Vector3.one * dropScale;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = DropMaterial();
            }

            tropfen.Add(go);
            birthTime.Add(Time.time);
        }

        /// <summary>Ein shared Tropfen-Material: glaaendernklartropfen-look (transparent Lit).</summary>
        private static Material DropMaterial()
        {
            if (sharedMaterial == null)
            {
                var m = Rendering.PipelineMaterials.Lit(
                    new Color(0.7f, 0.8f, 0.9f, 0.65f));
                Rendering.PipelineMaterials.SetTransparent(m, true);
                Rendering.PipelineMaterials.SetSmoothness(m, 0.9f);
                sharedMaterial = m;
            }

            return sharedMaterial;
        }

        /// <summary>Move Tropfen down and remove expired ones. Runs every frame, also in dry weather.</summary>
        private void AdvanceTropfen()
        {
            for (int i = tropfen.Count - 1; i >= 0; i--)
            {
                GameObject go = tropfen[i];
                if (Time.time - birthTime[i] > lifeSeconds)
                {
                    tropfen.RemoveAt(i);
                    birthTime.RemoveAt(i);
                    Destroy(go);
                    continue;
                }

                go.transform.position += Vector3.down * fallSpeed * Time.deltaTime;
            }
        }
    }
}

