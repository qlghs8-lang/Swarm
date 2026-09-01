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
    /// The clip is pulled from Resources so no scene or prefab has to reference it: drop an audio
    /// file at Assets/_Project/Resources/BGM.* (any Unity-supported format) and it plays. If the
    /// file is absent this does nothing at all, silently — a missing track is not an error worth
    /// spamming the console over on every launch.
    /// </summary>
    public static class BackgroundMusic
    {
        private const string ClipResourcePath = "BGM";
        private const string ObjectName = "BackgroundMusic (Runtime)";
        private const string VolumeKey = "swarm.bgm.volume";
        private const float DefaultVolume = 0.5f;

        private static AudioSource _source;

        /// <summary>0-1. Persisted, so a player who turns the music down stays turned down.</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
            set
            {
                var clamped = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, clamped);
                PlayerPrefs.Save();
                if (_source != null) _source.volume = clamped;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // A domain reload between play sessions clears _source but leaves nothing behind, so
            // this only ever runs once per play session.
            if (_source != null) return;

            var clip = Resources.Load<AudioClip>(ClipResourcePath);
            if (clip == null) return;

            var host = new GameObject(ObjectName);
            Object.DontDestroyOnLoad(host);

            _source = host.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = Volume;
            // 2D: the listener moves with the player, and a positional BGM would pan and fade.
            _source.spatialBlend = 0f;
            // The game pauses with Time.timeScale = 0; audio should keep playing through it.
            _source.ignoreListenerPause = true;
            _source.Play();
        }
    }
}
