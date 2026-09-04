using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace Swarm.Game
{
    /// <summary>
    /// Takes manual control of the main camera for the end-of-run sequence.
    ///
    /// Cinemachine's brain is switched off rather than driven through it: the run is over, nothing
    /// else wants the camera any more, and the follow damping on the virtual camera would smear
    /// exactly the snap that makes a shake read as impact. Everything here runs on unscaled time,
    /// because the sequence is mostly played at a timeScale near zero.
    /// </summary>
    public sealed class CameraJuice
    {
        private readonly Camera _camera;
        private readonly Behaviour _brain;
        private readonly float _baseSize;

        private Vector3 _anchor;
        private Vector3 _shakeOffset;

        private CameraJuice(Camera camera, Behaviour brain)
        {
            _camera = camera;
            _brain = brain;
            _baseSize = camera.orthographicSize;
            _anchor = camera.transform.position;
        }

        public float BaseSize => _baseSize;

        /// <summary>Null when there is no main camera to drive — every call then no-ops.</summary>
        public static CameraJuice Acquire()
        {
            var camera = Camera.main;
            if (camera == null) return null;

            var brain = camera.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = false;

            return new CameraJuice(camera, brain);
        }

        public void Release()
        {
            if (_camera != null)
            {
                _camera.orthographicSize = _baseSize;
                _camera.transform.position = _anchor;
            }

            if (_brain != null) _brain.enabled = true;
        }

        /// <summary>
        /// Pushes toward <paramref name="target"/> and to <paramref name="size"/> at once — the
        /// slow crawl in on the body, or the step back that opens the arena up on a clear.
        /// </summary>
        public IEnumerator MoveTo(Vector3? target, float size, float duration)
        {
            if (_camera == null) yield break;

            var fromPosition = _camera.transform.position;
            var toPosition = target.HasValue
                ? new Vector3(target.Value.x, target.Value.y, fromPosition.z)
                : fromPosition;
            var fromSize = _camera.orthographicSize;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(elapsed / duration);
                // Ease out: the move should arrive settling, not braking.
                k = 1f - Mathf.Pow(1f - k, 3f);
                _anchor = Vector3.Lerp(fromPosition, toPosition, k);
                _camera.orthographicSize = Mathf.Lerp(fromSize, size, k);
                _camera.transform.position = _anchor + _shakeOffset;
                yield return null;
            }

            _anchor = toPosition;
            _camera.orthographicSize = size;
            _camera.transform.position = _anchor + _shakeOffset;
        }

        /// <summary>Decaying random offset. Run it alongside a move — the two compose.</summary>
        public IEnumerator Shake(float amplitude, float duration)
        {
            if (_camera == null) yield break;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var falloff = 1f - Mathf.Clamp01(elapsed / duration);
                var strength = amplitude * falloff * falloff;
                _shakeOffset = new Vector3(Random.Range(-strength, strength),
                                           Random.Range(-strength, strength), 0f);
                _camera.transform.position = _anchor + _shakeOffset;
                yield return null;
            }

            _shakeOffset = Vector3.zero;
            _camera.transform.position = _anchor;
        }
    }
}
