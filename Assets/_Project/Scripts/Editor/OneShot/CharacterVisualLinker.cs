using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class CharacterVisualLinker
    {
        [MenuItem("Swarm/Art Test/Link Character Visuals (Warrior + Archer + Mage)")]
        private static void Link()
        {
            LinkOne(
                "Assets/_Project/Data/Characters/Warrior.asset",
                "Assets/_Project/Textures/Player/Warrior.aseprite",
                "Assets/_Project/Animation/Warrior.controller");

            LinkOne(
                "Assets/_Project/Data/Characters/Archer.asset",
                "Assets/_Project/Textures/Player/Archer.aseprite",
                "Assets/_Project/Animation/Archer.controller");

            LinkOne(
                "Assets/_Project/Data/Characters/Mage.asset",
                "Assets/_Project/Textures/Player/Mage.aseprite",
                "Assets/_Project/Animation/Mage.controller");

            AssetDatabase.SaveAssets();
            Debug.Log("Character visuals linked. GameManager.Start() will now apply the correct sprite/Animator per selected character at runtime.");
        }

        private static void LinkOne(string characterAssetPath, string asepritePath, string controllerPath)
        {
            var characterAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(characterAssetPath);
            if (characterAsset == null)
            {
                Debug.LogError($"Character asset not found at {characterAssetPath}. Skipped.");
                return;
            }

            var frame0 = AssetDatabase.LoadAllAssetsAtPath(asepritePath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == "Frame_0");
            if (frame0 == null)
            {
                Debug.LogError($"Frame_0 sprite not found in {asepritePath}. Skipped.");
                return;
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                Debug.LogError($"AnimatorController not found at {controllerPath}. Skipped.");
                return;
            }

            var serializedObject = new SerializedObject(characterAsset);
            serializedObject.FindProperty("defaultSprite").objectReferenceValue = frame0;
            serializedObject.FindProperty("animatorController").objectReferenceValue = controller;
            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(characterAsset);
            Debug.Log($"Linked {characterAssetPath} -> sprite={frame0.name}, controller={controller.name}");
        }
    }
}
