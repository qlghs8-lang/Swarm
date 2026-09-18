using UnityEngine;

namespace Swarm.UI
{
    /// <summary>
    /// 런타임에 만들어지는 <see cref="UnityEngine.UI.Text"/>가 쓸 폰트.
    /// 씬과 프리팹의 Text가 인스펙터에서 물고 있는 것과 같은 에셋이다.
    ///
    /// <para>
    /// 이 클래스는 WebGL 때문에 생겼다. 유니티 내장 폰트(<c>LegacyRuntime.ttf</c>)에는 한글 글리프가 없다.
    /// 데스크톱에서는 그게 드러나지 않는데, 유니티가 빠진 글자를 OS에 설치된 폰트(맑은 고딕)로 대신
    /// 그려주기 때문이다. <b>WebGL에는 OS 폰트가 없다.</b> 그래서 웹 빌드에서만 한글이 통째로 빈칸이
    /// 되고 영문·숫자는 멀쩡히 보인다 — docs/webgl-build.md §6.
    /// </para>
    ///
    /// <para>
    /// 고치는 방법은 한글이 든 폰트를 빌드에 직접 넣는 것뿐이고, 그러려면 <c>Resources.GetBuiltinResource</c>를
    /// 부르던 자리가 전부 한 곳을 보게 해야 한다. 씬·프리팹의 Text는 인스펙터에서 에셋을 물면 되지만,
    /// 설정 메뉴와 버프 아이콘 카운트는 코드가 <c>new GameObject</c>로 만든다 — 그 셋이 여기를 거친다.
    /// </para>
    ///
    /// <para>
    /// Resources를 쓰는 근거는 <c>ArenaBuilder</c>·<c>PotScatterer</c>와 같다. 이 폰트를 필요로 하는
    /// 곳들이 인스펙터가 없는 정적 클래스라, 직접 참조를 쓰려면 씬마다 관리자 오브젝트를 만들어
    /// 같은 배선을 중복으로 유지해야 한다 — docs/portfolio-notes/P3-1-resources.md 참고.
    /// 폰트는 서브셋(상용한글 2350자)이라 480KB다.
    /// </para>
    /// </summary>
    public static class UiFont
    {
        /// <summary><c>Assets/_Project/Resources/</c> 기준 경로. 확장자는 붙이지 않는다.</summary>
        private const string ResourcePath = "Galmuri11-Swarm";

        private static Font _font;

        /// <summary>UI 폰트. 처음 한 번만 로드하고 그다음은 캐시를 돌려준다.</summary>
        public static Font Current
        {
            get
            {
                if (_font != null) return _font;

                _font = Resources.Load<Font>(ResourcePath);
                if (_font != null) return _font;

                // 폰트가 사라져도 UI가 통째로 없어지지는 않게 한다. 이 경로로 빠지면 증상은
                // 정확히 이 클래스가 생기기 전으로 되돌아간다 — 데스크톱은 멀쩡하고 WebGL에서만
                // 한글이 빈칸이 된다. 조용히 그렇게 되면 원인을 찾는 데 또 시간이 걸리므로 로그를 남긴다.
                Debug.LogWarning(
                    $"[UI] 폰트 리소스 '{ResourcePath}'를 찾지 못했다. 내장 폰트로 대체한다 — " +
                    "WebGL 빌드에서는 한글이 보이지 않게 된다.");

                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }
    }
}
