using UnityEngine;

namespace Swarm.Audio
{
    /// <summary>효과음 종류. 지금 잡아둔 스코프는 획득음 두 갈래뿐이다.</summary>
    public enum SoundEffect
    {
        /// <summary>경험치 구슬 획득.</summary>
        ExperiencePickup,
        /// <summary>골드·자석·회복 등 그 밖의 획득물.</summary>
        ObjectPickup
    }

    /// <summary>
    /// 효과음 재생구. <see cref="BackgroundMusic"/>과 같은 방식으로 스스로 만들어지고 씬 로드를 넘겨 산다.
    ///
    /// **아직 게임 쪽 어디에서도 호출하지 않는다.** 설정 창의 효과음 슬라이더가 붙을 자리를 먼저
    /// 만들어 두는 것이 목적이고, 실제 재생은 클립이 준비된 뒤 획득 지점에서 <see cref="Play"/>를
    /// 한 줄 부르면 그대로 살아난다.
    ///
    /// 클립은 Resources에서 이름으로 찾는다. Assets/_Project/Resources/SFX/ 아래에
    /// ExperiencePickup.*, ObjectPickup.* 을 넣으면 잡히고, 파일이 없으면 조용히 아무 일도 하지 않는다 —
    /// 없는 클립은 매 프레임 콘솔을 채울 만한 오류가 아니다.
    /// </summary>
    public static class SoundEffects
    {
        private const string ClipFolder = "SFX/";
        private const string ObjectName = "SoundEffects (Runtime)";
        private const string VolumeKey = "swarm.sfx.volume";
        private const float DefaultVolume = 0.8f;

        /// <summary>한 프레임에 같은 소리가 겹쳐 울리며 볼륨이 튀는 것을 막는 최소 간격(초).</summary>
        private const float RetriggerInterval = 0.04f;

        private static AudioSource _source;
        private static readonly AudioClip[] _clips = new AudioClip[System.Enum.GetValues(typeof(SoundEffect)).Length];
        private static readonly float[] _lastPlayedAt = new float[System.Enum.GetValues(typeof(SoundEffect)).Length];
        private static bool _clipsLoaded;

        /// <summary>0-1. 저장되므로 소리를 줄여둔 플레이어는 다음 실행에도 줄어든 채로 시작한다.</summary>
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

        /// <summary>
        /// 효과음 한 번. 클립이 없으면 아무 일도 하지 않는다.
        /// 설정에서 볼륨을 0으로 내려두면 재생 자체를 건너뛴다.
        /// </summary>
        public static void Play(SoundEffect effect)
        {
            if (_source == null || Volume <= 0f) return;

            EnsureClipsLoaded();

            var index = (int)effect;
            var clip = _clips[index];
            if (clip == null) return;

            // 경험치 구슬은 자석에 끌려 한 프레임에 수십 개가 한꺼번에 들어온다. 그대로 다 울리면
            // 소리가 겹쳐 커지기만 하고 개수는 들리지 않는다.
            if (Time.unscaledTime - _lastPlayedAt[index] < RetriggerInterval) return;
            _lastPlayedAt[index] = Time.unscaledTime;

            _source.PlayOneShot(clip, Volume);
        }

        /// <summary>설정 창에서 슬라이더를 놓았을 때 지금 볼륨을 귀로 확인시켜 주는 용도.</summary>
        public static void PlayPreview() => Play(SoundEffect.ObjectPickup);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_source != null) return;

            var host = new GameObject(ObjectName);
            Object.DontDestroyOnLoad(host);

            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.volume = Volume;
            // 2D: 리스너가 플레이어를 따라다니므로 위치를 가진 소리는 좌우로 흔들린다.
            _source.spatialBlend = 0f;
            _source.ignoreListenerPause = true;
        }

        private static void EnsureClipsLoaded()
        {
            if (_clipsLoaded) return;
            _clipsLoaded = true;

            for (var i = 0; i < _clips.Length; i++)
            {
                _clips[i] = Resources.Load<AudioClip>(ClipFolder + ((SoundEffect)i));
            }
        }
    }
}
