using UnityEngine;

namespace Swarm.UI
{
    /// <summary>
    /// 설정 창이 쓸 스프라이트 묶음.
    ///
    /// <see cref="SettingsMenu"/>는 런타임에 스스로 만들어지는 오브젝트라 인스펙터에서 스프라이트를
    /// 끌어다 놓을 자리가 없고, UI 아트는 Resources 밖(Textures/UI)에 있어 이름으로 불러올 수도 없다.
    /// 그래서 참조를 담을 에셋 하나를 Resources에 두고 그것만 이름으로 불러온다 —
    /// 아트를 교체할 때 코드가 아니라 이 에셋만 만지면 된다는 뜻이기도 하다.
    ///
    /// 에셋이 없거나 칸이 비어 있으면 <see cref="SettingsMenu"/>가 코드로 그린 도형으로 대체하므로,
    /// 이 파일이 사라져도 설정 창은 뜬다.
    /// </summary>
    [CreateAssetMenu(fileName = "SettingsSkin", menuName = "Swarm/Settings Menu Skin")]
    public sealed class SettingsMenuSkin : ScriptableObject
    {
        [Header("Frames (9-slice)")]
        [SerializeField] private Sprite panel;
        [SerializeField] private Sprite button;
        [SerializeField] private Sprite buttonHover;
        [SerializeField] private Sprite buttonPressed;
        [SerializeField] private Sprite buttonDisabled;
        [SerializeField] private Sprite buttonPrimary;
        [SerializeField] private Sprite barFrame;
        [SerializeField] private Sprite barFill;

        [Header("Icons")]
        [SerializeField] private Sprite gearIcon;

        public Sprite Panel => panel;
        public Sprite Button => button;
        public Sprite ButtonHover => buttonHover;
        public Sprite ButtonPressed => buttonPressed;
        public Sprite ButtonDisabled => buttonDisabled;
        public Sprite ButtonPrimary => buttonPrimary;
        public Sprite BarFrame => barFrame;
        public Sprite BarFill => barFill;
        public Sprite GearIcon => gearIcon;
    }
}
