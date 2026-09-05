using Swarm.Player;
using Swarm.Spawner;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Swarm.Audio
{
    /// <summary>
    /// 게임의 사실을 <see cref="AudioDirector"/>로 흘려보내는 곳. 여기서 하는 일은 두 가지뿐이다 —
    /// 살아 있는 적의 수를 주기적으로 세어 보고하고, 체력 변화를 구독해 그때그때 보고한다.
    ///
    /// <see cref="BackgroundMusic"/>·<see cref="SoundEffects"/>와 같은 방식으로 스스로 만들어지고 씬 로드를
    /// 넘겨 산다. 씬이나 프리팹에 붙이지 않는 이유는 붙일 곳이 마땅치 않아서가 아니라, 오디오 배선 하나를
    /// 바꾸려고 세 개의 씬을 열어 저장해야 하는 상황을 만들지 않기 위해서다.
    ///
    /// 소리에 대한 판단은 한 줄도 없다. "적이 몇 마리인가"까지가 이 파일의 몫이다.
    /// </summary>
    public class WwiseGameSync : MonoBehaviour
    {
        private const string ObjectName = "WwiseGameSync (Runtime)";

        /// <summary>적 수를 다시 세는 간격(초). 스포너의 정리 주기가 0.5초라 그보다 촘촘히 볼 이유가 없다.</summary>
        private const float EnemyCountInterval = 0.25f;

        /// <summary>씬 안에서 대상을 다시 찾아보는 간격(초). 찾기는 비싸고, 늦게 잡혀도 문제가 없다.</summary>
        private const float SearchInterval = 0.5f;

        private static WwiseGameSync _instance;

        private EnemySpawner _spawner;
        private PlayerHealth _health;
        private float _nextPollAt;
        private float _nextSearchAt;
        private int _lastReportedCount = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<WwiseGameSync>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Unsubscribe();
        }

        private void Update()
        {
            // 사운드 엔진이 늦게 뜨는 실행에서, 그 사이에 들어와 있던 보고를 내보낸다.
            AudioDirector.Flush();

            AcquireReferences();
            PollEnemyCount();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 씬이 바뀌면 이전 씬의 인스턴스는 죽는다. 참조를 놓고 다시 찾는다.
            Unsubscribe();
            _spawner = null;
            _nextSearchAt = 0f;

            // 타이틀로 돌아왔는데 마지막 전투의 적 수가 남아 있으면 음악이 그 판을 기억한 채로 시작한다.
            _lastReportedCount = 0;
            AudioDirector.SetEnemyCount(0);
        }

        private void AcquireReferences()
        {
            // 시간은 unscaled로 잰다. 레벨업 카드가 열려 있는 동안 Time.timeScale이 0이고,
            // 그 사이에도 적은 그대로 서 있으므로 보고는 계속되어야 한다.
            if (Time.unscaledTime < _nextSearchAt) return;
            if (_spawner != null && _health != null) return;

            _nextSearchAt = Time.unscaledTime + SearchInterval;

            if (_spawner == null)
            {
                _spawner = FindAnyObjectByType<EnemySpawner>();
            }

            if (_health != null) return;

            _health = FindAnyObjectByType<PlayerHealth>();
            if (_health == null) return;

            _health.OnHealthChanged += HandleHealthChanged;

            // OnHealthChanged는 체력이 변할 때만 울린다. 만피로 시작한 판은 첫 피격 전까지
            // RTPC가 이전 판의 값(사망 직전의 낮은 체력)으로 남아 있게 되므로 여기서 한 번 민다.
            HandleHealthChanged(_health.CurrentHealth, _health.MaxHealth);
        }

        private void Unsubscribe()
        {
            if (_health == null) return;

            _health.OnHealthChanged -= HandleHealthChanged;
            _health = null;
        }

        private void HandleHealthChanged(int current, int max) => AudioDirector.SetPlayerHealth(current, max);

        private void PollEnemyCount()
        {
            if (_spawner == null) return;
            if (Time.unscaledTime < _nextPollAt) return;

            _nextPollAt = Time.unscaledTime + EnemyCountInterval;

            // 스포너에는 변경 이벤트가 없어 세어보는 수밖에 없다. 값이 그대로면 보내지 않는다 —
            // 같은 값을 매번 다시 밀어도 결과는 같고, 프로파일러만 지저분해진다.
            var count = _spawner.ActiveEnemyCount;
            if (count == _lastReportedCount) return;

            _lastReportedCount = count;
            AudioDirector.SetEnemyCount(count);
        }
    }
}
