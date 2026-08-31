#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Swarm.Player;
using Swarm.Spawner;
using UnityEngine;

namespace Swarm.Game
{
    /// <summary>
    /// TEST ONLY — draws elapsed time, live enemy count, player level and FPS in the corner while
    /// balancing. The enemy count is the one to watch: it should settle at or below the spawner's
    /// Max Active Enemies rather than climbing without bound.
    ///
    /// Installs itself at runtime and nothing in any scene references it, so deleting this file
    /// removes it completely.
    /// </summary>
    public class DebugStatsOverlay : MonoBehaviour
    {
        private const string ObjectName = "DebugStatsOverlay (Runtime)";

        private EnemySpawner _spawner;
        private PlayerExperience _experience;
        private float _elapsed;
        private float _smoothedDeltaTime;
        private GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
            TryInstall();
        }

        private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                              UnityEngine.SceneManagement.LoadSceneMode mode)
            => TryInstall();

        private static void TryInstall()
        {
            // Only where enemies actually spawn — no point on the title screen.
            if (Object.FindAnyObjectByType<EnemySpawner>() == null) return;
            if (Object.FindAnyObjectByType<DebugStatsOverlay>() != null) return;

            new GameObject(ObjectName, typeof(DebugStatsOverlay));
        }

        private void Awake()
        {
            _spawner = Object.FindAnyObjectByType<EnemySpawner>();

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) player.TryGetComponent(out _experience);
        }

        private void Update()
        {
            // Unscaled so the readout keeps running while the level-up panel holds timeScale at 0,
            // and so the F1 fast-forward does not distort the FPS number.
            _elapsed += Time.unscaledDeltaTime;
            _smoothedDeltaTime = Mathf.Lerp(_smoothedDeltaTime, Time.unscaledDeltaTime, 0.1f);
        }

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white },
            };

            var fps = _smoothedDeltaTime > 0f ? 1f / _smoothedDeltaTime : 0f;
            var minutes = Mathf.FloorToInt(_elapsed / 60f);
            var seconds = Mathf.FloorToInt(_elapsed % 60f);
            var enemies = _spawner != null ? _spawner.ActiveEnemyCount : 0;
            var level = _experience != null ? _experience.Level : 0;

            var text = $"{minutes:00}:{seconds:00}   적 {enemies}   Lv.{level}   {fps:0} FPS";

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(8f, 8f, 290f, 26f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(16f, 11f, 300f, 22f), text, _style);
        }
    }
}
#endif
