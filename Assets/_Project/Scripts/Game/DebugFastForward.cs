#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;

namespace Swarm.Game
{
    public class DebugFastForward : MonoBehaviour
    {
        [SerializeField] private float fastForwardScale = 10f;

        private void Update()
        {
            if (Time.timeScale == 0f) return;
            if (Keyboard.current == null) return;

            Time.timeScale = Keyboard.current.f1Key.isPressed ? fastForwardScale : 1f;
        }

        private void OnDestroy()
        {
            if (Time.timeScale != 0f) Time.timeScale = 1f;
        }
    }
}
#endif
