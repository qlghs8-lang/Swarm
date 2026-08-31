using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class FireballEffectSetup
    {
        private const string BasicProjectilePrefabPath = "Assets/_Project/Prefabs/Projectile_Basic.prefab";
        private const string FireballProjectilePrefabPath = "Assets/_Project/Prefabs/Projectile_Fireball.prefab";
        private const string FireballAsepritePath = "Assets/_Project/Textures/Effects/fireball.aseprite";
        private const string FireballWeaponDataPath = "Assets/_Project/Data/Weapons/MageFireballWeaponData.asset";
        private const int TravelFrameCount = 4;
        private const float ProjectileScale = 2.5f;
        private const float HitEffectScale = 3.5f;

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Setup Mage Fireball Effects")]
        private static void Setup()
        {
            var frames = AssetDatabase.LoadAllAssetsAtPath(FireballAsepritePath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            if (frames.Length == 0)
            {
                Debug.LogError($"No Sprite sub-assets found in {FireballAsepritePath}.");
                return;
            }

            var travelFrames = frames.Take(TravelFrameCount).ToArray();
            var hitFrames = frames.Skip(TravelFrameCount).ToArray();

            if (!CreateFireballProjectilePrefab(travelFrames)) return;
            if (!LinkProjectilePrefabToWeaponData()) return;

            foreach (var scenePath in ScenePaths)
            {
                LinkHitEffectInScene(scenePath, hitFrames);
            }

            Debug.Log($"Fireball setup complete: {travelFrames.Length} travel frames baked into {FireballProjectilePrefabPath}, " +
                      $"{hitFrames.Length} hit-effect frames linked to MageFireballWeapon in both scenes. " +
                      "Import 설정(PPU 50 / Pivot Space Canvas+Center / Sprite Mesh Type Full Rect)은 art-plan.md 2b 기준대로 수동 확인 필요.");
        }

        private static bool CreateFireballProjectilePrefab(Sprite[] travelFrames)
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

            spriteRenderer.sprite = travelFrames[0];
            spriteRenderer.color = Color.white;
            spriteRenderer.transform.localScale = Vector3.one * ProjectileScale;

            var animator = spriteRenderer.gameObject.GetComponent<FireballAnimator>();
            if (animator == null)
            {
                animator = spriteRenderer.gameObject.AddComponent<FireballAnimator>();
            }

            var serializedAnimator = new SerializedObject(animator);
            var framesProperty = serializedAnimator.FindProperty("frames");
            framesProperty.arraySize = travelFrames.Length;
            for (var i = 0; i < travelFrames.Length; i++)
            {
                framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = travelFrames[i];
            }
            serializedAnimator.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(contents, FireballProjectilePrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool LinkProjectilePrefabToWeaponData()
        {
            var weaponData = AssetDatabase.LoadAssetAtPath<ProjectileWeaponData>(FireballWeaponDataPath);
            if (weaponData == null)
            {
                Debug.LogError($"Could not load {FireballWeaponDataPath}.");
                return false;
            }

            var newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FireballProjectilePrefabPath);
            if (newPrefab == null)
            {
                Debug.LogError($"Could not load newly created {FireballProjectilePrefabPath}.");
                return false;
            }

            var serializedObject = new SerializedObject(weaponData);
            serializedObject.FindProperty("projectilePrefab").objectReferenceValue = newPrefab;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(weaponData);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static void LinkHitEffectInScene(string scenePath, Sprite[] hitFrames)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError($"Player GameObject not found in {scenePath}. Skipped.");
                return;
            }

            var weapon = player.GetComponent<MageFireballWeapon>();
            if (weapon == null)
            {
                Debug.LogError($"MageFireballWeapon component not found on Player in {scenePath}. Skipped.");
                return;
            }

            var serializedObject = new SerializedObject(weapon);
            var framesProperty = serializedObject.FindProperty("hitEffectFrames");
            framesProperty.arraySize = hitFrames.Length;
            for (var i = 0; i < hitFrames.Length; i++)
            {
                framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = hitFrames[i];
            }

            serializedObject.FindProperty("hitEffectScale").floatValue = HitEffectScale;
            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
