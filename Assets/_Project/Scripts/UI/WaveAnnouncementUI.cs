using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    public class WaveAnnouncementUI : MonoBehaviour
    {
        [SerializeField] private Text announcementText;
        [SerializeField] private float displayDuration = 2f;
        [SerializeField] private float fadeDuration = 0.5f;

        private Coroutine _routine;

        private void Awake()
        {
            if (announcementText != null) announcementText.canvasRenderer.SetAlpha(0f);
        }

        public void Show(string message)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(message));
        }

        private IEnumerator ShowRoutine(string message)
        {
            announcementText.text = message;
            announcementText.canvasRenderer.SetAlpha(1f);

            yield return new WaitForSeconds(displayDuration);

            announcementText.CrossFadeAlpha(0f, fadeDuration, false);
        }
    }
}
