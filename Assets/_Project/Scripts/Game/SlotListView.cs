using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.Game
{
    /// <summary>
    /// A scrollable, layout-driven list of <see cref="ShopSlot"/> rows, shared by the shop and the
    /// character select.
    ///
    /// Both screens used to hand-place their rows — <c>anchoredPosition = (0, startY - i * height)</c>
    /// against a fixed-size container — and destroy every row on each refresh only to instantiate
    /// the same number back. That silently overflowed the moment a ninth entry appeared, and each
    /// purchase churned eight GameObjects. Rows are now reused and positioned by a layout group.
    ///
    /// The scroll rig is assembled here rather than authored into the scene, so nothing has to be
    /// re-wired in the inspector: the container the caller passes becomes the viewport, and the
    /// rows live in a Content child that grows to fit them.
    /// </summary>
    public sealed class SlotListView
    {
        private readonly ShopSlot _prefab;
        private readonly RectTransform _content;
        private readonly List<ShopSlot> _slots = new();

        private SlotListView(ShopSlot prefab, RectTransform content)
        {
            _prefab = prefab;
            _content = content;
        }

        public static SlotListView Attach(RectTransform viewport, ShopSlot prefab,
                                          float spacing = 8f, int padding = 8)
        {
            // Idempotent: re-attaching (a scene reload, a second Open) reuses the rig already built.
            var content = viewport.Find("Content") as RectTransform;
            if (content == null)
            {
                content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(viewport, false);
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            var layout = Ensure<VerticalLayoutGroup>(content.gameObject);
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = TextAnchor.UpperCenter;
            // Rows keep their authored width and height. The prefab anchors its label and button
            // at fixed offsets from the row's centre, so stretching the row would leave the button
            // floating away from the right edge.
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;

            var fitter = Ensure<ContentSizeFitter>(content.gameObject);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // RectMask2D rather than Mask: it clips without needing an Image or an extra draw call.
            Ensure<RectMask2D>(viewport.gameObject);

            var scroll = Ensure<ScrollRect>(viewport.gameObject);
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            return new SlotListView(prefab, content);
        }

        /// <summary>
        /// Shows <paramref name="count"/> rows, calling <paramref name="configure"/> for each.
        /// Rows are reused; surplus rows are hidden rather than destroyed.
        /// </summary>
        public void Rebuild(int count, Action<int, ShopSlot> configure)
        {
            while (_slots.Count < count)
            {
                _slots.Add(UnityEngine.Object.Instantiate(_prefab, _content));
            }

            for (var i = 0; i < _slots.Count; i++)
            {
                var active = i < count;
                _slots[i].gameObject.SetActive(active);
                if (active) configure(i, _slots[i]);
            }
        }

        private static T Ensure<T>(GameObject target) where T : Component
        {
            return target.TryGetComponent<T>(out var existing) ? existing : target.AddComponent<T>();
        }
    }
}
