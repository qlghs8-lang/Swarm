using Swarm.Game;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Swarm.EditorTools
{
    public static class DebugEvolveButtonsSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/TestStage.unity";
        private const string CanvasName = "HUD Canvas";

        [MenuItem("Swarm/Debug/Add Evolve Weapon Buttons (TestStage)")]
        private static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var canvasObject = GameObject.Find(CanvasName);
            if (canvasObject == null)
            {
                Debug.LogError($"'{CanvasName}' not found in {ScenePath}.");
                return;
            }

            var controllerObject = GameObject.Find("DebugEvolveWeapons");
            if (controllerObject == null)
            {
                controllerObject = new GameObject("DebugEvolveWeapons");
            }

            if (!controllerObject.TryGetComponent<DebugEvolveWeaponsButton>(out var controller))
            {
                controller = controllerObject.AddComponent<DebugEvolveWeaponsButton>();
            }

            CreateButton(canvasObject.transform, controller, "EvolveWarriorButton", "전사 진화", 0, nameof(DebugEvolveWeaponsButton.EvolveWarriorWeapons));
            CreateButton(canvasObject.transform, controller, "EvolveArcherButton", "궁수 진화", 1, nameof(DebugEvolveWeaponsButton.EvolveArcherWeapons));
            CreateButton(canvasObject.transform, controller, "EvolveMageButton", "마법사 진화", 2, nameof(DebugEvolveWeaponsButton.EvolveMageWeapons));

            EditorUtility.SetDirty(controllerObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Added 3 evolve-weapon debug buttons to TestStage (top-left).");
        }

        private static void CreateButton(Transform canvasTransform, DebugEvolveWeaponsButton controller, string objectName, string label, int index, string methodName)
        {
            var existing = canvasTransform.Find(objectName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasTransform, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(140f, 50f);
            rect.anchoredPosition = new Vector2(20f, -20f - index * 60f);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.25f, 0.85f);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;

            var button = buttonObject.GetComponent<Button>();
            var action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), controller, methodName);
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }
    }
}
