using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class ArcherFocusEffectSetup
    {
        private const string BasicProjectilePrefabPath = "Assets/_Project/Prefabs/Projectile_Basic.prefab";
        private const string FocusProjectilePrefabPath = "Assets/_Project/Prefabs/Projectile_FocusArrow.prefab";
        private const string FocusSpriteAsepritePath = "Assets/_Project/Textures/Effects/arrow_cct.aseprite";
        private const string ArrowHitAsepritePath = "Assets/_Project/Textures/Effects/arrow_hit.aseprite";
        private const string FocusWeaponDataPath = "Assets/_Project/Data/Weapons/ArcherFocusWeaponData.asset";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Setup Archer Focus Effect")]
        private static void Setup()
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(FocusSpriteAsepritePath).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                Debug.LogError($"No Sprite sub-asset found in {FocusSpriteAsepritePath}.");
                return;
            }

            var hitFrames = AssetDatabase.LoadAllAssetsAtPath(ArrowHitAsepritePath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (hitFrames.Length == 0)
            {
                Debug.LogError($"No Sprite sub-assets found in {ArrowHitAsepritePath}.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(BasicProjectilePrefabPath);
            var spriteRenderer = contents.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError($"{BasicProjectilePrefabPath} has no SpriteRenderer.");
                PrefabUtility.UnloadPrefabContents(contents);
                return;
            }

            spriteRenderer.sprite = sprite;
            spriteRenderer.color = Color.white;
            spriteRenderer.transform.localScale = Vector3.one * 0.5f;

            PrefabUtility.SaveAsPrefabAsset(contents, FocusProjectilePrefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            AssetDatabase.SaveAssets();

            var focusData = AssetDatabase.LoadAssetAtPath<ProjectileWeaponData>(FocusWeaponDataPath);
            if (focusData == null)
            {
                Debug.LogError($"Could not load {FocusWeaponDataPath}.");
                return;
            }

            var newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FocusProjectilePrefabPath);
            var prefabSerializedObject = new SerializedObject(focusData);
            prefabSerializedObject.FindProperty("projectilePrefab").objectReferenceValue = newPrefab;
            prefabSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(focusData);
            AssetDatabase.SaveAssets();

            foreach (var scenePath in ScenePaths)
            {
                LinkHitEffectInScene(scenePath, hitFrames);
            }

            Debug.Log($"Archer Focus setup complete: {FocusProjectilePrefabPath} updated and linked to {FocusWeaponDataPath}, " +
                      $"{hitFrames.Length} hit-effect frames (reused from Arrow/Volley) linked in both scenes. " +
                      "Import 설정(PPU 50 / Pivot Space Canvas+Center / Sprite Mesh Type Full Rect)은 art-plan.md 2b 기준대로 수동 확인 필요.");
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

            if (!player.TryGetComponent<ArcherFocusWeapon>(out var weapon))
            {
                Debug.LogError($"ArcherFocusWeapon component not found on Player in {scenePath}. Skipped.");
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
