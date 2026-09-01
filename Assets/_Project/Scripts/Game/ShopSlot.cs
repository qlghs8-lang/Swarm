using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.Game
{
    public class ShopSlot : MonoBehaviour
    {
        [SerializeField] private Text labelText;
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionButtonText;

        [Header("Level pips")]
        [SerializeField] private Color pipFilledColor = new(0.36f, 0.86f, 0.56f, 1f);
        [SerializeField] private Color pipEmptyColor = new(1f, 1f, 1f, 0.16f);
        [SerializeField] private Vector2 pipRowSize = new(288f, 6f);
        [SerializeField] private Vector2 pipRowOffset = new(16f, 6f);
        [SerializeField] private float pipSpacing = 3f;

        [Header("Affordability")]
        [SerializeField] private Color affordableTextColor = Color.white;
        [SerializeField] private Color unaffordableTextColor = new(1f, 0.55f, 0.5f, 1f);

        // Built on demand rather than authored into the prefab, so the row adapts to however many
        // levels an upgrade has — it went from 5 to 10 without anyone touching the prefab.
        private RectTransform _pipRow;
        private readonly List<Image> _pips = new();

        public void Setup(string label, string buttonLabel, bool interactable, Action onClick)
        {
            labelText.text = label;
            labelText.color = affordableTextColor;
            actionButtonText.text = buttonLabel;
            actionButton.interactable = interactable;

            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => onClick?.Invoke());
        }

        /// <summary>Marks the row as priced beyond the player's gold — the button used to stay lit
        /// and a click simply did nothing.</summary>
        public void SetAffordable(bool affordable)
        {
            labelText.color = affordable ? affordableTextColor : unaffordableTextColor;
        }

        public void SetProgress(int level, int maxLevel)
        {
            if (maxLevel <= 0)
            {
                HideProgress();
                return;
            }

            EnsurePipRow();
            _pipRow.gameObject.SetActive(true);

            var pipWidth = (pipRowSize.x - pipSpacing * (maxLevel - 1)) / maxLevel;

            for (var i = 0; i < maxLevel; i++)
            {
                if (i >= _pips.Count) _pips.Add(CreatePip());

                var pip = _pips[i];
                pip.gameObject.SetActive(true);
                pip.color = i < level ? pipFilledColor : pipEmptyColor;

                var rect = (RectTransform)pip.transform;
                rect.sizeDelta = new Vector2(pipWidth, pipRowSize.y);
                rect.anchoredPosition = new Vector2(i * (pipWidth + pipSpacing), 0f);
            }

            for (var i = maxLevel; i < _pips.Count; i++)
            {
                _pips[i].gameObject.SetActive(false);
            }
        }

        public void HideProgress()
        {
            if (_pipRow != null) _pipRow.gameObject.SetActive(false);
        }

        private void EnsurePipRow()
        {
            if (_pipRow != null) return;

            var rowObject = new GameObject("LevelPips", typeof(RectTransform));
            _pipRow = (RectTransform)rowObject.transform;
            _pipRow.SetParent(transform, false);
            _pipRow.anchorMin = _pipRow.anchorMax = _pipRow.pivot = Vector2.zero;
            _pipRow.anchoredPosition = pipRowOffset;
            _pipRow.sizeDelta = pipRowSize;
        }

        private Image CreatePip()
        {
            var pipObject = new GameObject("Pip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)pipObject.transform;
            rect.SetParent(_pipRow, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;

            var image = pipObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
