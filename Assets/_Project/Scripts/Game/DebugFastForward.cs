using UnityEngine;
using UnityEngine.InputSystem;

namespace Swarm.Game
{
    // This component sits on the GameManager object in Game.unity, the shipping scene. It used to
    // be wrapped in #if UNITY_EDITOR, which compiled the class out of player builds and left the
    // scene holding a "missing script" reference. It now compiles everywhere and turns itself off
    // outside the editor and development builds instead, so release players get no F1 fast-forward.
    public class DebugFastForward : MonoBehaviour
    {
        [SerializeField] private float fastForwardScale = 10f;

        private void Awake()
        {
            if (!Application.isEditor && !Debug.isDebugBuild)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            if (Time.timeScale == 0f) return;
            if (Keyboard.current == null) return;

            Time.timeScale = Keyboard.current.f1Key.isPressed ? fastForwardScale : 1f;
        }

        private void OnDestroy()
        {
            if (!enabled) return;
            if (Time.timeScale != 0f) Time.timeScale = 1f;
        }
    }
}
