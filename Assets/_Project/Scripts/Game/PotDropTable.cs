using UnityEngine;
using UnityEngine.SceneManagement;

namespace Swarm.Game
{
    public enum PotDrop
    {
        Experience,
        Gold,
        HealthPack,
        Magnet,
    }

    /// <summary>
    /// What a smashed pot pays out.
    ///
    /// There is no empty result. Pots respawn and the arena is 70 units across, so a pot is the
    /// reward for walking somewhere rather than standing in one place — an empty one would teach
    /// the opposite lesson, that leaving the safe spot was a waste. The common result is instead a
    /// small experience gem: always worth the detour, never worth abandoning a good position for.
    ///
    /// Gold is the rare one at 5%. It is the only drop that survives the run (the shop spends it
    /// between runs), so pots must not become the way the meta-progression is farmed.
    ///
    /// The magnet is capped rather than merely rare. At 3% it would be a coin flip whether a ten
    /// minute run ever produced one, and a run that produced six would trivialise pickup range as
    /// a stat. <see cref="MaxMagnetsPerRun"/> holds it at two; once spent, further magnet rolls
    /// fall back to the gem rather than becoming nothing. They deliberately do not fall back to
    /// gold: gold is the one drop that leaves the run, and letting a spent cap quietly inflate it
    /// would make the shop's income depend on how the magnet rolls landed.
    /// </summary>
    public static class PotDropTable
    {
        private const float MagnetChance = 0.03f;
        private const float GoldChance = 0.05f;
        private const float HealthPackChance = 0.10f;
        // Everything else — 82% — is the small experience gem.

        public const int MaxMagnetsPerRun = 2;

        private static int _magnetsDropped;

        public static int MagnetsDropped => _magnetsDropped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            _magnetsDropped = 0;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _magnetsDropped = 0;
        }

        public static PotDrop Roll()
        {
            var roll = Random.value;

            if (roll < MagnetChance)
            {
                if (_magnetsDropped >= MaxMagnetsPerRun) return PotDrop.Experience;

                _magnetsDropped++;
                return PotDrop.Magnet;
            }

            roll -= MagnetChance;
            if (roll < GoldChance) return PotDrop.Gold;

            roll -= GoldChance;
            return roll < HealthPackChance ? PotDrop.HealthPack : PotDrop.Experience;
        }
    }
}
