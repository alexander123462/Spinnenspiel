using System.Collections.Generic;
using UnityEngine;

namespace Spinnenspiel
{
    /// <summary>
    /// Aktiviert kurzzeitig Kapsel-Kollider für Pflanzen in der Nähe der Spinne (Layer 8 "Pflanzen"),
    /// damit Netze und Fäden dagegen greifen können. Pools GameObjects und recycelt sie pro Refresh.
    /// </summary>
    public class VegetationColliderPool : MonoBehaviour
    {
        public Transform focus;
        public float radius = 4f;
        public float minHeight = 0.2f;
        public int maxColliders = 200;
        public float refreshInterval = 0.3f;

        public int ActiveCount { get; private set; }

        private VegetationSystem _system;
        private readonly List<GameObject> _pool = new List<GameObject>();
        private readonly List<Matrix4x4> _matrices = new List<Matrix4x4>();
        private readonly List<Bounds> _meshBounds = new List<Bounds>();
        private float _timer;

        private void Awake()
        {
            if (focus == null)
            {
                SpiderMovement spider = FindFirstObjectByType<SpiderMovement>();
                if (spider != null)
                {
                    focus = spider.transform;
                }
            }

            _system = GetComponent<VegetationSystem>();
            if (_system == null)
            {
                _system = FindFirstObjectByType<VegetationSystem>();
            }

            _timer = refreshInterval;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = refreshInterval;
                Refresh();
            }
        }

        public void ForceRefresh()
        {
            _timer = refreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            if (_system == null || focus == null)
            {
                return;
            }

            _system.QueryInstances(focus.position, radius, minHeight, _matrices, _meshBounds);

            int count = Mathf.Min(_matrices.Count, maxColliders);
            for (int i = 0; i < count; i++)
            {
                GameObject go = GetOrCreate(i);
                go.SetActive(true);

                Bounds b = _meshBounds[i];
                Matrix4x4 m = _matrices[i];

                go.transform.position = m.MultiplyPoint3x4(b.center);
                go.transform.rotation = Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1));
                go.transform.localScale = Vector3.one;

                CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
                capsule.height = b.size.y * m.GetColumn(1).magnitude;
                capsule.radius = Mathf.Clamp(0.25f * Mathf.Min(b.size.x * m.GetColumn(0).magnitude, b.size.z * m.GetColumn(2).magnitude), 0.015f, 0.12f);
            }

            for (int i = count; i < _pool.Count; i++)
            {
                _pool[i].SetActive(false);
            }

            ActiveCount = count;
            Physics.SyncTransforms();
        }

        private GameObject GetOrCreate(int index)
        {
            if (index < _pool.Count)
            {
                return _pool[index];
            }

            GameObject go = new GameObject("PflanzenCollider");
            go.layer = GameLayers.Plants;
            go.transform.SetParent(transform, false);
            go.AddComponent<CapsuleCollider>().direction = 1; // Y
            _pool.Add(go);
            return go;
        }
    }
}
