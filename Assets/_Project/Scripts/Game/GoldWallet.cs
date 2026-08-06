using UnityEngine;

namespace Swarm.Game
{
    public static class GoldWallet
    {
        private const string GoldKey = "Swarm_Gold";

        public static int Current => PlayerPrefs.GetInt(GoldKey, 0);

        public static void Add(int amount)
        {
            PlayerPrefs.SetInt(GoldKey, Current + amount);
            PlayerPrefs.Save();
        }

        public static bool TrySpend(int amount)
        {
            if (Current < amount) return false;

            PlayerPrefs.SetInt(GoldKey, Current - amount);
            PlayerPrefs.Save();
            return true;
        }
    }
}
