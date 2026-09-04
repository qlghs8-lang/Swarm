using System.Collections;
using System.Collections.Generic;
using Swarm.Audio;
using Swarm.Enemy;
using Swarm.Game;
using Swarm.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    /// <summary>
    /// The end-of-run presentation: everything between the last hit and the result panel being
    /// ready for input.
    ///
    /// Before this, a run ended on a single frame — timeScale to zero and the panel popped on. The
    /// two endings deserve opposite shapes, and both are built here out of the same parts:
    ///
    ///   Game over — hit stop, then the world slows to a crawl and closes in on the body while the
    ///   screen bleeds dark. Time stops *after* the player has watched themselves die, not instead
    ///   of it.
    ///
    ///   Clear — the same hit stop, then time is handed *back*: the camera steps out, the last
    ///   enemies burn off, every dropped pickup flies in, and only then does the panel arrive.
    ///
    /// It attaches itself (GameManager adds it if the scene has none) and borrows the panel and
    /// text objects that are already wired there, so none of this needs new inspector fields.
    /// Every wait is unscaled — the sequence spends most of its length at timeScale near zero.
    /// </summary>
    public class ResultSequence : MonoBehaviour
    {
        [Header("Game over")]
        [SerializeField] private float deathHitStop = 0.1f;
        [SerializeField] private float deathSlowMoDuration = 1f;
        [SerializeField] private float deathSlowMoScale = 0.12f;
        [SerializeField] private float deathZoom = 4.4f;
        [SerializeField] private Color deathTint = new(0.16f, 0f, 0.03f, 0.5f);
        [SerializeField] private Color deathTitleColor = new(0.93f, 0.24f, 0.22f);
        [SerializeField] private Color deathCorpseTint = new(0.35f, 0.16f, 0.16f, 1f);

        [Header("Clear")]
        [SerializeField] private float clearHitStop = 0.18f;
        [SerializeField] private float clearRecoverDuration = 1.1f;
        [SerializeField] private float clearSlowMoScale = 0.15f;
        [SerializeField] private float clearZoom = 7.4f;
        [SerializeField] private float clearCelebrationDuration = 1.1f;
        [SerializeField] private Color clearTint = new(0.09f, 0.07f, 0.02f, 0.45f);
        [SerializeField] private Color clearTitleColor = new(1f, 0.85f, 0.36f);

        [Header("Panel")]
        [SerializeField] private float hudFadeDuration = 0.45f;
        [SerializeField] private float titlePopDuration = 0.26f;
        [SerializeField] private float lineInterval = 0.12f;
        [SerializeField] private float countUpDuration = 0.4f;

        [Header("Panel layout")]
        [Tooltip("결과 항목이 4~5줄로 늘어나면서 기존 배치로는 잘리기 때문에, 패널을 열 때 한 번만 자리를 잡아준다. 씬에서 직접 배치를 조정했다면 꺼도 된다.")]
        [SerializeField] private bool adjustLayout = true;
        [SerializeField] private float titleY = 170f;
        [SerializeField] private float bodyY = 40f;
        [SerializeField] private Vector2 bodySize = new(600f, 170f);
        [SerializeField] private int bodyFontSize = 24;
        [SerializeField] private float bodyLineSpacing = 1.25f;

        private float _baseFixedDelta;
        private CameraJuice _camera;
        private ScreenOverlay _tint;
        private ScreenOverlay _flash;
        private int _levelAtRunEnd = 1;

        /// <param name="goldAtRunStart">Balance before the run, so the panel can report what this
        /// run earned rather than the lifetime total. Read at reveal time, not now: on a clear the
        /// pickup sweep is still paying out while the celebration plays.</param>
        public void Play(bool cleared, float survivedSeconds, int goldAtRunStart, int clearBonus,
                         GameObject panel, Text titleText, Text bodyText)
        {
            StartCoroutine(Run(cleared, survivedSeconds, goldAtRunStart, clearBonus, panel, titleText, bodyText));
        }

        private IEnumerator Run(bool cleared, float survivedSeconds, int goldAtRunStart, int clearBonus,
                                GameObject panel, Text titleText, Text bodyText)
        {
            _baseFixedDelta = Time.fixedDeltaTime;
            // Snapshot now, not at reveal: the clear sweep pours the whole field of orbs into the
            // player and would otherwise credit the run with levels it never fought for.
            _levelAtRunEnd = ReadPlayerLevel();
            _camera = CameraJuice.Acquire();
            _tint = ScreenOverlay.Create("Result Tint (Runtime)", -1, Clear(cleared ? clearTint : deathTint));
            _flash = ScreenOverlay.Create("Result Flash (Runtime)", 100, Color.clear);

            if (panel != null) panel.SetActive(false);

            yield return cleared
                ? ClearIntro(panel)
                : DeathIntro(panel);

            yield return ShowPanel(cleared, survivedSeconds, goldAtRunStart, clearBonus, panel, titleText, bodyText);
        }

        // ---------------------------------------------------------------- game over

        private IEnumerator DeathIntro(GameObject panel)
        {
            var player = GameObject.FindGameObjectWithTag("Player");

            // 1. The hit lands. Everything stops for a tenth of a second — long enough to register
            //    as a blow rather than a dropped frame.
            SetTimeScale(0f);
            StartCoroutine(_flash.Flash(new Color(1f, 0.85f, 0.85f, 0.8f), 0.22f));
            yield return Wait(deathHitStop);

            // 2. Then time doesn't stop, it *drags*: the camera crawls in on the body, the screen
            //    bleeds dark, and the HUD leaves so the last thing on screen is the player.
            SetTimeScale(deathSlowMoScale);
            BackgroundMusic.SetPitch(0.6f);

            if (_camera != null)
            {
                StartCoroutine(_camera.Shake(0.35f, 0.3f));
                var focus = player != null ? player.transform.position : (Vector3?)null;
                StartCoroutine(_camera.MoveTo(focus, deathZoom, deathSlowMoDuration));
            }

            StartCoroutine(_tint.FadeTo(deathTint, deathSlowMoDuration));
            StartCoroutine(FadeHud(panel, hudFadeDuration));
            StartCoroutine(TintSprites(player, deathCorpseTint, deathSlowMoDuration * 0.7f));
            StartCoroutine(RampTimeScale(deathSlowMoScale, 0.02f, deathSlowMoDuration));

            yield return Wait(deathSlowMoDuration);

            // 3. Only now does the world actually stop.
            SetTimeScale(0f);
        }

        // ---------------------------------------------------------------- clear

        private IEnumerator ClearIntro(GameObject panel)
        {
            var player = GameObject.FindGameObjectWithTag("Player");

            // Time runs again for the celebration, so the stragglers still on screen could kill a
            // player who has already won. They get the length of the sequence for free.
            if (player != null && player.TryGetComponent<PlayerHealth>(out var health))
            {
                health.SetInvincible(30f);
            }

            SetTimeScale(0f);
            StartCoroutine(_flash.Flash(Color.white, 0.4f));
            yield return Wait(clearHitStop);

            // Time is given back rather than taken away: the boss dying is the moment the pressure
            // lifts, so the camera opens out and the clock winds up to normal.
            SetTimeScale(clearSlowMoScale);
            BackgroundMusic.SetPitch(1f);

            if (_camera != null)
            {
                StartCoroutine(_camera.Shake(0.5f, 0.45f));
                StartCoroutine(_camera.MoveTo(null, clearZoom, clearRecoverDuration));
            }

            StartCoroutine(RampTimeScale(clearSlowMoScale, 1f, clearRecoverDuration));

            // The arena empties out: whatever was still chasing burns off in a wave, and every orb
            // and coin the player fought over comes to them. That sweep is the reward, and it only
            // reads if the panel waits for it.
            StartCoroutine(SweepEnemies());
            MagnetAttractable.AttractAll();

            yield return Wait(clearCelebrationDuration);

            StartCoroutine(FadeHud(panel, hudFadeDuration));
            StartCoroutine(_tint.FadeTo(clearTint, 0.5f));
            yield return Wait(0.35f);

            SetTimeScale(0f);
        }

        private IEnumerator SweepEnemies()
        {
            var enemies = FindObjectsByType<EnemyHealth>();
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                StartCoroutine(BurnOff(enemy.gameObject));
                yield return Wait(0.025f);
            }
        }

        private IEnumerator BurnOff(GameObject enemy)
        {
            // Deactivated rather than damaged: routing these through TakeDamage would pay out
            // experience and gold after the numbers on the panel were already counted, and would
            // credit the player with kills they did not make.
            foreach (var behaviour in enemy.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            foreach (var collider in enemy.GetComponentsInChildren<Collider2D>()) collider.enabled = false;

            var renderers = enemy.GetComponentsInChildren<SpriteRenderer>();
            var startScale = enemy.transform.localScale;
            var elapsed = 0f;
            const float duration = 0.25f;

            while (elapsed < duration && enemy != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(elapsed / duration);
                foreach (var renderer in renderers)
                {
                    if (renderer != null) renderer.color = new Color(1f, 1f, 1f, 1f - k);
                }

                enemy.transform.localScale = startScale * Mathf.Lerp(1f, 1.3f, k);
                yield return null;
            }

            if (enemy != null)
            {
                enemy.transform.localScale = startScale;
                enemy.SetActive(false);
            }
        }

        // ---------------------------------------------------------------- panel

        private IEnumerator ShowPanel(bool cleared, float survivedSeconds, int goldAtRunStart, int clearBonus,
                                      GameObject panel, Text titleText, Text bodyText)
        {
            if (panel == null) yield break;

            var group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            // Locked until the numbers have finished: a player mashing through the death screen
            // should not be able to restart before they have seen what the run was worth.
            group.interactable = false;
            group.blocksRaycasts = false;

            panel.SetActive(true);

            if (adjustLayout) ApplyLayout(titleText, bodyText);

            if (titleText != null)
            {
                titleText.text = cleared ? "CLEAR!" : "GAME OVER";
                titleText.color = cleared ? clearTitleColor : deathTitleColor;
            }

            if (bodyText != null) bodyText.text = "";

            StartCoroutine(FadeGroup(group, 1f, 0.16f));

            if (titleText != null)
            {
                // Game over drops in from oversized and overshoots on landing; clear springs up
                // from nothing. Same code, opposite direction.
                yield return cleared
                    ? PopText(titleText, 0.25f, 1f, titlePopDuration)
                    : PopText(titleText, 2.8f, 1f, titlePopDuration);
                StartCoroutine(_flash.Flash(new Color(1f, 1f, 1f, cleared ? 0.35f : 0.2f), 0.25f));
            }

            yield return Wait(0.15f);

            if (bodyText != null)
            {
                // Gold, unlike the level, is read now: coins swept off the ground were dropped by
                // enemies the player actually killed, so they belong to the run.
                var goldEarned = Mathf.Max(0, GoldWallet.Current - goldAtRunStart);

                var lines = new List<StatLine>
                {
                    StatLine.Time("생존 시간", survivedSeconds),
                    StatLine.Number("도달 레벨", _levelAtRunEnd, " Lv"),
                    StatLine.Number("처치", RunStats.Kills, " 마리"),
                    StatLine.Number("획득 골드", goldEarned, " G"),
                };

                if (clearBonus > 0) lines.Add(StatLine.Number("클리어 보너스", clearBonus, " G", "+"));

                yield return RevealLines(bodyText, lines);
            }

            group.interactable = true;
            group.blocksRaycasts = true;
        }

        /// <summary>One row of the result readout, revealed by counting its value up from zero.</summary>
        private readonly struct StatLine
        {
            private readonly string _label;
            private readonly string _suffix;
            private readonly string _prefix;
            private readonly float _value;
            private readonly bool _isTime;

            private StatLine(string label, float value, string suffix, string prefix, bool isTime)
            {
                _label = label;
                _value = value;
                _suffix = suffix;
                _prefix = prefix;
                _isTime = isTime;
            }

            public static StatLine Time(string label, float seconds) => new(label, seconds, "", "", true);

            public static StatLine Number(string label, int value, string suffix, string prefix = "")
                => new(label, value, suffix, prefix, false);

            public string Render(float progress, bool highlight)
            {
                var shown = _value * progress;
                var text = _isTime ? FormatTime(shown) : $"{_prefix}{Mathf.RoundToInt(shown)}{_suffix}";
                var row = $"{_label}   {text}";
                return highlight ? $"<color=#FFD86B>{row}</color>" : row;
            }
        }

        private IEnumerator RevealLines(Text target, List<StatLine> lines)
        {
            var settled = new List<string>();

            foreach (var line in lines)
            {
                var elapsed = 0f;
                while (elapsed < countUpDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var k = Mathf.Clamp01(elapsed / countUpDuration);
                    // Ease out, so the number lands on its final value instead of ticking past it.
                    k = 1f - Mathf.Pow(1f - k, 3f);
                    target.text = string.Join("\n", settled) +
                                  (settled.Count > 0 ? "\n" : "") + line.Render(k, true);
                    yield return null;
                }

                settled.Add(line.Render(1f, false));
                target.text = string.Join("\n", settled);
                yield return Wait(lineInterval);
            }
        }

        /// <summary>
        /// The panel was laid out for a single line of text. Four or five stat rows do not fit
        /// between the title and the restart button, and UI.Text truncates rather than spilling,
        /// so the rows would simply vanish. Nudged here instead of in the scene so the existing
        /// prefab keeps working and every number stays tunable in the inspector.
        /// </summary>
        private void ApplyLayout(Text titleText, Text bodyText)
        {
            if (titleText != null)
            {
                var rect = titleText.rectTransform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, titleY);
            }

            if (bodyText == null) return;

            bodyText.rectTransform.anchoredPosition = new Vector2(bodyText.rectTransform.anchoredPosition.x, bodyY);
            bodyText.rectTransform.sizeDelta = bodySize;
            bodyText.fontSize = bodyFontSize;
            bodyText.lineSpacing = bodyLineSpacing;
            bodyText.supportRichText = true;
            bodyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        // ---------------------------------------------------------------- shared parts

        private IEnumerator PopText(Text text, float from, float to, float duration)
        {
            var rect = text.rectTransform;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(elapsed / duration);
                // Back-ease: overshoots ~10% and settles, which is what makes it land.
                var eased = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, eased);
                yield return null;
            }

            rect.localScale = Vector3.one * to;
        }

        private IEnumerator FadeGroup(CanvasGroup group, float target, float duration)
        {
            var from = group.alpha;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = target;
        }

        /// <summary>Fades every HUD element except the result panel's own branch.</summary>
        private IEnumerator FadeHud(GameObject panel, float duration)
        {
            if (panel == null) yield break;

            var canvas = panel.GetComponentInParent<Canvas>();
            if (canvas == null) yield break;

            var groups = new List<CanvasGroup>();
            foreach (Transform child in canvas.transform)
            {
                if (child == panel.transform || !child.gameObject.activeSelf) continue;

                var group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();
                groups.Add(group);
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var alpha = 1f - Mathf.Clamp01(elapsed / duration);
                foreach (var group in groups) group.alpha = alpha;
                yield return null;
            }

            foreach (var group in groups)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        private IEnumerator TintSprites(GameObject root, Color target, float duration)
        {
            if (root == null) yield break;

            var renderers = root.GetComponentsInChildren<SpriteRenderer>();
            var from = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++) from[i] = renderers[i].color;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(elapsed / duration);
                for (var i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null) renderers[i].color = Color.Lerp(from[i], target, k);
                }

                yield return null;
            }
        }

        private IEnumerator RampTimeScale(float from, float to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(elapsed / duration);
                SetTimeScale(Mathf.Lerp(from, to, k * k));
                yield return null;
            }

            SetTimeScale(to);
        }

        private void SetTimeScale(float scale)
        {
            Time.timeScale = scale;
            // Physics steps have to follow the clock, or slow motion turns into a slideshow of
            // enemy positions instead of smooth movement.
            if (scale > 0f) Time.fixedDeltaTime = _baseFixedDelta * scale;
        }

        private static IEnumerator Wait(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static int ReadPlayerLevel()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            return player != null && player.TryGetComponent<PlayerExperience>(out var experience)
                ? experience.Level
                : 1;
        }

        private static Color Clear(Color color)
        {
            color.a = 0f;
            return color;
        }

        private static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var minutes = Mathf.FloorToInt(seconds / 60f);
            var secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{secs:00}";
        }

        /// <summary>Restores everything the sequence took over, for Restart / GoToTitle.</summary>
        public void Cleanup()
        {
            StopAllCoroutines();
            if (_baseFixedDelta > 0f) Time.fixedDeltaTime = _baseFixedDelta;
            BackgroundMusic.SetPitch(1f);
            _camera?.Release();
            _tint?.Destroy();
            _flash?.Destroy();
        }
    }
}
