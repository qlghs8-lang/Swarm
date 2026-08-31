using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class LifestealEffectLinker
    {
        private const string LifestealEffectAsepritePath = "Assets/_Project/Textures/Effects/lifesteel.aseprite";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Link Warrior Lifesteal Smash Effect (Both Scenes)")]
        private static void Link()
        {
            var frames = AssetDatabase.LoadAllAssetsAtPath(LifestealEffectAsepritePath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            if (frames.Length == 0)
            {
                Debug.LogError($"No Sprite sub-assets found in {LifestealEffectAsepritePath}.");
                return;
            }

            foreach (var scenePath in ScenePaths)
            {
                LinkInScene(scenePath, frames);
            }

            Debug.Log($"Linked {frames.Length} lifesteal smash effect frames to WarriorLifestealWeapon and WarriorRageWeapon in both scenes. " +
                      "Import 설정(PPU 50 / Pivot Space Canvas+Center / Sprite Mesh Type Full Rect)은 art-plan.md 2b 기준대로 수동 확인 필요.");
        }

        private static void LinkInScene(string scenePath, Sprite[] frames)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError($"Player GameObject not found in {scenePath}. Skipped.");
                return;
            }

            var dirty = false;

            if (player.TryGetComponent<WarriorLifestealWeapon>(out var lifesteal))
            {
                SetFrames(lifesteal, frames);
                dirty = true;
            }
            else
            {
                Debug.LogError($"WarriorLifestealWeapon component not found on Player in {scenePath}.");
            }

            if (player.TryGetComponent<WarriorRageWeapon>(out var rage))
            {
                SetFrames(rage, frames);
                dirty = true;
            }
            else
            {
                Debug.LogError($"WarriorRageWeapon component not found on Player in {scenePath}.");
            }

            if (!dirty) return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetFrames(Object weapon, Sprite[] frames)
        {
            var serializedObject = new SerializedObject(weapon);
            var framesProperty = serializedObject.FindProperty("smashEffectFrames");
            framesProperty.arraySize = frames.Length;
            for (var i = 0; i < frames.Length; i++)
            {
                framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(weapon);
        }
    }
}
