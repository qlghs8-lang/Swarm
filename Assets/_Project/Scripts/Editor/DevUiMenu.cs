using UnityEditor;

namespace Swarm.Game
{
    /// <summary>
    /// <see cref="DevUi"/> 스위치를 켜고 끄는 메뉴. EditorPrefs에 저장되므로 프로젝트 파일이나
    /// 빌드에는 영향을 주지 않고, 이 PC의 에디터에서만 유효하다.
    /// </summary>
    internal static class DevUiMenu
    {
        private const string MenuPath = "Swarm/개발용 UI 표시";

        [MenuItem(MenuPath, priority = 0)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(DevUi.EditorPrefKey, !DevUi.IsEnabled);
        }

        [MenuItem(MenuPath, isValidateFunction: true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, DevUi.IsEnabled);
            return !EditorApplication.isPlaying;
        }
    }
}
