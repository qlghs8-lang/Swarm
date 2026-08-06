using Swarm.Weapon;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    public class RageStackUI : MonoBehaviour
    {
        [SerializeField] private Text stackText;

        private WarriorRageWeapon _rageWeapon;

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.TryGetComponent(out _rageWeapon);
            }
        }

        private void Update()
        {
            if (stackText == null) return;

            var active = _rageWeapon != null && _rageWeapon.Level > 0;
            stackText.text = active ? $"레이지 스택 {_rageWeapon.StackCount}" : string.Empty;
        }
    }
}
