using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Swarm.UI
{
    /// <summary>
    /// On-screen action button helper. Fires on pointer down (instead of the
    /// Button's pointer-up click) so touch input feels responsive, and gives the
    /// button a small press-scale feedback. Placed next to a Selectable so the
    /// interactable state is still respected.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIPressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Selectable selectable;
        [SerializeField] private float pressedScale = 0.9f;
        [SerializeField] private float scaleSpeed = 14f;
        [SerializeField] private UnityEvent onPress;

        private RectTransform _rect;
        private Vector3 _baseScale;
        private bool _pressed;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _baseScale = _rect.localScale;
            if (selectable == null) selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            _pressed = false;
            if (_rect != null) _rect.localScale = _baseScale;
        }

        private void Update()
        {
            var target = _pressed ? _baseScale * pressedScale : _baseScale;
            _rect.localScale = Vector3.Lerp(_rect.localScale, target, 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable != null && !selectable.IsInteractable()) return;

            _pressed = true;
            onPress?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData) => _pressed = false;

        public void OnPointerExit(PointerEventData eventData) => _pressed = false;
    }
}
