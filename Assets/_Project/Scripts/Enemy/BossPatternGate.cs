using System.Collections.Generic;
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
        // Reused across calls: the array-returning GetComponents allocated a fresh array on every
        // query, and a pattern that is due but blocked asks again every frame until it is free.
        // Nothing this method calls can re-enter it, so one shared buffer is safe.
        private static readonly List<MonoBehaviour> BehaviourBuffer = new();

        public static bool IsAnyPatternCasting(GameObject boss, IBossPattern self)
        {
            boss.GetComponents(BehaviourBuffer);

            for (var i = 0; i < BehaviourBuffer.Count; i++)
            {
                if (BehaviourBuffer[i] is not IBossPattern pattern || ReferenceEquals(pattern, self)) continue;
                if (pattern.IsCasting) return true;
            }

            return false;
        }
    }
}
