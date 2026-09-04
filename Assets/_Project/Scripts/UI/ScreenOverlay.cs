using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    /// <summary>
    /// A full-screen colour wash built at runtime — the death tint, the clear flash.
    ///
    /// Built in code rather than placed in the scene so the result sequence needs no extra
    /// inspector wiring, and so its sorting order can be chosen per use: the tint sits *under*
    /// the HUD canvas (negative order) because the result panel has to read on top of it, while a
    /// flash sits above everything.
    ///
    /// All fades run on unscaled time: the sequence that drives them spends most of its length at
    /// timeScale 0.
    /// </summary>
    public sealed class ScreenOverlay
    {
        private readonly Image _image;

        private ScreenOverlay(Image image)
        {
            _image = image;
        }

        public static ScreenOverlay Create(string name, int sortingOrder, Color initial)
        {
            var host = new GameObject(name, typeof(Canvas), typeof(CanvasRenderer), typeof(Image));
            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var image = host.GetComponent<Image>();
            image.color = initial;
            // Never eat a button press: the result panel's buttons live on the HUD canvas below.
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return new ScreenOverlay(image);
        }

        public Color Color
        {
            get => _image != null ? _image.color : Color.clear;
            set { if (_image != null) _image.color = value; }
        }

        public IEnumerator FadeTo(Color target, float duration)
        {
            if (_image == null) yield break;

            var from = _image.color;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(elapsed / duration);
                _image.color = Color.Lerp(from, target, k * k * (3f - 2f * k));
                yield return null;
            }

            _image.color = target;
        }

        /// <summary>Snaps to <paramref name="color"/> and falls off to transparent.</summary>
        public IEnumerator Flash(Color color, float duration)
        {
            if (_image == null) yield break;

            _image.color = color;
            var faded = color;
            faded.a = 0f;
            yield return FadeTo(faded, duration);
        }

        public void Destroy()
        {
            if (_image != null) Object.Destroy(_image.canvas.gameObject);
        }
    }
}
