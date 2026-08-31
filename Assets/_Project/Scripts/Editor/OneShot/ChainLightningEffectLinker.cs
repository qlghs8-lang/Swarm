using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class ChainLightningEffectLinker
    {
        private const string ChainLightningAsepritePath = "Assets/_Project/Textures/Effects/chainlightning.aseprite";
        private const int TravelFrameCount = 3;
        private const float VisualScale = 2.5f;
        private const float HitEffectScale = 3.5f;

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Link Mage Chain Lightning (Both Scenes)")]
        private static void Link()
        {
            var frames = AssetDatabase.LoadAllAssetsAtPath(ChainLightningAsepritePath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            if (frames.Length == 0)
            {
                Debug.LogError($"No Sprite sub-assets found in {ChainLightningAsepritePath}.");
                return;
            }

            var travelFrames = frames.Take(TravelFrameCount).ToArray();
            var hitFrames = frames.Skip(TravelFrameCount).ToArray();

            foreach (var scenePath in ScenePaths)
            {
                LinkInScene(scenePath, travelFrames, hitFrames);
            }

            Debug.Log($"Linked {travelFrames.Length} travel frames + {hitFrames.Length} hit frames to MageIceBoltWeapon (Chain Lightning) in both scenes. " +
                      "Import 설정(PPU 50 / Pivot Space Canvas+Center / Sprite Mesh Type Full Rect)은 art-plan.md 2b 기준대로 수동 확인 필요.");
        }

        private static void LinkInScene(string scenePath, Sprite[] travelFrames, Sprite[] hitFrames)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError($"Player GameObject not found in {scenePath}. Skipped.");
                return;
            }

            var weapon = player.GetComponent<MageIceBoltWeapon>();
            if (weapon == null)
            {
                Debug.LogError($"MageIceBoltWeapon component not found on Player in {scenePath}. Skipped.");
                return;
            }

            var serializedObject = new SerializedObject(weapon);

            AssignFrames(serializedObject.FindProperty("travelFrames"), travelFrames);
            AssignFrames(serializedObject.FindProperty("hitEffectFrames"), hitFrames);
            serializedObject.FindProperty("visualScale").floatValue = VisualScale;
            serializedObject.FindProperty("hitEffectScale").floatValue = HitEffectScale;

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AssignFrames(SerializedProperty property, Sprite[] frames)
        {
            property.arraySize = frames.Length;
            for (var i = 0; i < frames.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
        }
    }
}
