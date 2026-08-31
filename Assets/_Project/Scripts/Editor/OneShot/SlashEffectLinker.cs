using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class SlashEffectLinker
    {
        private const string SlashEffectAsepritePath = "Assets/_Project/Textures/Effects/SlashEffect.aseprite";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Link Warrior Slash Effect (Both Scenes)")]
        private static void Link()
        {
            var frames = AssetDatabase.LoadAllAssetsAtPath(SlashEffectAsepritePath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            if (frames.Length == 0)
            {
                Debug.LogError($"No Sprite sub-assets found in {SlashEffectAsepritePath}.");
                return;
            }

            foreach (var scenePath in ScenePaths)
            {
                LinkInScene(scenePath, frames);
            }

            Debug.Log($"Linked {frames.Length} slash effect frames to WarriorForwardWeapon in both scenes.");
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

            var weapon = player.GetComponent<WarriorForwardWeapon>();
            if (weapon == null)
            {
                Debug.LogError($"WarriorForwardWeapon component not found on Player in {scenePath}. Skipped.");
                return;
            }

            var serializedObject = new SerializedObject(weapon);
            var framesProperty = serializedObject.FindProperty("slashEffectFrames");
            framesProperty.arraySize = frames.Length;
            for (var i = 0; i < frames.Length; i++)
            {
                framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
