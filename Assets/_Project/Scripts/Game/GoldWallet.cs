using UnityEngine;
using UnityEngine.SceneManagement;

namespace Swarm.Game
{
    public static class GoldWallet
    {
        private const string GoldKey = "Swarm_Gold";

        // PlayerPrefs.Save() is a synchronous disk write (the registry on Windows). It used to run
        // on every single gold pickup, so a drop mid-fight cost a frame hitch — and gold drops
        // from any enemy kill. The balance is cached here and flushed only at the moments where
        // losing it would actually matter: focus loss, scene change, and quit.
        private static int _cached;
        private static bool _loaded;
        private static bool _dirty;

        /// <summary>
        /// Raised whenever the balance changes. UI that displays gold but does not own the
        /// transaction — the title screen's total while the shop panel is spending — has no other
        /// way to know it went stale.
        /// </summary>
        public static event System.Action OnChanged;

        public static int Current
        {
            get
            {
                if (!_loaded)
                {
                    _cached = PlayerPrefs.GetInt(GoldKey, 0);
                    _loaded = true;
                }

                return _cached;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            // With domain reload disabled in the editor, static state survives between play
            // sessions, so the cache and the flush hooks are both re-established here.
            _loaded = false;
            _dirty = false;
            OnChanged = null;

            Application.focusChanged -= HandleFocusChanged;
            Application.focusChanged += HandleFocusChanged;
            Application.quitting -= Flush;
            Application.quitting += Flush;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private static void HandleFocusChanged(bool hasFocus)
        {
            if (!hasFocus) Flush();
        }

        private static void HandleSceneUnloaded(Scene scene) => Flush();

        public static void Add(int amount)
        {
            Set(Current + amount);
        }

        public static bool TrySpend(int amount)
        {
            if (Current < amount) return false;

            Set(Current - amount);
            // Purchases are rare and player-initiated, so this one is worth persisting now.
            Flush();
            return true;
        }

        /// <summary>Drops the cache after an external write such as PlayerPrefs.DeleteAll().</summary>
        public static void Invalidate()
        {
            _loaded = false;
            _dirty = false;
        }

        public static void Flush()
        {
            if (!_dirty) return;

            _dirty = false;
            PlayerPrefs.Save();
        }

        private static void Set(int value)
        {
            _cached = value;
            _loaded = true;
            _dirty = true;
            // Cheap: this only updates PlayerPrefs' in-memory table. Save() is the expensive part.
            PlayerPrefs.SetInt(GoldKey, value);
            OnChanged?.Invoke();
        }
    }
}
