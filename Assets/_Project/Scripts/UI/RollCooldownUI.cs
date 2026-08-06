using Swarm.Weapon;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    public class RollCooldownUI : MonoBehaviour
    {
        [SerializeField] private GameObject iconRoot;
        [SerializeField] private Image fillImage;
        [SerializeField] private Button rollButton;

        private ArcherRollWeapon _rollWeapon;
        private ArcherToxicRollWeapon _toxicRollWeapon;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.TryGetComponent(out _rollWeapon);
                player.TryGetComponent(out _toxicRollWeapon);
            }
        }

        private void Update()
        {
            var active = GetActive();

            if (active == null)
            {
                iconRoot.SetActive(false);
                return;
            }

            iconRoot.SetActive(true);
            fillImage.fillAmount = active.CooldownProgress01;

            if (rollButton != null) rollButton.interactable = active.CooldownProgress01 >= 1f;
        }

        public void TryRoll()
        {
            GetActive()?.TryRoll();
        }

        private IActiveRoll GetActive()
        {
            if (_toxicRollWeapon != null && _toxicRollWeapon.Level > 0) return _toxicRollWeapon;
            if (_rollWeapon != null && _rollWeapon.Level > 0) return _rollWeapon;
            return null;
        }
    }
}
