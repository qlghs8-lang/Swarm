using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class LifestealPassiveSetup
    {
        private const string LifestealDisplayName = "흡혈";
        private const string LifestealDescription = "적에게 피해를 줄 때마다 10% 확률로 추가 피해 + 체력 회복";
        private const string RageDisplayName = "레이지";
        private const string RageDescription = "흡혈 발동 시 10% 확률로 공격력/최대 체력 영구 증가";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Rename Lifesteal Passive (Both Scenes)")]
        private static void Setup()
        {
            foreach (var scenePath in ScenePaths)
            {
                SetupInScene(scenePath);
            }

            Debug.Log("Renamed WarriorLifestealWeapon → 흡혈, WarriorRageWeapon → 레이지 in both scenes.");
        }

        private static void SetupInScene(string scenePath)
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
                SetDisplayText(lifesteal, LifestealDisplayName, LifestealDescription);
                dirty = true;
            }

            if (player.TryGetComponent<WarriorRageWeapon>(out var rage))
            {
                SetDisplayText(rage, RageDisplayName, RageDescription);
                dirty = true;
            }

            if (!dirty) return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void SetDisplayText(Object component, string displayName, string description)
        {
            var serializedObject = new SerializedObject(component);
            serializedObject.FindProperty("displayName").stringValue = displayName;
            serializedObject.FindProperty("description").stringValue = description;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
        }
    }
}
