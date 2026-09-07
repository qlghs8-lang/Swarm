namespace Swarm.Game
{
    /// <summary>
    /// 개발/테스트용 UI를 화면에 띄울지 여부를 한 곳에서 결정한다.
    ///
    /// 기본값은 "끔"이다. 에디터에서 플레이해도 실제 출시 빌드와 같은 화면이 나오도록 하기
    /// 위해서다. 테스트 버튼이 필요할 때는 Unity 상단 메뉴 <c>Swarm ▸ 개발용 UI 표시</c>를
    /// 체크하면 된다(에디터 전용 설정이라 빌드에는 영향이 없다). 체크 상태는 Play 시작 시점에
    /// 읽히므로, 켜고 끈 뒤에는 Play를 다시 시작해야 반영된다.
    ///
    /// Development Build에서는 성능 측정·밸런스 확인이 목적이므로 항상 켜진다.
    /// 릴리즈 빌드에서는 어떤 경우에도 켜지지 않는다.
    /// </summary>
    public static class DevUi
    {
        public const string EditorPrefKey = "Swarm_ShowDevUi";

        public static bool IsEnabled
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(EditorPrefKey, false);
#elif DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }
    }
}
