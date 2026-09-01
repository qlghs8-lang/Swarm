using System.Collections;
using UnityEngine;

namespace Swarm.Weapon
{
    /// <summary>
    /// A one-shot sprite animation played at a point in the world — hit flashes, slashes, impacts.
    ///
    /// Nine weapons each carried their own copy of this: build a GameObject with a SpriteRenderer,
    /// warm the additive shader during load, then a coroutine that steps frames and hides it again.
    /// The code was identical down to the comments, so changing how effects work meant editing
    /// nine files. The weapons keep their own serialized frame arrays and materials; only the
    /// machinery moved here.
    /// </summary>
    public sealed class SpriteEffectPlayer
    {
        private readonly MonoBehaviour _owner;
        private readonly SpriteRenderer _renderer;
        private readonly Transform _transform;
        private readonly Sprite[] _defaultFrames;
        private readonly WaitForSeconds _frameWait;
        private Coroutine _routine;

        /// <summary>The effect's own transform, for callers that position it themselves.</summary>
        public Transform Transform => _transform;

        private SpriteEffectPlayer(MonoBehaviour owner, SpriteRenderer renderer, Sprite[] frames, float frameDuration)
        {
            _owner = owner;
            _renderer = renderer;
            _transform = renderer.transform;
            _defaultFrames = frames;
            // Allocated once instead of per frame per playback, which is where the old routines
            // produced a steady trickle of garbage.
            _frameWait = new WaitForSeconds(frameDuration);
        }

        /// <param name="warmUpSprite">Frame to compile the shader with when the effect has no
        /// single default frame set — the warrior's combo picks one from its per-step sets.</param>
        /// <param name="parent">Set for effects that ride along with their owner — the heal and
        /// blessing auras sit on the player. Hit effects stay unparented so they mark where the
        /// hit landed rather than following the player.</param>
        public static SpriteEffectPlayer Create(MonoBehaviour owner, string name, Material material,
                                                Sprite[] frames, float frameDuration, int sortingOrder = 2,
                                                Sprite warmUpSprite = null, Transform parent = null,
                                                float initialScale = 1f)
        {
            var effectObject = new GameObject(name);
            if (parent != null)
            {
                effectObject.transform.SetParent(parent);
                effectObject.transform.localPosition = Vector3.zero;
            }

            effectObject.transform.localScale = Vector3.one * initialScale;

            var renderer = effectObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            if (material != null) renderer.material = material;
            effectObject.SetActive(false);

            var player = new SpriteEffectPlayer(owner, renderer, frames, frameDuration);

            // Forces the additive shader variant to compile on scene load (one invisible on-screen
            // frame) instead of during the player's first real hit, where a compile stutter would
            // otherwise show up as a flash of the wrong (uncompiled fallback) colour.
            var warmUp = warmUpSprite != null ? warmUpSprite
                       : frames != null && frames.Length > 0 ? frames[0] : null;
            if (material != null && warmUp != null)
            {
                owner.StartCoroutine(player.WarmUp(warmUp));
            }

            return player;
        }

        public void Play(Vector2 position, float rotationDegrees = 0f, float scale = 1f)
        {
            Play(_defaultFrames, position, rotationDegrees, scale);
        }

        /// <summary>Plays without moving the effect — for the parented auras, which stay put.</summary>
        public void PlayInPlace()
        {
            if (_renderer == null || _defaultFrames == null || _defaultFrames.Length == 0) return;

            Restart(_defaultFrames);
        }

        /// <summary>Plays a different frame set — the warrior's combo cycles one per step.</summary>
        public void Play(Sprite[] frames, Vector2 position, float rotationDegrees = 0f, float scale = 1f)
        {
            if (_renderer == null || frames == null || frames.Length == 0) return;

            _transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotationDegrees));
            _transform.localScale = Vector3.one * scale;

            Restart(frames);
        }

        private void Restart(Sprite[] frames)
        {
            if (_routine != null) _owner.StopCoroutine(_routine);
            _routine = _owner.StartCoroutine(Run(frames));
        }

        public void Hide()
        {
            if (_renderer != null) _renderer.gameObject.SetActive(false);
        }

        private IEnumerator Run(Sprite[] frames)
        {
            _renderer.gameObject.SetActive(true);

            foreach (var frame in frames)
            {
                _renderer.sprite = frame;
                yield return _frameWait;
            }

            _renderer.gameObject.SetActive(false);
            _routine = null;
        }

        private IEnumerator WarmUp(Sprite sprite)
        {
            _renderer.sprite = sprite;
            _transform.position = _owner.transform.position;

            var originalColor = _renderer.color;
            _renderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
            _renderer.gameObject.SetActive(true);

            yield return null;

            _renderer.gameObject.SetActive(false);
            _renderer.color = originalColor;
        }
    }
}
