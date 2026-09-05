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
    /// 효과음 재생구. 소리 자체는 Wwise의 <c>SFX</c> 액터믹서가 들고 있고, 이 클래스는 "무엇이 일어났다"를
    /// <see cref="AudioDirector"/>로 넘기는 얇은 층이다. 어떤 파일이 어떤 볼륨으로 몇 번 겹쳐 울릴지는 전부
    /// Wwise 쪽 사정이다.
    ///
    /// public API는 AudioSource를 쓰던 시절과 같다. 설정 창의 효과음 슬라이더는 한 줄도 바뀌지 않는다.
    /// </summary>
    public static class SoundEffects
    {
        private const string VolumeKey = "swarm.sfx.volume";
        private const float DefaultVolume = 0.8f;

        /// <summary>한 프레임에 같은 소리가 겹쳐 울리며 볼륨이 튀는 것을 막는 최소 간격(초).</summary>
        private const float RetriggerInterval = 0.04f;

        private static readonly float[] _lastPlayedAt = new float[System.Enum.GetValues(typeof(SoundEffect)).Length];

        /// <summary>0-1. 저장되므로 소리를 줄여둔 플레이어는 다음 실행에도 줄어든 채로 시작한다.</summary>
        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
            set
            {
                var clamped = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, clamped);
                PlayerPrefs.Save();
                AudioDirector.SetSfxVolume(clamped);
            }
        }

        /// <summary>효과음 한 번. 설정에서 볼륨을 0으로 내려두면 이벤트 자체를 보내지 않는다.</summary>
        public static void Play(SoundEffect effect)
        {
            if (Volume <= 0f) return;

            // 경험치 구슬은 자석에 끌려 한 프레임에 수십 개가 한꺼번에 들어온다. 그대로 다 보내면
            // 소리가 겹쳐 커지기만 하고 개수는 들리지 않는다. Wwise의 Playback Limit으로도 같은 것을
            // 걸 수 있지만, 보이스를 만들었다가 버리는 것보다 애초에 보내지 않는 쪽이 싸다.
            var index = (int)effect;
            if (Time.unscaledTime - _lastPlayedAt[index] < RetriggerInterval) return;
            _lastPlayedAt[index] = Time.unscaledTime;

            switch (effect)
            {
                case SoundEffect.ExperiencePickup:
                    AudioDirector.PlayXpPickup();
                    break;
                case SoundEffect.ObjectPickup:
                    AudioDirector.PlayObjectPickup();
                    break;
            }
        }

        /// <summary>설정 창에서 슬라이더를 놓았을 때 지금 볼륨을 귀로 확인시켜 주는 용도.</summary>
        public static void PlayPreview() => Play(SoundEffect.ObjectPickup);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            AudioDirector.SetSfxVolume(Volume);
        }
    }
}
