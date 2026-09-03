using UnityEngine;
using UnityEngine.SceneManagement;

namespace Swarm.Arena
{
    /// <summary>
    /// Places the breakable pots at scene load and keeps the arena stocked with them, the same way
    /// <see cref="ArenaBuilder"/> lays the ground and the props — in code, from a fixed seed, off a
    /// prefab in Resources, so neither Game.unity nor TestStage.unity has to carry a hundred
    /// objects nobody edits by hand and both scenes get the identical arena.
    /// </summary>
    public static class PotScatterer
    {
        private const string RootName = "Pots (Runtime)";
        private const string PrefabPath = "Props/Prop_Pot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryBuild();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => TryBuild();

        private static void TryBuild()
        {
            // The title screen has no arena to scatter into.
            if (GameObject.FindGameObjectWithTag("Player") == null) return;
            if (GameObject.Find(RootName) != null) return;

            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null) return;

            var root = new GameObject(RootName);
            root.AddComponent<PotField>().Build(prefab);
        }
    }
}
