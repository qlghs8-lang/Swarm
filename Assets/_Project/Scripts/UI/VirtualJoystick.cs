using UnityEngine;
using UnityEngine.EventSystems;

namespace Swarm.UI
{
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform touchZone;
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [SerializeField] private float maxRadius = 80f;

        public Vector2 Direction { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                touchZone, eventData.position, eventData.pressEventCamera, out var localPoint);

            background.anchoredPosition = localPoint;
            background.gameObject.SetActive(true);
            handle.anchoredPosition = Vector2.zero;
            Direction = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                touchZone, eventData.position, eventData.pressEventCamera, out var localPoint);

            var offset = Vector2.ClampMagnitude(localPoint - background.anchoredPosition, maxRadius);
            handle.anchoredPosition = offset;
            Direction = offset / maxRadius;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            handle.anchoredPosition = Vector2.zero;
            Direction = Vector2.zero;
            background.gameObject.SetActive(false);
        }
    }
}
