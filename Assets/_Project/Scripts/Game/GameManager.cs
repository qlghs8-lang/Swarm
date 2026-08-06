using Swarm.Enemy;
using Swarm.Player;
using Swarm.Spawner;
using Swarm.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Swarm.Game
{
    public class GameManager : MonoBehaviour
    {
        private const string SelectedCharacterKey = "Swarm_SelectedCharacterId";

        [SerializeField] private Text timerText;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Text resultTitleText;
        [SerializeField] private Text resultTimeText;
        [SerializeField] private CharacterDefinition[] characters;
        [SerializeField] private PassiveUpgrade[] passiveUpgrades;
        [SerializeField] private string titleSceneName = "Title";
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField] private BossHealthBarUI bossHealthBarUI;
        [SerializeField] private WaveAnnouncementUI waveAnnouncementUI;

        private float _elapsedTime;
        private bool _isGameEnded;
        private PlayerHealth _playerHealth;
        private EnemyHealth _boss;

        private void Awake()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.TryGetComponent(out _playerHealth))
            {
                _playerHealth.OnDied += HandleGameOver;
            }

            if (enemySpawner != null)
            {
                enemySpawner.OnBossSpawned += HandleBossSpawned;
                enemySpawner.OnWaveTriggered += HandleWaveTriggered;
            }
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            string selectedId = PlayerPrefs.GetString(SelectedCharacterKey, "");
            CharacterDefinition character = System.Array.Find(characters, c => c.Id == selectedId) ??
                                             (characters.Length > 0 ? characters[0] : null);
            character?.ApplyToPlayer(player);

            foreach (var upgrade in passiveUpgrades)
            {
                if (upgrade != null)
                {
                    upgrade.ApplyToPlayer(player);
                }
            }
        }

        private void OnDestroy()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnDied -= HandleGameOver;
            }

            if (enemySpawner != null)
            {
                enemySpawner.OnBossSpawned -= HandleBossSpawned;
                enemySpawner.OnWaveTriggered -= HandleWaveTriggered;
            }

            if (_boss != null)
            {
                _boss.OnDied -= HandleClear;
            }
        }

        private void Update()
        {
            if (_isGameEnded) return;

            _elapsedTime += Time.deltaTime;
            timerText.text = FormatTime(_elapsedTime);
        }

        private void HandleBossSpawned(EnemyHealth boss)
        {
            _boss = boss;
            bossHealthBarUI?.Bind(boss);
            boss.OnDied += HandleClear;
            waveAnnouncementUI?.Show("BOSS");
        }

        private void HandleWaveTriggered(int waveNumber)
        {
            waveAnnouncementUI?.Show($"WAVE {waveNumber}");
        }

        private void HandleGameOver()
        {
            ShowResult("GAME OVER");
        }

        private void HandleClear()
        {
            ShowResult("CLEAR!");
        }

        private void ShowResult(string title)
        {
            if (_isGameEnded) return;
            _isGameEnded = true;

            resultTitleText.text = title;
            resultTimeText.text = $"생존 시간: {FormatTime(_elapsedTime)}";
            resultPanel.SetActive(true);
            Time.timeScale = 0f;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void GoToTitle()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(titleSceneName);
        }

        private static string FormatTime(float seconds)
        {
            var minutes = Mathf.FloorToInt(seconds / 60f);
            var secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{secs:00}";
        }
    }
}
