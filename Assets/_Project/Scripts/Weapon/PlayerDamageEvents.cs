using System;
using UnityEngine;

namespace Swarm.Weapon
{
    public static class PlayerDamageEvents
    {
        public static event Action<GameObject> OnDamageDealt;

        public static void RaiseDamageDealt(GameObject target)
        {
            OnDamageDealt?.Invoke(target);
        }
    }
}
