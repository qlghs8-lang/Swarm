using System.Collections.Generic;
using UnityEngine;

namespace Swarm.Weapon
{
    public class ObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Queue<GameObject> _pool = new();

        public ObjectPool(GameObject prefab)
        {
            _prefab = prefab;
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            var instance = _pool.Count > 0 ? _pool.Dequeue() : Object.Instantiate(_prefab);

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            Physics2D.SyncTransforms();
            return instance;
        }

        public void Release(GameObject instance)
        {
            instance.SetActive(false);
            _pool.Enqueue(instance);
        }
    }
}
