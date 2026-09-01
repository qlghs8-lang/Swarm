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

        private SlotListView _list;

        private void OnEnable()
        {
            // The debug gold button (and any future reward) can change the balance while the shop
            // is open, and affordability is now part of how each row renders.
            GoldWallet.OnChanged += HandleGoldChanged;
        }

        private void OnDisable()
        {
            GoldWallet.OnChanged -= HandleGoldChanged;
        }

        private void HandleGoldChanged()
        {
            if (shopPanel != null && shopPanel.activeInHierarchy) RefreshUI();
        }

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

            _list ??= SlotListView.Attach(slotContainer, slotPrefab);

            _list.Rebuild(upgrades.Length, (i, slot) =>
            {
                var upgrade = upgrades[i];
                if (upgrade == null) return;

                slot.SetProgress(upgrade.Level, upgrade.MaxLevel);

                if (upgrade.IsMaxed)
                {
                    slot.Setup($"{upgrade.DisplayName}\nLv.{upgrade.Level}/{upgrade.MaxLevel} MAX  {upgrade.DescribeTotal(upgrade.Level)}",
                               "MAX", false, null);
                    slot.SetAffordable(true);
                    return;
                }

                // The row used to show only a price. What that gold actually buys — the effect now
                // and the effect after this purchase — is the part worth deciding on. Kept to two
                // lines and mostly ASCII: the label is 320px wide at font 22 and truncates rather
                // than shrinking, so a third line would be cut off.
                var affordable = GoldWallet.Current >= upgrade.Cost;
                var effect = $"{upgrade.DescribeTotal(upgrade.Level)}→{upgrade.DescribeTotal(upgrade.Level + 1)}";
                slot.Setup($"{upgrade.DisplayName}\nLv.{upgrade.Level}/{upgrade.MaxLevel}  {effect}  {upgrade.Cost}G",
                           "구매", affordable, () =>
                {
                    // Previously always interactable: clicking with too little gold silently did
                    // nothing, with no way to tell that was why.
                    upgrade.TryPurchase();
                    RefreshUI();
                });
                slot.SetAffordable(affordable);
            });
        }
    }
}
