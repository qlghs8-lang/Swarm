using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Swarm.Weapon
{
    // A single ObjectPool is owned per weapon instance (see ArcherArrowWeapon etc.), which works
    // because each weapon component is the sole spawner of its own projectiles. Damage numbers,
    // XP pickups and gold pickups are different: many different EnemyHealth instances (one per
    // enemy) and multiple weapon scripts all spawn the same handful of prefabs, so they need one
    // pool per prefab shared across every caller instead of one pool per spawning component.
    public static class SharedObjectPool
    {
        private static readonly Dictionary<GameObject, ObjectPool> Pools = new();

        // Pooled instances live in the active scene's hierarchy, so Unity destroys any queued
        // (inactive) instances when that scene unloads. Without this, Pools would keep handing out
        // references to those now-destroyed GameObjects after a scene reload, causing
        // MissingReferenceException on the first pooled spawn in the new scene.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Pools.Clear();
        }

        public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!Pools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool(prefab);
                Pools[prefab] = pool;
            }

            return pool.Get(position, rotation);
        }

        public static void Release(GameObject prefab, GameObject instance)
        {
            if (prefab != null && Pools.TryGetValue(prefab, out var pool))
            {
                pool.Release(instance);
            }
            else
            {
                Object.Destroy(instance);
            }
        }
    }
}
