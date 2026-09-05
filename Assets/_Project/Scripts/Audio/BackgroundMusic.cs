using UnityEngine;

namespace Swarm.Audio
{
    /// <summary>
    /// Plays one looping background track for the whole game.
    ///
    /// It builds itself at runtime and survives scene loads, which matters here because the title,
    /// the stage and every restart are separate scene loads — a BGM placed inside a scene would
    /// cut out and restart from zero each time the player dies or returns to the title.
    ///
    /// The track itself now lives in Wwise (Music_Bed) rather than in an AudioClip, so what plays,
    /// how many layers are audible and when they cross-fade is decided there. This class only owns
    /// the two values the game has an opinion about — the player's volume setting and the pitch the
    /// death sequence bends — and hands them to <see cref="AudioDirector"/>. The public API is
    /// unchanged, so GameManager, ResultSequence, GameSettings and SettingsMenu never learn that
    /// the backend moved.
    /// </summary>
    public static class BackgroundMusic
    {
        private const string VolumeKey = "swarm.bgm.volume";
        private const float DefaultVolume = 0.5f;

        /// <summary>0-1. Persisted, so a player who turns the music down stays turned down.</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
            set
            {
                var clamped = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, clamped);
                PlayerPrefs.Save();
                AudioDirector.SetMusicVolume(clamped);
            }
        }

        /// <summary>
        /// Bends the track's playback rate. The death sequence drags it down as the world slows,
        /// which is most of why slow motion reads as slow motion rather than as a frame rate
        /// problem. Restored to 1 when a run restarts — the music outlives the scene.
        /// </summary>
        public static void SetPitch(float pitch) => AudioDirector.SetMusicPitch(pitch);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // The sound engine may not be up yet — the two RuntimeInitializeOnLoadMethod hooks have
            // no defined order between them. AudioDirector holds these until it can send them, so
            // nothing here has to know or care.
            AudioDirector.SetMusicVolume(Volume);
            AudioDirector.SetMusicPitch(1f);
            AudioDirector.PlayMusic();
        }
    }
}
