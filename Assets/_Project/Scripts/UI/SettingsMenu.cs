using System.Collections.Generic;
using Swarm.Audio;
using Swarm.Game;
using Swarm.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Swarm.UI
{
    /// <summary>
    /// 설정 창. 자기 캔버스까지 통째로 런타임에 만들고 씬 로드를 넘겨 살아남는다 —
    /// <see cref="BackgroundMusic"/>과 같은 방식이다. 타이틀·게임·테스트 스테이지가 각각 다른 씬이라
    /// 씬마다 같은 패널을 놓고 같은 배선을 세 번 반복하면 셋이 조금씩 어긋나기 시작하고,
    /// 항목을 하나 늘릴 때마다 세 군데를 고쳐야 한다.
    ///
    /// 여는 방법은 두 가지다.
    /// * 씬에 만들어 둔 톱니 버튼: 그 버튼 오브젝트에 <see cref="SettingsButton"/>을 붙이면 끝.
    ///   OnClick 배선은 필요 없다.
    /// * ESC 키(에디터·PC 확인용).
    ///
    /// 플레이 중(씬에 Player 태그가 있으면)에는 열리는 순간 <see cref="Time.timeScale"/>을 0으로
    /// 내리고, 닫을 때 원래 값으로 되돌린다. 레벨업 카드가 떠 있는 동안 열어도 카드 쪽이 걸어 둔
    /// 0이 그대로 복원된다.
    /// </summary>
    public sealed class SettingsMenu : MonoBehaviour
    {
        private const string ObjectName = "SettingsMenu (Runtime)";
        private const string SkinResourcePath = "SettingsSkin";

        private const float PanelWidth = 720f;
        private const float PanelPadding = 36f;
        private const float RowHeight = 78f;
        private const float RowGap = 14f;
        private const float TitleHeight = 76f;

        // 색은 전부 흰색(=스프라이트 원본 색 그대로)이거나 그 위에 살짝 얹는 정도로만 쓴다.
        // UI 아트(Panel_Frame, Button_*)가 이미 팔레트를 들고 있어서, 여기서 색을 덧칠하면
        // 픽셀 아트의 명암 단계가 뭉개진다.
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color SpriteTint = Color.white;
        private static readonly Color LabelColor = new Color(0.94f, 0.87f, 0.75f, 1f);
        private static readonly Color ValueColor = new Color(0.66f, 0.53f, 0.37f, 1f);
        private static readonly Color TitleColor = new Color(1f, 0.90f, 0.71f, 1f);
        private static readonly Color DangerTint = new Color(1f, 0.72f, 0.66f, 1f);

        // 스프라이트가 없을 때(스킨 에셋 분실) 코드로 그린 도형에 입히는 대체 색.
        private static readonly Color FallbackPanelColor = new Color(0.24f, 0.19f, 0.16f, 0.98f);
        private static readonly Color FallbackButtonColor = new Color(0.42f, 0.33f, 0.25f, 1f);
        private static readonly Color FallbackPrimaryColor = new Color(0.66f, 0.53f, 0.37f, 1f);

        private sealed class Row
        {
            public RectTransform Rect;
            public float Height = RowHeight;
            public bool Visible = true;
        }

        public static SettingsMenu Instance { get; private set; }

        public bool IsOpen => _root != null && _root.activeSelf;

        private GameObject _root;
        private GameObject _gear;
        private RectTransform _panel;
        private RectTransform _content;
        private readonly List<Row> _rows = new();

        private Row _runRow;
        private Button _rollLeftButton;
        private Button _rollRightButton;
        private Button _damageOffButton;
        private Button _damageOnButton;
        private Slider _bgmSlider;
        private Slider _sfxSlider;
        private Text _bgmValueText;
        private Text _sfxValueText;

        private SettingsMenuSkin _skin;
        private GameManager _gameManager;
        private float _previousTimeScale = 1f;
        private bool _pausedByMenu;
        private bool _suppressCallbacks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Instance != null) return;

            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            host.AddComponent<SettingsMenu>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // 씬에 배선할 수 없는 오브젝트라 아트를 이름으로 불러온다. 없으면 도형으로 대체된다.
            _skin = Resources.Load<SettingsMenuSkin>(SkinResourcePath);
            Build();
            RefreshGear();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Toggle();
            }
        }

        /// <summary>
        /// 씬이 바뀌면 창을 닫되 timeScale은 건드리지 않는다. 새 씬은 이미 자기 값으로 시작하고
        /// 있으므로, 여기서 이전 씬의 값을 복원하면 그쪽을 덮어쓰게 된다.
        /// </summary>
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _pausedByMenu = false;
            _gameManager = null;
            if (_root != null) _root.SetActive(false);
            RefreshGear();
        }

        /// <summary>
        /// 기본 톱니 버튼을 보일지 정한다.
        ///
        /// 씬에 직접 만들어 둔 버튼(<see cref="SettingsButton"/>이 붙은)이 있으면 그쪽에 자리를 내준다 —
        /// 같은 일을 하는 버튼이 화면에 두 개 뜨는 것이 이 기본 버튼의 유일한 실패 방식이다.
        /// 설정 창이 열려 있는 동안에도 숨긴다(닫기 버튼이 그 자리를 대신한다).
        /// </summary>
        private void RefreshGear()
        {
            if (_gear == null) return;
            var sceneHasOwnButton = FindAnyObjectByType<SettingsButton>() != null;
            _gear.SetActive(!IsOpen && !sceneHasOwnButton);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_root == null || IsOpen) return;

            _gameManager = FindAnyObjectByType<GameManager>();

            // 타이틀에는 플레이어가 없다 — 멈출 것도 없고, 멈추면 슬롯 버튼의 연출만 굳는다.
            var inPlay = GameObject.FindGameObjectWithTag("Player") != null;
            if (inPlay && !_pausedByMenu)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _pausedByMenu = true;
            }

            _runRow.Visible = _gameManager != null;

            PullValues();
            LayoutRows();
            _root.SetActive(true);
            RefreshGear();
        }

        public void Close()
        {
            if (_root == null || !IsOpen) return;

            _root.SetActive(false);
            RefreshGear();

            if (_pausedByMenu)
            {
                Time.timeScale = _previousTimeScale;
                _pausedByMenu = false;
            }
        }

        /// <summary>씬의 버튼이 부를 수 있게 인스턴스 없이도 열리는 입구.</summary>
        public static void OpenMenu() => Instance?.Open();

        public static void ToggleMenu() => Instance?.Toggle();

        // ------------------------------------------------------------------ 값 반영

        /// <summary>저장된 값을 위젯에 되읽는다. 열 때마다 부르므로 화면과 저장값이 어긋날 수 없다.</summary>
        private void PullValues()
        {
            _suppressCallbacks = true;

            var side = GameSettings.RollSide;
            Paint(_rollLeftButton, side == RollButtonSide.Left);
            Paint(_rollRightButton, side == RollButtonSide.Right);

            var numbers = GameSettings.ShowDamageNumbers;
            Paint(_damageOnButton, numbers);
            Paint(_damageOffButton, !numbers);

            _bgmSlider.value = GameSettings.BgmVolume;
            _sfxSlider.value = GameSettings.SfxVolume;
            _bgmValueText.text = Percent(_bgmSlider.value);
            _sfxValueText.text = Percent(_sfxSlider.value);

            _suppressCallbacks = false;
        }

        private static string Percent(float value01) => Mathf.RoundToInt(value01 * 100f) + "%";

        /// <summary>
        /// 두 갈래 선택지에서 고른 쪽을 표시한다. 색을 덧칠하는 대신 눌린 느낌의 밝은 프레임
        /// (Button_Primary)으로 스프라이트를 바꾼다 — 타이틀 화면의 '선택됨' 슬롯과 같은 방식이다.
        /// </summary>
        private void Paint(Button button, bool on)
        {
            if (button == null) return;
            if (button.targetGraphic is not Image image) return;

            if (_skin != null && _skin.Button != null && _skin.ButtonPrimary != null)
            {
                image.sprite = on ? _skin.ButtonPrimary : _skin.Button;
                image.color = SpriteTint;
                return;
            }

            image.color = on ? FallbackPrimaryColor : FallbackButtonColor;
        }

        private void SetRollSide(RollButtonSide side)
        {
            if (_suppressCallbacks) return;
            GameSettings.RollSide = side;
            PullValues();
        }

        private void SetDamageNumbers(bool show)
        {
            if (_suppressCallbacks) return;
            GameSettings.ShowDamageNumbers = show;
            PullValues();
        }

        // ------------------------------------------------------------------ 구성

        private void Build()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.layer = LayerMask.NameToLayer("UI");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 결과 패널·레벨업 카드가 올라와 있는 HUD 캔버스(0)보다 확실히 위.
            canvas.sortingOrder = 500;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)_root.transform);

            // 뒤쪽 조이스틱·버튼이 눌리지 않도록 화면 전체를 덮는 차단막.
            var backdrop = CreateImage(_root.transform, "Backdrop", BackdropColor, null);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;

            var panelSprite = _skin != null ? _skin.Panel : null;
            var panelImage = CreateImage(_root.transform, "Panel",
                                         panelSprite != null ? SpriteTint : FallbackPanelColor,
                                         panelSprite != null ? panelSprite : RoundedSprite());
            _panel = panelImage.rectTransform;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(PanelWidth, 600f);

            var title = CreateText(_panel, "Title", "설정", 40, TextAnchor.MiddleCenter, TitleColor);
            title.fontStyle = FontStyle.Bold;
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(PanelPadding, -TitleHeight);
            titleRect.offsetMax = new Vector2(-PanelPadding, 0f);

            _content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            _content.SetParent(_panel, false);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = new Vector2(PanelPadding, 0f);
            _content.offsetMax = new Vector2(-PanelPadding, -TitleHeight);

            BuildRollRow();
            BuildVolumeRow("BgmRow", "배경 음악", out _bgmSlider, out _bgmValueText, value =>
            {
                if (_suppressCallbacks) return;
                GameSettings.BgmVolume = value;
                _bgmValueText.text = Percent(value);
            });
            BuildVolumeRow("SfxRow", "효과음", out _sfxSlider, out _sfxValueText, value =>
            {
                if (_suppressCallbacks) return;
                GameSettings.SfxVolume = value;
                _sfxValueText.text = Percent(value);
            });
            BuildDamageNumberRow();
            BuildRunRow();
            BuildCloseRow();

            _root.SetActive(false);

            BuildGear(canvasObject.transform);
        }

        /// <summary>
        /// 화면 우측 상단의 기본 톱니 버튼. 설정 창과 같은 캔버스에 올라가므로 씬이 바뀌어도 따라다니고,
        /// HUD(sortingOrder 0)보다 위에 있어 결과 패널이 떠 있어도 가려지지 않는다.
        /// </summary>
        private void BuildGear(Transform parent)
        {
            var button = CreateButton((RectTransform)parent, "SettingsGear", string.Empty, SpriteTint, Open);
            _gear = button.gameObject;

            var rect = (RectTransform)_gear.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(96f, 96f);
            rect.anchoredPosition = new Vector2(-40f, -40f);

            var iconSprite = _skin != null && _skin.GearIcon != null ? _skin.GearIcon : GearSprite();
            var icon = CreateImage(rect, "Icon", SpriteTint, iconSprite);
            icon.raycastTarget = false;
            Stretch(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(20f, 20f);
            icon.rectTransform.offsetMax = new Vector2(-20f, -20f);
        }

        private void BuildRollRow()
        {
            var row = NewRow("RollSideRow");
            CreateLabel(row.Rect, "구르기 버튼 위치");

            _rollLeftButton = CreateSegment(row.Rect, "Left", "왼쪽", 2, 0, () => SetRollSide(RollButtonSide.Left));
            _rollRightButton = CreateSegment(row.Rect, "Right", "오른쪽", 2, 1, () => SetRollSide(RollButtonSide.Right));
        }

        private void BuildDamageNumberRow()
        {
            var row = NewRow("DamageNumberRow");
            CreateLabel(row.Rect, "데미지 숫자 표시");

            _damageOffButton = CreateSegment(row.Rect, "Off", "끄기", 2, 0, () => SetDamageNumbers(false));
            _damageOnButton = CreateSegment(row.Rect, "On", "켜기", 2, 1, () => SetDamageNumbers(true));
        }

        private void BuildVolumeRow(string name, string label, out Slider slider, out Text valueText, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var row = NewRow(name);
            CreateLabel(row.Rect, label);

            valueText = CreateText(row.Rect, "Value", "100%", 26, TextAnchor.MiddleRight, ValueColor);
            var valueRect = valueText.rectTransform;
            valueRect.anchorMin = new Vector2(1f, 0.5f);
            valueRect.anchorMax = new Vector2(1f, 0.5f);
            valueRect.pivot = new Vector2(1f, 0.5f);
            valueRect.sizeDelta = new Vector2(90f, 40f);
            valueRect.anchoredPosition = Vector2.zero;

            slider = CreateSlider(row.Rect, "Slider", onChanged);
        }

        private void BuildRunRow()
        {
            _runRow = NewRow("RunRow");

            CreateWideButton(_runRow.Rect, "Restart", "재시작", SpriteTint, 2, 0, () =>
            {
                // 씬 재로드가 뒤따르므로 Close()의 timeScale 복원이 먼저 돌아야 한다.
                var manager = _gameManager;
                Close();
                if (manager != null) manager.Restart();
            });

            CreateWideButton(_runRow.Rect, "Title", "타이틀로 나가기", DangerTint, 2, 1, () =>
            {
                var manager = _gameManager;
                Close();
                if (manager != null) manager.GoToTitle();
            });
        }

        private void BuildCloseRow()
        {
            var row = NewRow("CloseRow");
            CreateWideButton(row.Rect, "Close", "닫기", SpriteTint, 1, 0, Close);
        }

        private Row NewRow(string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(_content, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -RowHeight);
            rect.offsetMax = Vector2.zero;

            var row = new Row { Rect = rect };
            _rows.Add(row);
            return row;
        }

        /// <summary>보이는 줄만 위에서부터 쌓고, 그 높이에 맞춰 패널을 줄인다.</summary>
        private void LayoutRows()
        {
            var y = 0f;

            foreach (var row in _rows)
            {
                row.Rect.gameObject.SetActive(row.Visible);
                if (!row.Visible) continue;

                row.Rect.offsetMax = new Vector2(0f, -y);
                row.Rect.offsetMin = new Vector2(0f, -(y + row.Height));
                y += row.Height + RowGap;
            }

            var contentHeight = Mathf.Max(0f, y - RowGap);
            _content.offsetMin = new Vector2(PanelPadding, -(TitleHeight + contentHeight));
            _panel.sizeDelta = new Vector2(PanelWidth, TitleHeight + contentHeight + PanelPadding);
        }

        // ------------------------------------------------------------------ 위젯 조립

        private static Text CreateLabel(RectTransform parent, string text)
        {
            var label = CreateText(parent, "Label", text, 28, TextAnchor.MiddleLeft, LabelColor);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0.45f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return label;
        }

        /// <summary>줄 오른쪽 절반을 <paramref name="count"/>등분한 칸 중 <paramref name="index"/>번째.</summary>
        private Button CreateSegment(RectTransform parent, string name, string label, int count, int index, UnityEngine.Events.UnityAction onClick)
        {
            var button = CreateButton(parent, name, label, SpriteTint, onClick);

            // 선택 표시는 Paint()가 image.sprite를 Button_Primary로 바꿔서 한다. 그런데 Selectable의
            // SpriteSwap은 그 위에 overrideSprite를 덮어쓴다 — 버튼을 한 번 누르면 그 칸이 계속
            // '선택된(selected)' 상태로 남아 Button_Hover가 덮이고, 결과적으로 고른 칸과 안 고른 칸이
            // 같아 보였다. 두 갈래 선택지에서는 눌림 연출보다 '어느 쪽을 골랐는가'가 중요하므로,
            // 여기서만 색 틴트로 바꿔 밑그림(Primary/Normal)이 항상 드러나게 한다.
            if (_skin != null && _skin.Button != null)
            {
                button.transition = Selectable.Transition.ColorTint;
                var colors = button.colors;
                colors.normalColor = SpriteTint;
                colors.selectedColor = SpriteTint;
                colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
                colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
                colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                colors.fadeDuration = 0.08f;
                button.colors = colors;
            }
            else
            {
                // 스킨이 없을 때는 Paint()가 색으로 선택을 표시한다. 어떤 전환이든 그 색을 덮어쓴다.
                button.transition = Selectable.Transition.None;
            }

            var rect = ((RectTransform)button.transform);
            var slot = 0.55f / count;
            rect.anchorMin = new Vector2(0.45f + slot * index, 0.14f);
            rect.anchorMax = new Vector2(0.45f + slot * (index + 1), 0.86f);
            rect.offsetMin = new Vector2(6f, 0f);
            rect.offsetMax = new Vector2(-6f, 0f);
            return button;
        }

        /// <summary>줄 전체를 <paramref name="count"/>등분한 칸을 채우는 버튼.</summary>
        private Button CreateWideButton(RectTransform parent, string name, string label, Color color, int count, int index, UnityEngine.Events.UnityAction onClick)
        {
            var button = CreateButton(parent, name, label, color, onClick);
            var rect = ((RectTransform)button.transform);
            var slot = 1f / count;
            rect.anchorMin = new Vector2(slot * index, 0.12f);
            rect.anchorMax = new Vector2(slot * (index + 1), 0.88f);
            rect.offsetMin = new Vector2(6f, 0f);
            rect.offsetMax = new Vector2(-6f, 0f);
            return button;
        }

        /// <summary>
        /// 씬의 다른 버튼들과 같은 아트를 쓰는 버튼. 눌림·호버 상태도 같은 스프라이트 세트를 그대로
        /// 갈아끼우므로(SpriteSwap) 손맛이 상점·레벨업 버튼과 일치한다.
        /// </summary>
        private Button CreateButton(RectTransform parent, string name, string label, Color tint, UnityEngine.Events.UnityAction onClick)
        {
            var skinned = _skin != null && _skin.Button != null;
            var image = CreateImage(parent, name,
                                    skinned ? tint : FallbackButtonColor,
                                    skinned ? _skin.Button : RoundedSprite());

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            if (skinned)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = _skin.ButtonHover,
                    pressedSprite = _skin.ButtonPressed,
                    selectedSprite = _skin.ButtonHover,
                    disabledSprite = _skin.ButtonDisabled
                };
            }

            var text = CreateText(image.rectTransform, "Text", label, 26, TextAnchor.MiddleCenter, LabelColor);
            Stretch(text.rectTransform);
            text.raycastTarget = false;

            return button;
        }

        private Slider CreateSlider(RectTransform parent, string name, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var sliderObject = new GameObject(name, typeof(RectTransform));
            sliderObject.transform.SetParent(parent, false);

            var rect = (RectTransform)sliderObject.transform;
            rect.anchorMin = new Vector2(0.45f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(0f, -14f);
            rect.offsetMax = new Vector2(-104f, 14f);

            var frameSprite = _skin != null ? _skin.BarFrame : null;
            var background = CreateImage(rect, "Background",
                                         frameSprite != null ? SpriteTint : new Color(0.18f, 0.15f, 0.12f, 1f),
                                         frameSprite != null ? frameSprite : RoundedSprite());
            Stretch(background.rectTransform);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(rect, false);
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(10f, 0f);
            fillArea.offsetMax = new Vector2(-10f, 0f);

            var fillSprite = _skin != null ? _skin.BarFill : null;
            var fill = CreateImage(fillArea, "Fill",
                                   fillSprite != null ? SpriteTint : FallbackPrimaryColor,
                                   fillSprite != null ? fillSprite : RoundedSprite());
            fill.rectTransform.anchorMin = new Vector2(0f, 0f);
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            fill.rectTransform.sizeDelta = new Vector2(20f, 0f);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(rect, false);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(14f, 0f);
            handleArea.offsetMax = new Vector2(-14f, 0f);

            var handleSprite = _skin != null ? _skin.Button : null;
            var handle = CreateImage(handleArea, "Handle",
                                     handleSprite != null ? SpriteTint : Color.white,
                                     handleSprite != null ? handleSprite : CircleSprite());
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(34f, 0f);

            var slider = sliderObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.AddListener(onChanged);

            return slider;
        }

        private static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            if (sprite != null)
            {
                image.sprite = sprite;
                // 테두리가 있는 스프라이트(둥근 사각형)만 9-slice로 늘린다. 원형 손잡이는
                // Sliced로 두면 유니티가 테두리 없다고 경고를 뱉는다.
                image.type = sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            }

            return image;
        }

        private static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;

            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 씬의 다른 Text들이 쓰는 것과 같은 폰트. 내장 폰트를 직접 부르지 않는다 —
        /// 거기에는 한글 글리프가 없어서 WebGL 빌드에서 이 메뉴의 라벨이 전부 빈칸이 된다.
        /// 자세한 것은 <see cref="UiFont"/>.
        /// </summary>
        private static Font BuiltinFont() => UiFont.Current;

        private static Sprite _roundedSprite;
        private static Sprite _circleSprite;

        /// <summary>
        /// 모서리가 둥근 흰 사각형. 9-slice로 늘려 쓰므로 크기와 상관없이 모서리 반경이 일정하다.
        ///
        /// 유니티 내장 UI 스킨(UI/Skin/UISprite.psd 등)은 에디터 전용 리소스라
        /// <see cref="Resources.GetBuiltinResource{T}"/>로 꺼내면 유니티 6에서는 실패한다.
        /// 필요한 모양이 둥근 사각형 하나뿐이라 그때그때 그려서 쓴다 — 32x32 텍스처 한 장이고
        /// 한 번만 만들어 캐시한다.
        /// </summary>
        private static Sprite RoundedSprite()
        {
            if (_roundedSprite != null) return _roundedSprite;

            const int size = 32;
            const float radius = 8f;

            var texture = NewTexture(size);
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // 픽셀 중심에서 모서리 원의 중심까지의 거리. 네 모서리 안쪽 영역에서는
                    // 거리가 0 이하로 나와 그대로 꽉 찬 면이 된다.
                    var px = x + 0.5f;
                    var py = y + 0.5f;
                    var dx = Mathf.Max(radius - px, px - (size - radius), 0f);
                    var dy = Mathf.Max(radius - py, py - (size - radius), 0f);
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);

                    pixels[y * size + x] = Alpha(1f - Mathf.Clamp01(distance - (radius - 1f)));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            _roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                           SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            _roundedSprite.hideFlags = HideFlags.HideAndDontSave;
            return _roundedSprite;
        }

        /// <summary>슬라이더 손잡이용 흰 원.</summary>
        private static Sprite CircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;

            const int size = 32;
            const float radius = size * 0.5f;

            var texture = NewTexture(size);
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    pixels[y * size + x] = Alpha(1f - Mathf.Clamp01(distance - (radius - 1.5f)));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            _circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _circleSprite.hideFlags = HideFlags.HideAndDontSave;
            return _circleSprite;
        }

        private static Texture2D NewTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                // 잘라 쓰는 스프라이트라 가장자리가 반대편으로 물리면 안 된다.
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            return texture;
        }

        /// <summary>흰색 + 주어진 알파. 색은 Image.color가 입힌다.</summary>
        private static Color32 Alpha(float a) => new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));

        private static Sprite _gearSprite;

        /// <summary>
        /// 톱니바퀴 아이콘. 이빨 여덟 개짜리 원판에서 가운데를 뚫은 모양을, 각도에 따라 바깥 반지름을
        /// 바꿔가며 채운다. 스프라이트 에셋을 하나 더 들이는 대신 64x64 텍스처 한 장으로 끝내는 쪽을
        /// 골랐다 — 아트가 들어오면 이 함수만 갈아끼우면 된다.
        /// </summary>
        private static Sprite GearSprite()
        {
            if (_gearSprite != null) return _gearSprite;

            const int size = 64;
            const int teeth = 8;
            const float center = size * 0.5f;
            const float outerRadius = 30f;   // 이빨 끝
            const float rootRadius = 24f;    // 이빨 사이 골
            const float holeRadius = 9f;     // 가운데 구멍
            const float toothFill = 0.5f;    // 한 주기에서 이빨이 차지하는 비율

            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            var period = Mathf.PI * 2f / teeth;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - center;
                    var dy = y + 0.5f - center;
                    var radius = Mathf.Sqrt(dx * dx + dy * dy);

                    // 이 각도에서 바깥 경계가 어디인가: 이빨 위면 outerRadius, 골이면 rootRadius.
                    var angle = Mathf.Atan2(dy, dx);
                    if (angle < 0f) angle += Mathf.PI * 2f;
                    var withinPeriod = (angle % period) / period;
                    var onTooth = withinPeriod < toothFill;
                    var boundary = onTooth ? outerRadius : rootRadius;

                    // 바깥 경계 안쪽이면서 가운데 구멍 바깥인 부분만 남긴다.
                    var inside = Mathf.Clamp01(boundary - radius);
                    var outsideHole = Mathf.Clamp01(radius - holeRadius);
                    pixels[y * size + x] = Alpha(Mathf.Min(inside, outsideHole));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            _gearSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            _gearSprite.hideFlags = HideFlags.HideAndDontSave;
            return _gearSprite;
        }
    }
}
