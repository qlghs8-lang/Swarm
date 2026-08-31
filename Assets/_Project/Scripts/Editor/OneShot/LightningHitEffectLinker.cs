using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class LightningHitEffectLinker
    {
        private const string LightningAsepritePath = "Assets/_Project/Textures/Effects/lightning.aseprite";
        private const string StrikeFlashPrefabPath = "Assets/_Project/Prefabs/StrikeFlash.prefab";
        private const float StrikeFlashScale = 3.0f;

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        // Undoes the earlier mistaken link: lightning.aseprite belongs to MageBoltWeapon's
        // StrikeFlash, not MageIceBoltWeapon's (the Chain Lightning evolution) hit effect.
        [MenuItem("Swarm/Art Test/Fix - Clear Wrong Mage Ice Bolt Hit Effect")]
        private static void ClearWrongLink()
        {
            foreach (var scenePath in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                var player = GameObject.Find("Player");
                if (player == null)
                {
                    Debug.LogError($"Player GameObject not found in {scenePath}. Skipped.");
                    continue;
                }

                var weapon = player.GetComponent<MageIceBoltWeapon>();
                if (weapon == null)
                {
                    Debug.LogError($"MageIceBoltWeapon component not found on Player in {scenePath}. Skipped.");
                    continue;
                }

                var serializedObject = new SerializedObject(weapon);
                var framesProperty = serializedObject.FindProperty("hitEffectFrames");
                framesProperty.arraySize = 0;
                serializedObject.ApplyModifiedProperties();

                EditorUtility.SetDirty(weapon);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("Cleared hitEffectFrames on MageIceBoltWeapon in both scenes.");
        }

        [MenuItem("Swarm/Art Test/Link Mage Bolt Strike Flash (StrikeFlash Prefab)")]
        private static void LinkStrikeFlash()
        {
            var frames = AssetDatabase.LoadAllAssetsAtPath(LightningAsepritePath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            if (frames.Length == 0)
            {
                Debug.LogError($"No Sprite sub-assets found in {LightningAsepritePath}.");
                return;
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(StrikeFlashPrefabPath);

            prefabRoot.transform.localScale = Vector3.one * StrikeFlashScale;

            var spriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }

            var strikeFlash = prefabRoot.GetComponent<StrikeFlash>();
            if (strikeFlash == null)
            {
                Debug.LogError("StrikeFlash component not found on StrikeFlash.prefab.");
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                return;
            }

            var serializedObject = new SerializedObject(strikeFlash);
            var framesProperty = serializedObject.FindProperty("frames");
            framesProperty.arraySize = frames.Length;
            for (var i = 0; i < frames.Length; i++)
            {
                framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }

            serializedObject.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, StrikeFlashPrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            Debug.Log($"Linked {frames.Length} lightning frames to StrikeFlash.prefab (used by MageBoltWeapon). " +
                      "Import 설정(PPU 50 / Pivot Space Canvas+Center / Sprite Mesh Type Full Rect)은 art-plan.md 2b 기준대로 수동 확인 필요.");
        }
    }
}
