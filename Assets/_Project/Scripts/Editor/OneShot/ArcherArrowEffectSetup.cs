using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class ArcherArrowEffectSetup
    {
        private const string BasicProjectilePrefabPath = "Assets/_Project/Prefabs/Projectile_Basic.prefab";
        private const string ArrowProjectilePrefabPath = "Assets/_Project/Prefabs/Projectile_Arrow.prefab";
        private const string ArrowFlyingAsepritePath = "Assets/_Project/Textures/Effects/arrow1.aseprite";
        private const string ArrowHitAsepritePath = "Assets/_Project/Textures/Effects/arrow_hit.aseprite";
        private const string ArrowWeaponDataPath = "Assets/_Project/Data/Weapons/ArcherArrowWeaponData.asset";
        private const string VolleyWeaponDataPath = "Assets/_Project/Data/Weapons/ArcherVolleyWeaponData.asset";
        private const string SprayWeaponDataPath = "Assets/_Project/Data/Weapons/ArcherSprayWeaponData.asset";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Setup Archer Arrow Effects")]
        private static void Setup()
        {
            var flyingSprite = AssetDatabase.LoadAllAssetsAtPath(ArrowFlyingAsepritePath).OfType<Sprite>().FirstOrDefault();
            if (flyingSprite == null)
            {
                Debug.LogError($"No Sprite sub-asset found in {ArrowFlyingAsepritePath}.");
                return;
            }

            var hitFrames = AssetDatabase.LoadAllAssetsAtPath(ArrowHitAsepritePath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (hitFrames.Length == 0)
            {
                Debug.LogError($"No Sprite sub-assets found in {ArrowHitAsepritePath}.");
                return;
            }

            if (!CreateArrowProjectilePrefab(flyingSprite)) return;
            if (!LinkProjectilePrefabToWeaponData(ArrowWeaponDataPath)) return;
            if (!LinkProjectilePrefabToWeaponData(VolleyWeaponDataPath)) return;
            if (!LinkProjectilePrefabToWeaponData(SprayWeaponDataPath)) return;

            foreach (var scenePath in ScenePaths)
            {
                LinkHitEffectInScene<ArcherArrowWeapon>(scenePath, hitFrames);
                LinkHitEffectInScene<ArcherVolleyWeapon>(scenePath, hitFrames);
                // Spray (난사) intentionally gets the flying arrow sprite only, no hit-effect wiring -
                // it fires very rapidly in a 360° pattern, and a hit burst on every impact would be
                // too visually noisy at that frequency.
            }

            Debug.Log($"Archer arrow/volley/spray setup complete: {ArrowProjectilePrefabPath} created and linked to all three weapons' data, " +
                      $"{hitFrames.Length} hit-effect frames linked to Arrow/Volley only (Spray excluded on purpose) in both scenes. " +
                      "Import 설정(PPU 50 / Pivot Space Canvas+Center / Sprite Mesh Type Full Rect)은 art-plan.md 2b 기준대로 수동 확인 필요.");
        }

        private static bool CreateArrowProjectilePrefab(Sprite flyingSprite)
        {
            var basicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasicProjectilePrefabPath);
            if (basicPrefab == null)
            {
                Debug.LogError($"Could not load {BasicProjectilePrefabPath}.");
                return false;
            }

            var contents = PrefabUtility.LoadPrefabContents(BasicProjectilePrefabPath);
            var spriteRenderer = contents.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError($"{BasicProjectilePrefabPath} has no SpriteRenderer.");
                PrefabUtility.UnloadPrefabContents(contents);
                return false;
            }

            spriteRenderer.sprite = flyingSprite;
            spriteRenderer.color = Color.white;
            spriteRenderer.transform.localScale = Vector3.one * 0.5f;

            PrefabUtility.SaveAsPrefabAsset(contents, ArrowProjectilePrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool LinkProjectilePrefabToWeaponData(string weaponDataPath)
        {
            var weaponData = AssetDatabase.LoadAssetAtPath<ProjectileWeaponData>(weaponDataPath);
            if (weaponData == null)
            {
                Debug.LogError($"Could not load {weaponDataPath}.");
                return false;
            }

            var newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowProjectilePrefabPath);
            if (newPrefab == null)
            {
                Debug.LogError($"Could not load newly created {ArrowProjectilePrefabPath}.");
                return false;
            }

            var serializedObject = new SerializedObject(weaponData);
            serializedObject.FindProperty("projectilePrefab").objectReferenceValue = newPrefab;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(weaponData);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static void LinkHitEffectInScene<T>(string scenePath, Sprite[] hitFrames) where T : Component
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError($"Player GameObject not found in {scenePath}. Skipped.");
                return;
            }

            if (!player.TryGetComponent<T>(out var weapon))
            {
                Debug.LogError($"{typeof(T).Name} component not found on Player in {scenePath}. Skipped.");
                return;
            }

            var serializedObject = new SerializedObject(weapon);
            var framesProperty = serializedObject.FindProperty("hitEffectFrames");
            framesProperty.arraySize = hitFrames.Length;
            for (var i = 0; i < hitFrames.Length; i++)
            {
                framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = hitFrames[i];
            }

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
