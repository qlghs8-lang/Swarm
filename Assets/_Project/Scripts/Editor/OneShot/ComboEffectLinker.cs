using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class ComboEffectLinker
    {
        private static readonly string[] StepAsepritePaths =
        {
            "Assets/_Project/Textures/Effects/comboattack_slash.aseprite",
            "Assets/_Project/Textures/Effects/comboattack_thrust.aseprite",
            "Assets/_Project/Textures/Effects/comboattack_slam.aseprite",
        };

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Link Warrior Combo Effects (Both Scenes)")]
        private static void Link()
        {
            var stepFrames = new Sprite[StepAsepritePaths.Length][];
            for (var i = 0; i < StepAsepritePaths.Length; i++)
            {
                var frames = AssetDatabase.LoadAllAssetsAtPath(StepAsepritePaths[i])
                    .OfType<Sprite>()
                    .OrderBy(s => s.name)
                    .ToArray();

                if (frames.Length == 0)
                {
                    Debug.LogError($"No Sprite sub-assets found in {StepAsepritePaths[i]}.");
                    return;
                }

                stepFrames[i] = frames;
            }

            foreach (var scenePath in ScenePaths)
            {
                LinkInScene(scenePath, stepFrames);
            }

            Debug.Log("Linked combo step effects (Slash/Thrust/Slam) to WarriorComboAttackWeapon in both scenes. " +
                      "Import 설정(PPU 50 / Pivot Space Canvas+Center / Sprite Mesh Type Full Rect)은 art-plan.md 2b 기준대로 수동 확인 필요.");
        }

        private static void LinkInScene(string scenePath, Sprite[][] stepFrames)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError($"Player GameObject not found in {scenePath}. Skipped.");
                return;
            }

            var weapon = player.GetComponent<WarriorComboAttackWeapon>();
            if (weapon == null)
            {
                Debug.LogError($"WarriorComboAttackWeapon component not found on Player in {scenePath}. Skipped.");
                return;
            }

            var serializedObject = new SerializedObject(weapon);
            var stepEffectsProperty = serializedObject.FindProperty("stepEffects");
            stepEffectsProperty.arraySize = stepFrames.Length;

            for (var i = 0; i < stepFrames.Length; i++)
            {
                var elementProperty = stepEffectsProperty.GetArrayElementAtIndex(i);
                var framesProperty = elementProperty.FindPropertyRelative("frames");
                framesProperty.arraySize = stepFrames[i].Length;
                for (var j = 0; j < stepFrames[i].Length; j++)
                {
                    framesProperty.GetArrayElementAtIndex(j).objectReferenceValue = stepFrames[i][j];
                }
            }

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
