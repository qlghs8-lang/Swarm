using UnityEngine;

namespace Swarm.Audio
{
    /// <summary>게임이 Wwise에 보고하는 상태. 이름은 Wwise의 <c>Game_State</c> 그룹과 1:1이다.</summary>
    public enum GameAudioState
    {
        Title,
        Playing,
        LevelUp,
        Dead
    }

    /// <summary>
    /// Wwise를 아는 유일한 클래스. 게임 코드는 <c>AkUnitySoundEngine</c>을 직접 부르지 않고 전부 여기를 거친다.
    ///
    /// 이렇게 두는 이유는 셋이다. Wwise를 걷어내도 이 파일 하나만 비우면 게임이 컴파일되고, 이벤트·RTPC 이름이
    /// 프로젝트 전체로 흩어지지 않으며, 어떤 게임 데이터가 오디오로 나가는지 파일 하나만 열면 전부 보인다.
    ///
    /// 여기 있는 메서드는 전부 <b>사실을 받는다.</b> "적이 32마리다", "체력이 47%다" 까지가 게임의 몫이고,
    /// 그 사실을 무엇으로 들려줄지는 Wwise가 정한다. <c>SetIntensity(High)</c> 같은 판단이 들어온 순간
    /// 파이프라인은 죽는다 — 자세한 것은 docs/wwise-pipeline.md §0-§2.
    ///
    /// <para>
    /// 파일은 두 층으로 갈라져 있다. 위쪽 <b>공개 API와 상태 관리</b>는 플랫폼과 무관하게 하나고,
    /// 아래쪽 <b>백엔드</b>(<c>IsReady</c> / <c>TryPostEvent</c> / <c>TryApplyRtpc</c> / <c>TryApplyState</c>)만
    /// 플랫폼별로 갈린다. WebGL 플레이어 빌드에는 Wwise 어셈블리 자체가 들어가지 않으므로
    /// (<c>Generated/</c>에 Windows·Mac만 있다 — docs/webgl-build.md §0) 백엔드가 무음 no-op으로 바뀐다.
    /// 조건이 <c>UNITY_WEBGL</c>이 아니라 <c>UNITY_WEBGL &amp;&amp; !UNITY_EDITOR</c>인 이유는,
    /// 에디터는 빌드 타깃이 WebGL이어도 Wwise를 계속 들고 있어서 플레이 모드 소리를 죽일 이유가 없기 때문이다.
    /// </para>
    /// </summary>
    public static class AudioDirector
    {
        // ── 계약 (docs/wwise-pipeline.md §2) ──────────────────────────────────────────────
        // 이름 문자열은 여기 적지 않는다. WwiseIds.generated.cs가 실제 사운드뱅크에서 뽑아낸 것이
        // 유일한 출처다. Wwise에서 이벤트 이름을 바꾸고 뱅크를 다시 만들면 그 파일에서 해당 상수가
        // 사라져 이 줄이 컴파일되지 않는다 — 조용히 소리만 안 나는 대신 빌드가 깨져서 즉시 알게 된다.
        //
        // 이 상수들은 순수 uint라 WebGL 빌드에서도 그대로 컴파일된다. 쓰이지 않을 뿐이다.
        private const uint EventPlayMusic = WwiseIds.Events.Play_Music;
        private const uint EventStopMusic = WwiseIds.Events.Stop_Music;
        private const uint EventPlayXpPickup = WwiseIds.Events.Play_XP_Pickup;
        private const uint EventPlayObjectPickup = WwiseIds.Events.Play_Object_Pickup;

        /// <summary>인덱스가 <see cref="GameAudioState"/>의 값과 일치한다. 상태를 늘리면 여기도 늘린다.</summary>
        private static readonly uint[] StateIds =
        {
            WwiseIds.States.Game_State.Title,
            WwiseIds.States.Game_State.Playing,
            WwiseIds.States.Game_State.LevelUp,
            WwiseIds.States.Game_State.Dead
        };

        /// <summary>RTPC 슬롯. 배열 인덱스가 곧 <see cref="RtpcIds"/>의 인덱스다.</summary>
        private enum Rtpc
        {
            EnemyCount,
            PlayerHealth,
            VolumeMusic,
            VolumeSfx,
            MusicPitch
        }

        private static readonly uint[] RtpcIds =
        {
            WwiseIds.Rtpc.Enemy_Count,
            WwiseIds.Rtpc.Player_Health,
            WwiseIds.Rtpc.Volume_Music,
            WwiseIds.Rtpc.Volume_SFX,
            WwiseIds.Rtpc.Music_Pitch
        };

        private static readonly float[] _rtpcValues = new float[RtpcIds.Length];
        private static readonly bool[] _rtpcPending = new bool[RtpcIds.Length];

        private static bool _musicWanted;
        private static bool _musicPosted;
        private static bool _statePending;
        private static GameAudioState _state = GameAudioState.Title;

        // ── 음악 ────────────────────────────────────────────────────────────────────────

        /// <summary>음악을 요청한다. 사운드 엔진이 아직 없으면 준비되는 프레임에 대신 나간다.</summary>
        public static void PlayMusic()
        {
            _musicWanted = true;
            if (_musicPosted) return;
            if (!TryPostEvent(EventPlayMusic)) return;
            _musicPosted = true;
        }

        /// <summary>음악을 멈춘다. BGM은 씬을 넘겨 사는 것이 설계라 지금은 호출 지점이 없다.</summary>
        public static void StopMusic()
        {
            _musicWanted = false;
            if (!_musicPosted) return;
            if (!TryPostEvent(EventStopMusic)) return;
            _musicPosted = false;
        }

        // ── 효과음 ──────────────────────────────────────────────────────────────────────

        /// <summary>경험치 구슬 획득.</summary>
        public static void PlayXpPickup() => TryPostEvent(EventPlayXpPickup);

        /// <summary>골드·자석·회복 등 그 밖의 획득물.</summary>
        public static void PlayObjectPickup() => TryPostEvent(EventPlayObjectPickup);

        // ── 사실 보고 (RTPC) ────────────────────────────────────────────────────────────

        /// <summary>지금 살아 있는 적의 수. 이 수를 무엇으로 들려줄지는 Wwise의 블렌드 곡선이 정한다.</summary>
        public static void SetEnemyCount(int count) => SetRtpc(Rtpc.EnemyCount, count);

        /// <summary>
        /// 체력. 최대치가 런타임에 늘어나므로 백분율은 매번 두 인자로 계산한다 —
        /// 남은 체력만 보내면 최대 체력을 올린 순간 값이 거짓이 된다.
        /// </summary>
        public static void SetPlayerHealth(int current, int max)
        {
            var percent = max > 0 ? 100f * current / max : 0f;
            SetRtpc(Rtpc.PlayerHealth, Mathf.Clamp(percent, 0f, 100f));
        }

        /// <summary>0-1로 들어와 Wwise의 0-100으로 나간다. 단위 변환은 Wwise 쪽 사정이라 여기서 흡수한다.</summary>
        public static void SetMusicVolume(float volume01) =>
            SetRtpc(Rtpc.VolumeMusic, Mathf.Clamp01(volume01) * 100f);

        /// <inheritdoc cref="SetMusicVolume"/>
        public static void SetSfxVolume(float volume01) =>
            SetRtpc(Rtpc.VolumeSfx, Mathf.Clamp01(volume01) * 100f);

        /// <summary>음악 재생 속도. 사망 슬로우모션이 세계를 늦출 때 같이 늘어진다.</summary>
        public static void SetMusicPitch(float pitch) =>
            SetRtpc(Rtpc.MusicPitch, Mathf.Clamp(pitch, 0.1f, 3f));

        // ── 사실 보고 (State) ───────────────────────────────────────────────────────────

        /// <summary>
        /// 게임이 어느 국면인지 알린다. 무엇을 덕킹하고 무엇을 멈출지는 Wwise가 정한다.
        ///
        /// 지금 Wwise에 걸린 것은 <c>LevelUp</c>에서 Music −8dB, SFX −4dB다. 그 값을 여기 적지
        /// 않는 것이 요점이다 — 덕킹이 과하면 Wwise에서 숫자만 바꾸면 되고 이 파일은 그대로다.
        /// </summary>
        public static void SetGameState(GameAudioState state)
        {
            _state = state;
            _statePending = true;
            FlushState();
        }

        // ── 지연 플러시 ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 사운드 엔진이 준비되기 전에 들어온 보고를 내보낸다. <see cref="WwiseGameSync"/>가 매 프레임 부른다.
        ///
        /// <c>RuntimeInitializeOnLoadMethod</c>끼리는 순서가 보장되지 않아, 게임 쪽 설치가 Wwise 초기화보다
        /// 먼저 도는 실행이 존재한다. 그때 들어온 값을 버리면 그 판 내내 볼륨이 기본값으로 남는다.
        ///
        /// WebGL에서는 <see cref="IsReady"/>가 항상 false라 첫 줄에서 끝난다 — 매 프레임 도는 경로이므로
        /// 무음 빌드에서 루프가 헛돌지 않게 하는 것이 중요하다.
        /// </summary>
        public static void Flush()
        {
            if (!IsReady) return;

            if (_musicWanted && !_musicPosted && TryPostEvent(EventPlayMusic))
            {
                _musicPosted = true;
            }

            for (var i = 0; i < _rtpcValues.Length; i++)
            {
                if (!_rtpcPending[i]) continue;
                if (!TryApplyRtpc(i, _rtpcValues[i])) continue;
                _rtpcPending[i] = false;
            }

            FlushState();
        }

        // ── 내부: 상태 관리 (플랫폼 공통) ───────────────────────────────────────────────

        private static void SetRtpc(Rtpc rtpc, float value)
        {
            var index = (int)rtpc;
            _rtpcValues[index] = value;

            if (!TryApplyRtpc(index, value))
            {
                // 마지막 값만 들고 있다가 준비된 프레임에 한 번 민다. 매 프레임 들어오는 값이라
                // 큐에 쌓아 두는 것은 의미가 없고, 최신값 하나면 충분하다.
                _rtpcPending[index] = true;
                return;
            }

            _rtpcPending[index] = false;
        }

        private static void FlushState()
        {
            if (!_statePending) return;
            if (!TryApplyState(_state)) return;
            _statePending = false;
        }

#if UNITY_WEBGL && !UNITY_EDITOR

        // ── 백엔드: WebGL (무음) ────────────────────────────────────────────────────────
        //
        // WebGL 플레이어 빌드에는 AK 런타임 어셈블리 4종이 들어가지 않는다(docs/webgl-build.md §2-1).
        // 따라서 이 블록 안에는 Ak* 로 시작하는 식별자가 하나도 없어야 한다.
        //
        // 값은 위쪽 공통 층이 계속 들고 있다. 소리만 안 날 뿐 게임 상태 추적은 그대로 돌고,
        // 나중에 Wwise WebGL 플랫폼을 깔거나 다른 백엔드를 붙일 때 이 네 개만 채우면 된다.

        private static bool IsReady => false;

        private static bool TryPostEvent(uint eventId) => false;

        private static bool TryApplyRtpc(int index, float value) => false;

        private static bool TryApplyState(GameAudioState state) => false;

#else

        // ── 백엔드: Wwise ───────────────────────────────────────────────────────────────

        private const string HostName = "AudioDirector (Runtime)";

        private static GameObject _host;
        private static bool _banksLoaded;
        private static bool _bankLoadFailed;

        /// <summary>
        /// 사운드 엔진이 살아 있고 이벤트를 걸 게임 오브젝트가 준비되었는가.
        ///
        /// 뱅크 로드는 여기 넣지 않는다. RTPC와 State는 Init 뱅크만 있으면 나가므로, 사용자 뱅크가
        /// 아직(또는 끝내) 없더라도 볼륨 설정 같은 것은 정상으로 흘러가야 한다.
        /// </summary>
        private static bool IsReady
        {
            get
            {
                if (!AkUnitySoundEngine.IsInitialized()) return false;
                EnsureHost();
                return _host != null;
            }
        }

        /// <summary>
        /// 이벤트가 든 사용자 뱅크를 올린다. <c>AkInitializer</c>가 자동으로 올려주는 것은 Init 뱅크뿐이고,
        /// 이벤트·구조·미디어가 든 뱅크는 누군가 명시적으로 올려야 한다. 씬에 <c>AkBank</c> 컴포넌트를
        /// 두는 방법도 있지만, 그러면 뱅크 로드가 씬 설정에 흩어져 이 클래스만 봐서는 알 수 없게 된다.
        ///
        /// 실패하면 한 번만 기록하고 다시 시도하지 않는다. 프레임마다 재시도하면 같은 에러가 콘솔을
        /// 채워 진짜 원인을 덮는다 — 실제로 겪은 실패 방식이다.
        /// </summary>
        private static bool EnsureBanks()
        {
            if (_banksLoaded) return true;
            if (_bankLoadFailed) return false;

            var result = AkUnitySoundEngine.LoadBank(WwiseIds.Banks.Main, out _);
            if (result == AKRESULT.AK_Success)
            {
                _banksLoaded = true;
                return true;
            }

            _bankLoadFailed = true;
            Debug.LogError(
                $"[Audio] 사운드뱅크 '{WwiseIds.Banks.Main}' 로드 실패({result}). " +
                "Swarm → Audio → Rebuild 로 StreamingAssets에 뱅크가 복사됐는지 확인할 것.");
            return false;
        }

        /// <summary>
        /// 이벤트를 posting 할 게임 오브젝트. Wwise 이벤트는 반드시 등록된 오브젝트 위에서 재생되므로
        /// 씬을 넘겨 사는 것 하나를 만들어 두고 전부 여기에 건다 — 소리가 전부 2D라 위치는 의미가 없다.
        /// </summary>
        private static void EnsureHost()
        {
            if (_host != null) return;

            _host = new GameObject(HostName);
            Object.DontDestroyOnLoad(_host);
            AkUnitySoundEngine.RegisterGameObj(_host, HostName);
        }

        private static bool TryPostEvent(uint eventId)
        {
            if (!IsReady || !EnsureBanks()) return false;
            return AkUnitySoundEngine.PostEvent(eventId, _host) != AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
        }

        private static bool TryApplyRtpc(int index, float value)
        {
            if (!IsReady) return false;
            return AkUnitySoundEngine.SetRTPCValue(RtpcIds[index], value) == AKRESULT.AK_Success;
        }

        private static bool TryApplyState(GameAudioState state)
        {
            if (!IsReady) return false;
            return AkUnitySoundEngine.SetState(WwiseIds.States.Game_State.Group, StateIds[(int)state]) == AKRESULT.AK_Success;
        }

#endif
    }
}
