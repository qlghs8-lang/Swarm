using System;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.Game
{
    public class ShopSlot : MonoBehaviour
    {
        [SerializeField] private Text labelText;
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionButtonText;

        public void Setup(string label, string buttonLabel, bool interactable, Action onClick)
        {
            labelText.text = label;
            actionButtonText.text = buttonLabel;
            actionButton.interactable = interactable;

            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => onClick?.Invoke());
        }
    }
}
