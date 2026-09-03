using UnityEngine;

namespace Swarm.Enemy
{
    /// <summary>A boss attack that occupies the boss for as long as it is resolving.</summary>
    public interface IBossPattern
    {
        bool IsCasting { get; }
    }

    /// <summary>
    /// Patterns run on independent timers, so without this two of them would occasionally come
    /// due on the same frame and the player would be asked to dodge two circles at once through
    /// no fault of their own. Each pattern asks the gate before starting and simply waits for its
    /// next tick if the boss is already busy.
    /// </summary>
    public static class BossPatternGate
    {
        public static bool IsAnyPatternCasting(GameObject boss, IBossPattern self)
        {
            var patterns = boss.GetComponents<MonoBehaviour>();
            foreach (var behaviour in patterns)
            {
                if (behaviour is not IBossPattern pattern || ReferenceEquals(pattern, self)) continue;
                if (pattern.IsCasting) return true;
            }

            return false;
        }
    }
}
