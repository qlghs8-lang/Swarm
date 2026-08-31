using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class MageArcherAdditiveMaterialLinker
    {
        private const string MaterialPath = "Assets/_Project/Materials/SpriteAdditive.mat";
        private const string StrikeFlashPrefabPath = "Assets/_Project/Prefabs/StrikeFlash.prefab";
        private const string FireballProjectilePrefabPath = "Assets/_Project/Prefabs/Projectile_Fireball.prefab";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Link Additive Material to Mage Bolt + Fireball + Archer Hit Effects (Both Scenes)")]
        private static void Link()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Debug.LogError($"No material found at {MaterialPath}. Run 'Swarm/Art Test/Create Additive Sprite Material' first.");
                return;
            }

            SetPrefabMaterial(StrikeFlashPrefabPath, material);
            SetPrefabMaterial(FireballProjectilePrefabPath, material);

            foreach (var scenePath in ScenePaths)
            {
                LinkInScene(scenePath, material);
            }

            Debug.Log("Linked SpriteAdditive material to StrikeFlash.prefab, Projectile_Fireball.prefab, MageFireballWeapon (hit) " +
                      "and ArcherArrowWeapon / ArcherVolleyWeapon / ArcherFocusWeapon (hit effects) in both scenes. " +
                      "ArcherSprayWeapon(hit 이펙트 없음)와 ArcherToxicRollWeapon(지속형 장판이라 알파블렌드 유지)은 의도적으로 제외.");
        }

        private static void SetPrefabMaterial(string prefabPath, Material material)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            var spriteRenderer = contents.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError($"{prefabPath} has no SpriteRenderer.");
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            spriteRenderer.sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
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
            dirty |= SetMaterial<MageFireballWeapon>(player, material, scenePath);
            dirty |= SetMaterial<ArcherArrowWeapon>(player, material, scenePath);
            dirty |= SetMaterial<ArcherVolleyWeapon>(player, material, scenePath);
            dirty |= SetMaterial<ArcherFocusWeapon>(player, material, scenePath);

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
