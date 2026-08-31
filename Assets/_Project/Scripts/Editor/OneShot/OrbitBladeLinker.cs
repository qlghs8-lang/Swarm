using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class OrbitBladeLinker
    {
        private const string SwordAsepritePath = "Assets/_Project/Textures/Effects/sword2.aseprite";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Link Warrior Orbit Blade Sprite (Both Scenes)")]
        private static void Link()
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(SwordAsepritePath)
                .OfType<Sprite>()
                .FirstOrDefault();

            if (sprite == null)
            {
                Debug.LogError($"No Sprite sub-asset found in {SwordAsepritePath}.");
                return;
            }

            foreach (var scenePath in ScenePaths)
            {
                LinkInScene(scenePath, sprite);
            }

            Debug.Log($"Linked orbit blade sprite to WarriorOrbitWeapon in both scenes. " +
                      "Import 설정(PPU 50 / Pivot Space Canvas+Center / Sprite Mesh Type Full Rect)은 art-plan.md 2b 기준대로 수동 확인 필요.");
        }

        private static void LinkInScene(string scenePath, Sprite sprite)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError($"Player GameObject not found in {scenePath}. Skipped.");
                return;
            }

            var weapon = player.GetComponent<WarriorOrbitWeapon>();
            if (weapon == null)
            {
                Debug.LogError($"WarriorOrbitWeapon component not found on Player in {scenePath}. Skipped.");
                return;
            }

            var serializedObject = new SerializedObject(weapon);
            serializedObject.FindProperty("bladeSprite").objectReferenceValue = sprite;
            serializedObject.FindProperty("bladeColor").colorValue = Color.white;
            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
