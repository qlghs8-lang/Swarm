using UnityEngine;

namespace Swarm.Game
{
    /// <summary>
    /// Counters that describe a single run, for the result screen to report.
    ///
    /// Static rather than a component because the things being counted happen inside pooled
    /// enemies that have no reason to know a GameManager exists. The run boundary is a scene load,
    /// so <see cref="Reset"/> is called from GameManager.Start rather than tracked here.
    /// </summary>
    public static class RunStats
    {
        public static int Kills { get; private set; }

        /// <summary>
        /// True from the moment the result sequence starts. Systems that interrupt play — the
        /// level-up card panel above all — check this: the clear sweep pours every orb left on the
        /// ground into the player, which is enough experience to trigger a level up, and a card
        /// panel taking over timeScale in the middle of the ending is not a choice worth offering.
        /// </summary>
        public static bool RunEnded { get; private set; }

        public static void Reset()
        {
            Kills = 0;
            RunEnded = false;
        }

        public static void MarkRunEnded()
        {
            RunEnded = true;
        }

        public static void AddKill()
        {
            Kills++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            // Domain reload is disabled in the editor, so static state survives between play
            // sessions and would otherwise carry the previous session's kills into the first run.
            Kills = 0;
            RunEnded = false;
        }
    }
}
