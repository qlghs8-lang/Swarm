using System;
using System.Collections.Generic;
using UnityEngine;

namespace Swarm.Weapon
{
    /// <summary>
    /// A pool for effect objects that are BUILT IN CODE rather than instantiated from a prefab.
    ///
    /// <see cref="ObjectPool"/> and <see cref="SharedObjectPool"/> both need a prefab to clone.
    /// The chain-lightning bolt, the meteor, its fire patch and the toxic roll's gas cloud have
    /// none: each is a bare GameObject with a SpriteRenderer and one component added at spawn
    /// time. They were therefore the only combat objects left going through
    /// new GameObject + AddComponent + Destroy on every shot — the most expensive spawn path in
    /// the game, on the weapons that fire most often.
    ///
    /// The factory builds the bare object once; callers re-apply the per-spawn state (position,
    /// scale, sprite, colour) on every Get, so a reused instance is indistinguishable from a
    /// fresh one.
    /// </summary>
    public sealed class RuntimeObjectPool
    {
        private readonly Func<GameObject> _factory;
        private readonly Stack<GameObject> _idle = new();

        // Guards against double-release the same way ObjectPool does: an effect that retires
        // twice in one frame would otherwise be handed to two callers at once.
        private readonly HashSet<GameObject> _idleInstances = new();

        public RuntimeObjectPool(Func<GameObject> factory)
        {
            _factory = factory;
        }

        public GameObject Get()
        {
            while (_idle.Count > 0)
            {
                var instance = _idle.Pop();
                _idleInstances.Remove(instance);

                // A scene unload destroys the queued instances while this pool keeps its
                // references, so a null here means "recycled by Unity", not an error.
                if (instance == null) continue;

                instance.SetActive(true);
                return instance;
            }

            return _factory();
        }

        public void Release(GameObject instance)
        {
            if (instance == null) return;
            if (!_idleInstances.Add(instance)) return;

            instance.SetActive(false);
            _idle.Push(instance);
        }
    }
}
