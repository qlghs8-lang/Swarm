using System.Collections.Generic;
using UnityEngine;

namespace Swarm.Weapon
{
    public class ObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Queue<GameObject> _pool = new();
        private readonly HashSet<GameObject> _queuedInstances = new();

        public ObjectPool(GameObject prefab)
        {
            _prefab = prefab;
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject instance;
            if (_pool.Count > 0)
            {
                instance = _pool.Dequeue();
                _queuedInstances.Remove(instance);
                instance.transform.SetPositionAndRotation(position, rotation);
                instance.SetActive(true);
            }
            else
            {
                instance = Object.Instantiate(_prefab, position, rotation);
            }

            // Physics2D.autoSyncTransforms is off, so a collider that was just moved and
            // re-enabled stays registered at its PREVIOUS position until the next physics step.
            // Weapons run their targeting queries in Update, so without a correction here a
            // freshly spawned enemy can be found — and hit, and have effects spawned on it — at
            // the pooled object's old position, which for a first-time spawn is the prefab's
            // origin, right on top of the player.
            //
            // Writing Rigidbody2D.position moves this one body in the physics world immediately.
            // The previous code called Physics2D.SyncTransforms() instead, which is correct but
            // pushes EVERY physics transform in the scene, once per spawned object — 20+ times in
            // a single frame during a wave burst.
            if (instance.TryGetComponent<Rigidbody2D>(out var body))
            {
                body.position = position;
                body.rotation = rotation.eulerAngles.z;
            }
            else if (instance.TryGetComponent<Collider2D>(out _))
            {
                // A collider with no body of its own can only be repositioned by a full sync.
                Physics2D.SyncTransforms();
            }

            return instance;
        }

        public void Release(GameObject instance)
        {
            // Guards against double-release (e.g. OnTriggerEnter2D and OnTriggerStay2D both firing
            // in the same physics step): without this, the same instance could be enqueued twice
            // and later handed out to two different callers at once, leaving one side holding a
            // stale reference once the object is reused/destroyed elsewhere.
            if (!_queuedInstances.Add(instance)) return;

            instance.SetActive(false);
            _pool.Enqueue(instance);
        }
    }
}
