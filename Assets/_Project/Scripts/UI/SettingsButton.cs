using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    /// <summary>
    /// 씬에 만들어 둔 톱니 버튼에 붙이기만 하면 설정 창이 열리도록 이어 주는 조각.
    ///
    /// 같은 오브젝트의 Button(또는 <see cref="UIPressButton"/>)을 찾아 스스로 구독하므로,
    /// 인스펙터에서 OnClick에 대상을 끌어다 놓을 필요가 없다 — <see cref="SettingsMenu"/>는
    /// 런타임에 만들어져 씬 파일에 존재하지 않기 때문에 애초에 끌어다 놓을 대상이 없기도 하다.
    ///
    /// 씬에 Button이 없다면 이 컴포넌트의 <see cref="Open"/>을 아무 UnityEvent에나 직접 연결해도 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsButton : MonoBehaviour
    {
        [Tooltip("비워두면 같은 오브젝트의 Button을 쓴다.")]
        [SerializeField] private Button button;

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(Open);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Open);
        }

        /// <summary>설정 창 열기. UnityEvent에서 직접 부를 수 있게 public.</summary>
        public void Open() => SettingsMenu.OpenMenu();

        /// <summary>열려 있으면 닫고, 닫혀 있으면 연다.</summary>
        public void Toggle() => SettingsMenu.ToggleMenu();
    }
}
