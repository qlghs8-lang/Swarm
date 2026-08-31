using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class BlessingEffectLinker
    {
        private const string BlessingAsepritePath = "Assets/_Project/Textures/Effects/blessing.aseprite";
        private const float BlessingEffectScale = 2f;
        private const float BlessingEffectFrameDuration = 0.16f;

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Link Mage Blessing Effect (Both Scenes)")]
        private static void Link()
        {
            var frames = AssetDatabase.LoadAllAssetsAtPath(BlessingAsepritePath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            if (frames.Length == 0)
            {
                Debug.LogError($"No Sprite sub-assets found in {BlessingAsepritePath}.");
                return;
            }

            foreach (var scenePath in ScenePaths)
            {
                LinkInScene(scenePath, frames);
            }

            Debug.Log($"Linked {frames.Length} blessing effect frames to MageBlessingWeapon in both scenes. " +
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

            var weapon = player.GetComponent<MageBlessingWeapon>();
            if (weapon == null)
            {
                Debug.LogError($"MageBlessingWeapon component not found on Player in {scenePath}. Skipped.");
                return;
            }

            var serializedObject = new SerializedObject(weapon);
            var framesProperty = serializedObject.FindProperty("blessingEffectFrames");
            framesProperty.arraySize = frames.Length;
            for (var i = 0; i < frames.Length; i++)
            {
                framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }

            serializedObject.FindProperty("blessingEffectScale").floatValue = BlessingEffectScale;
            serializedObject.FindProperty("blessingEffectFrameDuration").floatValue = BlessingEffectFrameDuration;
            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
