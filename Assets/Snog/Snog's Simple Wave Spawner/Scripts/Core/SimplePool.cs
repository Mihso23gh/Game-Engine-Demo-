
using System.Collections.Generic;
using UnityEngine;

namespace Snog.SimpleWaveSystem.Core
{
    public class SimplePool : MonoBehaviour
    {
        [System.Serializable]
        public class WarmupEntry
        {
            public GameObject prefab;

            [Min(0)]
            public int count;
        }

        [SerializeField] private bool autoExpand = true;
        [SerializeField] private List<WarmupEntry> warmup = new();

        private readonly Dictionary<GameObject, Queue<GameObject>> pools = new();

        private void Awake()
        {
            WarmupAll();
        }

        public void SetAutoExpand(bool value)
        {
            autoExpand = value;
        }

        public void WarmupAll()
        {
            for (int i = 0; i < warmup.Count; i++)
            {
                WarmupEntry entry = warmup[i];

                if (entry != null && entry.prefab != null && entry.count > 0)
                {
                    Warmup(entry.prefab, entry.count);
                }
            }
        }

        public void Warmup(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            Queue<GameObject> q = GetOrCreateQueue(prefab);

            for (int i = 0; i < count; i++)
            {
                GameObject obj = CreateInstance(prefab);
                Return(prefab, obj);
            }
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            Queue<GameObject> q = GetOrCreateQueue(prefab);

            GameObject obj = null;

            while (q.Count > 0 && obj == null)
            {
                obj = q.Dequeue();
            }

            if (obj == null)
            {
                if (!autoExpand)
                {
                    return null;
                }

                obj = CreateInstance(prefab);
            }

            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);

            return obj;
        }

        public void Return(GameObject prefab, GameObject obj)
        {
            if (prefab == null || obj == null)
            {
                return;
            }

            obj.SetActive(false);
            obj.transform.SetParent(transform, true);

            Queue<GameObject> q = GetOrCreateQueue(prefab);
            q.Enqueue(obj);
        }

        private Queue<GameObject> GetOrCreateQueue(GameObject prefab)
        {
            if (!pools.TryGetValue(prefab, out Queue<GameObject> q))
            {
                q = new Queue<GameObject>();
                pools.Add(prefab, q);
            }

            return q;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            obj.transform.SetParent(transform, true);
            return obj;
        }
    }
}