#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Swarm.Game
{
    /// <summary>
    /// TEST ONLY — adds a "+100 G" button to the title screen so gold can be topped up after a
    /// progress reset, since gold otherwise only comes from enemy drops.
    ///
    /// The button is built at runtime and nothing in Title.unity references this class, so
    /// deleting this one file removes the button completely — no scene cleanup, no missing script
    /// references. It is also compiled out of release builds entirely.
    /// </summary>
    public static class DebugGoldButton
    {
        private const int GoldPerClick = 100;
        private const string ObjectName = "DebugGoldButton (Runtime)";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            TryInstall();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall();

        private static void TryInstall()
        {
            // 기본적으로 꺼져 있다. Swarm ▸ 개발용 UI 표시를 켜야 버튼이 생긴다.
            if (!DevUi.IsEnabled) return;

            // TitleManager only exists on the title screen, which is where gold is actually spent.
            if (Object.FindAnyObjectByType<TitleManager>() == null) return;

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            var root = canvas.rootCanvas.transform;
            if (root.Find(ObjectName) != null) return;

            // Borrow the font from a Text already in the scene rather than guessing at the name of
            // a builtin resource, which changes between Unity versions.
            var existingText = Object.FindAnyObjectByType<Text>();
            var font = existingText != null ? existingText.font : null;
            if (font == null) font = Swarm.UI.UiFont.Current;

            var button = CreateButton(root, font);
            button.onClick.AddListener(() => GoldWallet.Add(GoldPerClick));
        }

        private static Button CreateButton(Transform parent, Font font)
        {
            var go = new GameObject(ObjectName, typeof(RectTransform), typeof(CanvasRenderer),
                                    typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24f, 24f);
            rect.sizeDelta = new Vector2(150f, 46f);

            go.GetComponent<Image>().color = new Color(0.10f, 0.43f, 0.36f, 0.92f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
                                             typeof(Text));
            labelObject.transform.SetParent(go.transform, false);

            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<Text>();
            label.text = $"+{GoldPerClick} G (TEST)";
            label.font = font;
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;

            return go.GetComponent<Button>();
        }
    }
}
#endif
