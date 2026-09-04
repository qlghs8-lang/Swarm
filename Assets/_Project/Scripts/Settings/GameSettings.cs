using System;
using Swarm.Audio;
using UnityEngine;

namespace Swarm.Settings
{
    /// <summary>구르기 버튼이 화면 어느 쪽에 붙는지.</summary>
    public enum RollButtonSide
    {
        Right = 0,
        Left = 1
    }

    /// <summary>
    /// 플레이어가 고른 옵션 한 벌. PlayerPrefs 위에 얇게 얹은 정적 클래스로, 인스턴스도 씬 오브젝트도
    /// 없다 — 설정을 읽는 쪽(구르기 버튼, 데미지 숫자)이 서로 다른 씬·다른 수명 주기에 흩어져 있어서,
    /// 참조를 물려주는 방식으로는 어디선가 반드시 끊긴다.
    ///
    /// 값이 바뀌면 <see cref="OnChanged"/>가 한 번 울린다. 구독자는 자기 것만 다시 반영하면 된다.
    ///
    /// 배경음/효과음 볼륨은 여기서 보관하지 않고 <see cref="BackgroundMusic"/>·<see cref="SoundEffects"/>에
    /// 그대로 넘긴다. 볼륨을 실제로 스피커에 반영하는 쪽이 값을 소유하는 편이 어긋날 여지가 없다.
    /// </summary>
    public static class GameSettings
    {
        private const string RollSideKey = "swarm.settings.rollSide";
        private const string DamageNumbersKey = "swarm.settings.damageNumbers";

        /// <summary>옵션이 하나라도 바뀔 때마다 호출된다.</summary>
        public static event Action OnChanged;

        public static RollButtonSide RollSide
        {
            get => (RollButtonSide)PlayerPrefs.GetInt(RollSideKey, (int)RollButtonSide.Right);
            set
            {
                if (RollSide == value) return;
                PlayerPrefs.SetInt(RollSideKey, (int)value);
                PlayerPrefs.Save();
                Raise();
            }
        }

        /// <summary>적이 맞을 때 뜨는 데미지 숫자를 그릴지. 끄면 화면이 훨씬 조용해진다.</summary>
        public static bool ShowDamageNumbers
        {
            get => PlayerPrefs.GetInt(DamageNumbersKey, 1) != 0;
            set
            {
                if (ShowDamageNumbers == value) return;
                PlayerPrefs.SetInt(DamageNumbersKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Raise();
            }
        }

        /// <summary>0-1. 실제 보관·적용은 <see cref="BackgroundMusic"/>이 한다.</summary>
        public static float BgmVolume
        {
            get => BackgroundMusic.Volume;
            set
            {
                if (Mathf.Approximately(BackgroundMusic.Volume, Mathf.Clamp01(value))) return;
                BackgroundMusic.Volume = value;
                Raise();
            }
        }

        /// <summary>0-1. 실제 보관·적용은 <see cref="SoundEffects"/>가 한다.</summary>
        public static float SfxVolume
        {
            get => SoundEffects.Volume;
            set
            {
                if (Mathf.Approximately(SoundEffects.Volume, Mathf.Clamp01(value))) return;
                SoundEffects.Volume = value;
                Raise();
            }
        }

        private static void Raise() => OnChanged?.Invoke();
    }
}
