using Swarm.Player;
using Swarm.Weapon;
using UnityEngine;
using UnityEngine.UI;

namespace Swarm.UI
{
    /// <summary>
    /// HP 바 오른쪽에 붙는 버프 아이콘 줄.
    /// 전사 레이지는 스택 수를, 마법사 블레싱은 버프가 켜져 있는 동안 남은 지속시간을 radial fill로 보여준다.
    /// 힐은 별도 아이콘을 띄우지 않는다.
    /// 슬롯은 런타임에 생성되므로 씬에는 이 컴포넌트와 스프라이트 참조만 있으면 된다.
    /// </summary>
    public class BuffIconBarUI : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private Sprite slotFrameSprite;
        [SerializeField] private Sprite rageIcon;
        [SerializeField] private Sprite blessingIcon;

        [Header("Layout")]
        [SerializeField] private float slotSize = 64f;
        [SerializeField] private float spacing = 8f;
        [SerializeField] private float iconPadding = 6f;

        [Header("Style")]
        [SerializeField] private Font countFont;
        [SerializeField] private int countFontSize = 22;
        [SerializeField] private Color cooldownOverlayColor = new(0f, 0f, 0f, 0.62f);
        [SerializeField] private Color buffActiveTint = new(1f, 0.92f, 0.55f, 1f);
        [SerializeField] private float buffPulseSpeed = 6f;

        private static Sprite _whiteSprite;

        private Slot _rageSlot;
        private Slot _blessingSlot;

        private WarriorRageWeapon _rageWeapon;
        private MageBlessingWeapon _blessingWeapon;
        private PlayerStats _stats;

        // The stack count changes a handful of times a run, but ToString() ran every frame.
        private int _shownRageStacks = -1;

        private class Slot
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Frame;
            public Image Icon;
            public Image Cooldown;
            public Text Count;
        }

        private void Start()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.TryGetComponent(out _rageWeapon);
                player.TryGetComponent(out _blessingWeapon);
                player.TryGetComponent(out _stats);
            }

            _rageSlot = CreateSlot("RageSlot", rageIcon, true);
            _blessingSlot = CreateSlot("BlessingSlot", blessingIcon, false);
        }

        private void LateUpdate()
        {
            var index = 0;

            var rageActive = _rageWeapon != null && _rageWeapon.enabled && _rageWeapon.Level > 0;
            if (rageActive)
            {
                if (_rageWeapon.StackCount != _shownRageStacks)
                {
                    _shownRageStacks = _rageWeapon.StackCount;
                    _rageSlot.Count.text = _shownRageStacks.ToString();
                }

                Place(_rageSlot, index++);
            }
            else
            {
                Hide(_rageSlot);
            }

            // 블레싱은 버프가 실제로 걸려 있는 동안에만 보여준다.
            var blessingActive = _blessingWeapon != null && _blessingWeapon.enabled && _blessingWeapon.Level > 0
                                 && _stats != null && _stats.IsTemporaryBuffActive;
            if (blessingActive)
            {
                var duration = _blessingWeapon.BuffDuration;
                _blessingSlot.Cooldown.fillAmount = duration > 0f
                    ? 1f - Mathf.Clamp01(_stats.TemporaryBuffRemaining / duration)
                    : 0f;

                var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * buffPulseSpeed);
                _blessingSlot.Frame.color = Color.Lerp(Color.white, buffActiveTint, pulse);
                _blessingSlot.Rect.localScale = Vector3.one * (1f + 0.06f * pulse);

                Place(_blessingSlot, index);
            }
            else
            {
                _blessingSlot.Frame.color = Color.white;
                _blessingSlot.Rect.localScale = Vector3.one;
                Hide(_blessingSlot);
            }
        }

        private void Place(Slot slot, int index)
        {
            if (!slot.Root.activeSelf) slot.Root.SetActive(true);
            slot.Rect.anchoredPosition = new Vector2(index * (slotSize + spacing), 0f);
        }

        private static void Hide(Slot slot)
        {
            if (slot != null && slot.Root.activeSelf) slot.Root.SetActive(false);
        }

        private Slot CreateSlot(string name, Sprite icon, bool withCount)
        {
            var slot = new Slot();

            slot.Root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            slot.Rect = (RectTransform)slot.Root.transform;
            slot.Rect.SetParent(transform, false);
            slot.Rect.anchorMin = new Vector2(0f, 1f);
            slot.Rect.anchorMax = new Vector2(0f, 1f);
            slot.Rect.pivot = new Vector2(0f, 1f);
            slot.Rect.sizeDelta = new Vector2(slotSize, slotSize);

            slot.Frame = slot.Root.GetComponent<Image>();
            slot.Frame.sprite = slotFrameSprite;
            slot.Frame.raycastTarget = false;
            slot.Frame.enabled = slotFrameSprite != null;

            slot.Icon = CreateChildImage(slot.Rect, "Icon", icon, Color.white, iconPadding);
            slot.Icon.preserveAspect = true;

            slot.Cooldown = CreateChildImage(slot.Rect, "Cooldown", WhiteSprite(), cooldownOverlayColor, iconPadding);
            slot.Cooldown.type = Image.Type.Filled;
            slot.Cooldown.fillMethod = Image.FillMethod.Radial360;
            slot.Cooldown.fillOrigin = (int)Image.Origin360.Top;
            slot.Cooldown.fillClockwise = false;
            slot.Cooldown.fillAmount = 0f;
            slot.Cooldown.enabled = !withCount;

            if (withCount)
            {
                var countGo = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
                var countRect = (RectTransform)countGo.transform;
                countRect.SetParent(slot.Rect, false);
                countRect.anchorMin = Vector2.zero;
                countRect.anchorMax = Vector2.one;
                countRect.offsetMin = new Vector2(0f, 2f);
                countRect.offsetMax = new Vector2(-3f, 0f);

                slot.Count = countGo.GetComponent<Text>();
                slot.Count.font = countFont != null ? countFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                slot.Count.fontSize = countFontSize;
                slot.Count.fontStyle = FontStyle.Bold;
                slot.Count.alignment = TextAnchor.LowerRight;
                slot.Count.color = Color.white;
                slot.Count.raycastTarget = false;
                slot.Count.horizontalOverflow = HorizontalWrapMode.Overflow;
                slot.Count.verticalOverflow = VerticalWrapMode.Overflow;

                var outline = countGo.GetComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            slot.Root.SetActive(false);
            return slot;
        }

        private static Image CreateChildImage(RectTransform parent, string name, Sprite sprite, Color color, float padding)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite WhiteSprite()
        {
            if (_whiteSprite == null)
            {
                _whiteSprite = Sprite.Create(
                    Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect);
                _whiteSprite.name = "BuffIconBar_White";
            }

            return _whiteSprite;
        }
    }
}
