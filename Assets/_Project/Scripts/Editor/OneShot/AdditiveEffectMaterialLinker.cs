using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class AdditiveEffectMaterialLinker
    {
        private const string MaterialPath = "Assets/_Project/Materials/SpriteAdditive.mat";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Link Additive Material to Warrior Effects (Both Scenes)")]
        private static void Link()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Debug.LogError($"No material found at {MaterialPath}. Run 'Swarm/Art Test/Create Additive Sprite Material' first.");
                return;
            }

            foreach (var scenePath in ScenePaths)
            {
                LinkInScene(scenePath, material);
            }

            Debug.Log("Linked SpriteAdditive material to WarriorForwardWeapon / WarriorComboAttackWeapon / WarriorLifestealWeapon / WarriorRageWeapon in both scenes.");
        }

        private static void LinkInScene(string scenePath, Material material)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError($"Player GameObject not found in {scenePath}. Skipped.");
                return;
            }

            var dirty = false;
            dirty |= SetMaterial<WarriorForwardWeapon>(player, material, scenePath);
            dirty |= SetMaterial<WarriorComboAttackWeapon>(player, material, scenePath);
            dirty |= SetMaterial<WarriorLifestealWeapon>(player, material, scenePath);
            dirty |= SetMaterial<WarriorRageWeapon>(player, material, scenePath);

            if (!dirty) return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static bool SetMaterial<T>(GameObject player, Material material, string scenePath) where T : Component
        {
            if (!player.TryGetComponent<T>(out var weapon))
            {
                Debug.LogError($"{typeof(T).Name} component not found on Player in {scenePath}.");
                return false;
            }

            var serializedObject = new SerializedObject(weapon);
            serializedObject.FindProperty("effectMaterial").objectReferenceValue = material;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(weapon);
            return true;
        }
    }
}
