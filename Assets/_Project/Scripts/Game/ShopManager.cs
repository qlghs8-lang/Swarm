using UnityEngine;
using UnityEngine.UI;

namespace Swarm.Game
{
    public class ShopManager : MonoBehaviour
    {
        [SerializeField] private GameObject backPanel;
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private Text goldText;

        [SerializeField] private PassiveUpgrade[] upgrades;
        [SerializeField] private RectTransform slotContainer;
        [SerializeField] private ShopSlot slotPrefab;

        private const float SlotHeight = 80f;

        public void Open()
        {
            backPanel.SetActive(false);
            shopPanel.SetActive(true);
            RefreshUI();
        }

        public void Close()
        {
            shopPanel.SetActive(false);
            backPanel.SetActive(true);
        }

        private void RefreshUI()
        {
            goldText.text = $"보유 골드: {GoldWallet.Current}";

            for (int i = slotContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(slotContainer.GetChild(i).gameObject);
            }

            float startY = (upgrades.Length - 1) * SlotHeight * 0.5f;

            for (int i = 0; i < upgrades.Length; i++)
            {
                PassiveUpgrade upgrade = upgrades[i];
                ShopSlot slot = Instantiate(slotPrefab, slotContainer);
                slot.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, startY - i * SlotHeight);

                if (upgrade.IsMaxed)
                {
                    slot.Setup($"{upgrade.DisplayName}\nLv. {upgrade.Level}/{upgrade.MaxLevel} (MAX)", "MAX", false, null);
                }
                else
                {
                    slot.Setup($"{upgrade.DisplayName}\nLv. {upgrade.Level}/{upgrade.MaxLevel} - {upgrade.Cost} G", "구매", true, () =>
                    {
                        upgrade.TryPurchase();
                        RefreshUI();
                    });
                }
            }
        }
    }
}
