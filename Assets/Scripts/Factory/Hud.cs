using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Audio settings fix: SFX volume now functional, duplicate AudioListener removed
// v2: Fixed ScrollRect.content assignment - settings sliders now appear

namespace Game.Factory
{
    /// <summary>
    /// Главный HUD производственного цеха и управления линиями завода.
    /// Построен по модульному принципу с разделением на под-канвасы (Sub-Canvases),
    /// использованием 9-slice спрайтов, аппаратным адаптивным лейаутом и плавной анимацией сайдбара.
    /// </summary>
    public class Hud
    {
        class Card
        {
            public RectTransform Root;
            public Image Background;
            public Image Stripe;
            public Text Title;
            public Text Level;
            public Text Stats;
            public Text Power;
            public Text ButtonLabel;
            public Button Button;
            public Image ButtonImage;
            public Color Accent;
        }

        class Row
        {
            public Text Line;
            public Text Materials;
            public Text Products;
            public Text Levels;
            public Text Price;
            public Text Status;
        }

        // ------------------------------------------------------------- Fields
        readonly FactoryGame _game;

        // TopBar Elements
        Text _money;
        Text _day;
        Text _plan;
        RectTransform _planProgressBar;
        Text _phase;
        Image _energyDot;
        Text _energy;
        Text _materials;
        Text _upkeep;
        Text _planDetail;

        // Пауза и брак
        Button _pauseButton;
        Text _pauseButtonLabel;
        Button _defectButton;
        Text _defectButtonLabel;
        int _defectLine = -1;

        // Sidebar & Dynamic Navigation
        RectTransform _sidebarPanel;
        RectTransform _sidebarToggleArrow;
        bool _sidebarCollapsed;
        float _sidebarCurrentX;
        float _sidebarTargetX;
        const float SidebarWidth = 340f;

        // Modals & Overlays
        Text _tableTotals;
        Text _journal;
        Text _gameOverText;
        GameObject _gameOver;
        GameObject _table;
        Text _tableButtonLabel;
        GameObject _tutorial;
        Text _tutorialButtonLabel;

        // Tutorial Steps
        int _tutorialStep;
        Text _tutorialStepBadge;
        Text _tutorialStepTitle;
        Text _tutorialSubTitle;
        Text _tutorialPoint1Title;
        Text _tutorialPoint1Desc;
        Text _tutorialPoint2Title;
        Text _tutorialPoint2Desc;
        Text _tutorialPoint3Title;
        Text _tutorialPoint3Desc;
        Text _tutorialPoint4Title;
        Text _tutorialPoint4Desc;
        Text _tutorialTipText;
        Button _tutorialPrev;
        Button _tutorialNext;
        Text _tutorialNextLabel;
        Button[] _tutorialStepTabs;

        // Практика: короткая плашка снизу, где задания выполняются руками.
        GameObject _practice;
        Text _practiceBadge;
        Text _practiceTask;
        Text _practiceCounter;
        Button _practiceSkip;

        // -1 — практика выключена, 0..3 — номер задания.
        int _practiceStep = -1;
        bool _practicePausePressed;
        int _practiceBuyerLevel;

        // Settings
        GameObject _settings;
        GameObject _settingsContent;
        ScrollRect _settingsScrollRect;
        Scrollbar _settingsScrollbar;
        Slider _musicSlider;
        Text _musicVolumeText;
        Slider _sfxSlider;
        Text _sfxVolumeText;

        const string TutorialDoneKey = "factory_tutorial_done";

        // Advice Banner
        GameObject _hintCard;
        Text _hintTitle;
        Text _hintText;
        Button _hintMinimizedBtn;
        bool _adviceVisible = true;

        // Cards & Tabs
        readonly Card[] _cards = new Card[3];
        readonly Card[] _generators = new Card[2];
        readonly Card[] _assemblyCards = new Card[3];
        Card _build;
        Button[] _tabs;
        Row[] _rows;

        int _selected;
        int _buildTarget = -1;

        // Fonts & Typography
        static Font _font;

        // Design System Palette (Figma Design Tokens)
        static readonly Color ColorSurface0 = new Color(0.070f, 0.086f, 0.102f, 1f);     // #12161A
        static readonly Color ColorSurface1 = new Color(0.094f, 0.125f, 0.145f, 0.98f);   // #182025
        static readonly Color ColorSurface2 = new Color(0.133f, 0.169f, 0.192f, 1f);     // #222B31
        static readonly Color ColorAssemblyBase = new Color(0.133f, 0.157f, 0.180f, 1f); // #22282E
        static readonly Color ColorBorder = new Color(0.169f, 0.220f, 0.251f, 1f);       // #2B3840
        static readonly Color TextWhite = new Color(0.96f, 0.97f, 0.99f, 1f);
        static readonly Color TextMuted = new Color(0.56f, 0.61f, 0.65f, 1f);             // #8F9CA7

        // Accent Colors
        static readonly Color AccentAmber = new Color(1.00f, 0.702f, 0.00f, 1f);          // #FFB300
        static readonly Color AccentCyan = new Color(0.00f, 0.824f, 1.00f, 1f);           // #00D2FF
        static readonly Color AccentGreen = new Color(0.180f, 0.835f, 0.451f, 1f);        // #2ED573
        static readonly Color AccentPink = new Color(0.949f, 0.580f, 0.722f, 1f);         // #F294B8
        static readonly Color AccentPurple = new Color(0.647f, 0.369f, 0.918f, 1f);       // #A55EEA
        static readonly Color AccentRed = new Color(0.839f, 0.188f, 0.192f, 1f);          // #D63031

        static readonly Color[] LineColors = { AccentAmber, AccentCyan, AccentPurple };

        // 9-Slice UI Sprites
        static Sprite _sprTopBarBg;
        static Sprite _sprBannerPlanBg;
        static Sprite _sprBannerAdviceBg;
        static Sprite _sprSidebarBg;
        static Sprite _sprCardMachineBase;
        static Sprite _sprCardAssemblyBase;
        static Sprite _sprTabContainerBg;
        static Sprite _sprTabActive;
        static Sprite _sprBtnAmber;
        static Sprite _sprBtnPink;
        static Sprite _sprBtnNeutral;
        static Sprite _sprBtnDisabled;
        static Sprite _sprBtnRed;
        static Sprite _sprSidebarToggle;
        static Sprite _sprProgressTrack;
        static Sprite _sprProgressFill;
        static Sprite _sprAccentBar;
        static Sprite _sprDivider;
        static Sprite _sprStatusDot;
        static Sprite _sprArrowCollapse;

        // ------------------------------------------------------------- Constructor
        public Hud(FactoryGame game)
        {
            _game = game;
            InitSprites();
            _font = LoadFont();
            Build();
        }

        static void InitSprites()
        {
            _sprTopBarBg = LoadSprite("panel_topbar_bg", 14, 14, 14, 14, ColorSurface1, ColorBorder, 12);
            _sprBannerPlanBg = LoadSprite("banner_daily_plan_bg", 10, 10, 10, 10, new Color(0.086f, 0.122f, 0.141f), ColorBorder, 8);
            _sprBannerAdviceBg = LoadSprite("banner_advice_bg", 14, 14, 14, 14, ColorSurface1, ColorBorder, 12);
            _sprSidebarBg = LoadSprite("frame_sidebar_bg", 18, 18, 18, 18, ColorSurface1, new Color(0.16f, 0.21f, 0.24f), 16);
            _sprCardMachineBase = LoadSprite("card_machine_base", 14, 14, 14, 14, ColorSurface2, new Color(0.18f, 0.23f, 0.27f), 12);
            _sprCardAssemblyBase = LoadSprite("card_assembly_base", 14, 14, 14, 14, ColorAssemblyBase, new Color(0.24f, 0.16f, 0.22f), 12);
            _sprTabContainerBg = LoadSprite("tab_container_bg", 12, 12, 12, 12, new Color(0.078f, 0.102f, 0.122f), Color.clear, 10);
            _sprTabActive = LoadSprite("tab_item_active", 10, 10, 10, 10, ColorSurface2, Color.clear, 8);
            _sprBtnAmber = LoadSprite("btn_cta_amber", 10, 10, 10, 10, AccentAmber, Color.clear, 8);
            _sprBtnPink = LoadSprite("btn_cta_pink", 10, 10, 10, 10, AccentPink, Color.clear, 8);
            _sprBtnNeutral = LoadSprite("btn_secondary_neutral", 10, 10, 10, 10, ColorSurface1, new Color(0.17f, 0.22f, 0.25f), 8);
            _sprBtnDisabled = LoadSprite("btn_disabled", 10, 10, 10, 10, new Color(0.118f, 0.149f, 0.173f), Color.clear, 8);
            _sprBtnRed = LoadSprite("btn_destructive_red", 10, 10, 10, 10, AccentRed, Color.clear, 8);
            _sprSidebarToggle = LoadSprite("sidebar_toggle_bg", 12, 12, 12, 12, ColorSurface1, new Color(0.16f, 0.21f, 0.24f), 10);
            _sprProgressTrack = LoadSprite("progress_track_bg", 4, 4, 4, 4, new Color(0.078f, 0.102f, 0.122f), Color.clear, 3);
            _sprProgressFill = LoadSprite("progress_fill_cyan", 4, 4, 4, 4, AccentCyan, Color.clear, 3);
            _sprAccentBar = LoadSprite("card_accent_bar", 2, 2, 2, 2, Color.white, Color.clear, 2);
            _sprDivider = LoadSprite("divider_vertical_line", 1, 1, 1, 1, ColorBorder, Color.clear, 0);
            _sprStatusDot = LoadSprite("status_dot_indicator", 0, 0, 0, 0, AccentGreen, Color.clear, 0);
            _sprArrowCollapse = LoadSprite("icon_arrow_collapse", 0, 0, 0, 0, AccentCyan, Color.clear, 0);
        }

        static Sprite LoadSprite(string name, int l, int b, int r, int t, Color fallbackFill, Color fallbackStroke, int radius)
        {
            Sprite sp = Resources.Load<Sprite>("UI/Sprites/" + name);
            if (sp != null) return sp;

            Texture2D tex = Resources.Load<Texture2D>("UI/Sprites/" + name);
            if (tex != null)
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(l, b, r, t));
            }

            int size = Mathf.Max(32, radius * 2 + 4);
            Texture2D pTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            pTex.filterMode = FilterMode.Bilinear;
            Color[] cols = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = (x == 0 || y == 0 || x == size - 1 || y == size - 1);
                    cols[y * size + x] = (isBorder && fallbackStroke.a > 0.05f) ? fallbackStroke : fallbackFill;
                }
            }
            pTex.SetPixels(cols);
            pTex.Apply();
            return Sprite.Create(pTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(l, b, r, t));
        }

        // ------------------------------------------------------------- Soft shapes
        static readonly Dictionary<string, Sprite> _softShapeCache = new Dictionary<string, Sprite>();

        // Мягкая фигура с антиалиасингом: круг или скруглённая плашка.
        // LoadSprite при отсутствии PNG рисует прямоугольник с рамкой 1px,
        // поэтому мелкие круглые элементы (бегунок слайдера, ручка тумблера)
        // собираем процедурно, а не единым спрайтом из кнопки.
        public static Sprite SoftShape(string name, int size, float radiusPx, Color fill, Color stroke, float strokePx)
        {
            Sprite cached;
            if (_softShapeCache.TryGetValue(name, out cached)) return cached;

            int s = Mathf.Clamp(size, 8, 128);
            float radius = Mathf.Clamp(radiusPx, 0.5f, s * 0.5f);
            const float feather = 1f; // один пиксель сглаживания на краю

            Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] cols = new Color[s * s];
            float center = (s - 1) * 0.5f;
            float box = Mathf.Max(0f, center - radius); // скругление только в углах

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float qx = Mathf.Max(Mathf.Abs(x - center) - box, 0f);
                    float qy = Mathf.Max(Mathf.Abs(y - center) - box, 0f);
                    float d = Mathf.Sqrt(qx * qx + qy * qy);
                    if (d >= radius + feather) { cols[y * s + x] = Color.clear; continue; }

                    Color c = fill;
                    if (strokePx > 0.01f)
                    {
                        float t = Mathf.Clamp01((d - (radius - strokePx)) / strokePx);
                        c = Color.Lerp(fill, stroke, t);
                    }

                    c.a *= Mathf.Clamp01(radius + feather - d);
                    cols[y * s + x] = c;
                }
            }

            tex.SetPixels(cols);
            tex.Apply();

            float border = strokePx > 0.01f ? strokePx : 1f;
            Sprite sp = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sp.name = name;
            _softShapeCache[name] = sp;
            return sp;
        }

        static Font LoadFont()
        {
            Font font = Resources.Load<Font>("UI/Fonts/Bahnschrift");
            if (font != null) return font;
            try { font = Font.CreateDynamicFontFromOSFont("Bahnschrift", 14); } catch { }
            if (font != null) return font;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (font != null) return font;
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            return font;
        }

        // ------------------------------------------------------------- Build
        void Build()
        {
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>();
                events.AddComponent<StandaloneInputModule>();
            }

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasGo.GetComponent<RectTransform>();

            // 1. Под-канвас для TopBar (изоляция ребилда при начислении монет)
            RectTransform topBarCanvas = CreateSubCanvas(root, "SubCanvas_TopBar");
            BuildTopBar(topBarCanvas);

            // 2. Под-канвас для баннеров (План дня и Совет директору)
            RectTransform bannerCanvas = CreateSubCanvas(root, "SubCanvas_Banners");
            BuildBanner(bannerCanvas);
            BuildHintCard(bannerCanvas);

            // 3. Под-канвас для сайдбара с карточками (изоляция анимации выезда)
            RectTransform sidebarCanvas = CreateSubCanvas(root, "SubCanvas_Sidebar");
            BuildSidebar(sidebarCanvas);

            // 4. Под-канвас для модальных окон
            RectTransform modalCanvas = CreateSubCanvas(root, "SubCanvas_Modals");
            BuildTable(modalCanvas);
            BuildTutorial(modalCanvas);
            BuildPractice(modalCanvas);
            BuildGameOver(modalCanvas);
            BuildSettings(modalCanvas);

            // Первый запуск: сразу показываем обучение, чтобы игрок
            // не оказался один на один с пустым заводом.
            if (PlayerPrefs.GetInt(TutorialDoneKey, 0) == 0) OpenTutorial();
        }

        static RectTransform CreateSubCanvas(RectTransform parent, string name)
        {
            RectTransform rect = NewRect(parent, name);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            var c = rect.gameObject.AddComponent<Canvas>();
            c.overrideSorting = false;
            rect.gameObject.AddComponent<GraphicRaycaster>();
            return rect;
        }

        // ------------------------------------------------------------- TopBar
        void BuildTopBar(RectTransform root)
        {
            RectTransform bar = NewRect(root, "TopBar");
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -14f);
            bar.sizeDelta = new Vector2(-40f, 48f);
            AddSlicedImage(bar, _sprTopBarBg);

            // Ресурсы слева
            float curX = 18f;
            _money = Stat(bar, "Money", ref curX, 90f, "$ 250", TextWhite, 22, FontStyle.Bold);
            AddDivider(bar, ref curX);

            _day = Stat(bar, "Day", ref curX, 64f, "ДЕНЬ 1", TextWhite, 14, FontStyle.Bold);
            AddDivider(bar, ref curX);

            _plan = Stat(bar, "Plan", ref curX, 72f, "ПЛАН 0/3", AccentCyan, 13, FontStyle.Bold);
            
            // Прогресс-бар плана
            RectTransform track = NewRect(bar, "PlanTrack");
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(0f, 0.5f);
            track.pivot = new Vector2(0f, 0.5f);
            track.anchoredPosition = new Vector2(curX, 0f);
            track.sizeDelta = new Vector2(70f, 6f);
            AddSlicedImage(track, _sprProgressTrack);

            _planProgressBar = NewRect(track, "PlanFill");
            _planProgressBar.anchorMin = new Vector2(0f, 0f);
            _planProgressBar.anchorMax = new Vector2(0f, 1f);
            _planProgressBar.pivot = new Vector2(0f, 0.5f);
            _planProgressBar.anchoredPosition = Vector2.zero;
            _planProgressBar.sizeDelta = new Vector2(0f, 0f);
            Image fillImg = _planProgressBar.gameObject.AddComponent<Image>();
            fillImg.color = AccentCyan;
            fillImg.raycastTarget = false;
            curX += 78f;

            AddDivider(bar, ref curX);

            _phase = Stat(bar, "Phase", ref curX, 92f, "СМЕНА 40.0с", TextMuted, 13, FontStyle.Normal);
            AddDivider(bar, ref curX);

            // Индикатор энергии
            RectTransform dotRect = NewRect(bar, "EnergyDot");
            dotRect.anchorMin = new Vector2(0f, 0.5f);
            dotRect.anchorMax = new Vector2(0f, 0.5f);
            dotRect.pivot = new Vector2(0f, 0.5f);
            dotRect.anchoredPosition = new Vector2(curX, 0f);
            dotRect.sizeDelta = new Vector2(8f, 8f);
            _energyDot = dotRect.gameObject.AddComponent<Image>();
            _energyDot.sprite = _sprStatusDot;
            _energyDot.color = AccentGreen;
            _energyDot.raycastTarget = false;
            curX += 14f;

            _energy = Stat(bar, "Energy", ref curX, 100f, "ЭНЕРГИЯ 6 / 8", AccentGreen, 13, FontStyle.Bold);
            AddDivider(bar, ref curX);

            _materials = Stat(bar, "Materials", ref curX, 136f, "МАТ 0/30 • ТОВ 0/30", TextMuted, 13, FontStyle.Normal);
            AddDivider(bar, ref curX);

            _upkeep = Stat(bar, "Upkeep", ref curX, 130f, "ТОПЛИВО $20/день", TextMuted, 13, FontStyle.Normal);

            // Кнопки справа (строго центрированные)
            Button restart = NewButton(bar, "Restart", "ЗАНОВО", Color.white, 12, _sprBtnRed);
            SetRect(restart.GetComponent<RectTransform>(), new Vector2(-14f, 0f), new Vector2(80f, 32f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            restart.onClick.AddListener(delegate { _game.Restart(); });

            Button table = NewButton(bar, "Table", "ТАБЛИЦА", Color.white, 12, _sprBtnNeutral);
            SetRect(table.GetComponent<RectTransform>(), new Vector2(-102f, 0f), new Vector2(88f, 32f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            table.onClick.AddListener(ToggleTable);
            _tableButtonLabel = table.GetComponentInChildren<Text>();

            Button tutorial = NewButton(bar, "Tutorial", "ОБУЧЕНИЕ", Color.white, 12, _sprBtnNeutral);
            SetRect(tutorial.GetComponent<RectTransform>(), new Vector2(-198f, 0f), new Vector2(90f, 32f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            tutorial.onClick.AddListener(ToggleTutorial);
            _tutorialButtonLabel = tutorial.GetComponentInChildren<Text>();

            Button settings = NewButton(bar, "Settings", "НАСТРОЙКИ", Color.white, 12, _sprBtnNeutral);
            SetRect(settings.GetComponent<RectTransform>(), new Vector2(-380f, 0f), new Vector2(86f, 32f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            settings.onClick.AddListener(OpenSettings);

            // Пауза. Специально рядом с таблицей и обучением — игрок ищет её там же.
            _pauseButton = NewButton(bar, "Pause", "ПАУЗА", Color.white, 12, _sprBtnNeutral);
            SetRect(_pauseButton.GetComponent<RectTransform>(), new Vector2(-290f, 0f), new Vector2(82f, 32f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            _pauseButton.onClick.AddListener(TogglePause);
            _pauseButtonLabel = _pauseButton.GetComponentInChildren<Text>();
        }

        void AddDivider(RectTransform parent, ref float curX)
        {
            RectTransform sep = NewRect(parent, "Sep");
            sep.anchorMin = new Vector2(0f, 0.5f);
            sep.anchorMax = new Vector2(0f, 0.5f);
            sep.pivot = new Vector2(0f, 0.5f);
            sep.anchoredPosition = new Vector2(curX, 0f);
            sep.sizeDelta = new Vector2(1f, 24f);
            AddSlicedImage(sep, _sprDivider);
            curX += 16f;
        }

        Text Stat(RectTransform parent, string name, ref float curX, float width, string value, Color color, int size, FontStyle style)
        {
            Text text = NewText(parent, name, value, size, color, TextAnchor.MiddleLeft);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(curX, 0f);
            rect.sizeDelta = new Vector2(width, 30f);
            text.fontStyle = style;
            curX += width;
            return text;
        }

        // ------------------------------------------------------------- Banners
        void BuildBanner(RectTransform root)
        {
            // Плашка плана дня по центру
            RectTransform planContainer = NewRect(root, "DailyPlanBanner");
            planContainer.anchorMin = new Vector2(0.5f, 1f);
            planContainer.anchorMax = new Vector2(0.5f, 1f);
            planContainer.pivot = new Vector2(0.5f, 1f);
            planContainer.anchoredPosition = new Vector2(0f, -68f);
            planContainer.sizeDelta = new Vector2(560f, 30f);
            AddSlicedImage(planContainer, _sprBannerPlanBg);

            _planDetail = NewText(planContainer, "PlanDetailText", "", 12, new Color(0.56f, 0.81f, 1f), TextAnchor.MiddleCenter);
            _planDetail.rectTransform.anchorMin = Vector2.zero;
            _planDetail.rectTransform.anchorMax = Vector2.one;
            _planDetail.rectTransform.sizeDelta = Vector2.zero;
            _planDetail.rectTransform.anchoredPosition = Vector2.zero;

            // Красная плашка брака. Висит под планом, пока деталь не убрали.
            // Сделал кнопкой: клик по ней = клик по детали в зале.
            _defectButton = NewButton(root, "DefectBanner", "", Color.white, 13, _sprBtnRed);
            SetRect(_defectButton.GetComponent<RectTransform>(), new Vector2(0f, -104f), new Vector2(560f, 30f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            _defectButton.onClick.AddListener(OnDefectClicked);
            _defectButtonLabel = _defectButton.GetComponentInChildren<Text>();
            _defectButton.gameObject.SetActive(false);
        }

        void BuildHintCard(RectTransform root)
        {
            // Плашка «Совет директору» в левом нижнем углу
            _hintCard = NewRect(root, "AdvicePanel").gameObject;
            RectTransform rect = _hintCard.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(24f, 20f);
            rect.sizeDelta = new Vector2(440f, 68f);
            AddSlicedImage(rect, _sprBannerAdviceBg);

            _hintTitle = NewText(rect, "AdviceTitle", "СОВЕТ ДИРЕКТОРУ", 12, AccentAmber, TextAnchor.UpperLeft);
            SetRect(_hintTitle.rectTransform, new Vector2(16f, -12f), new Vector2(370f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _hintTitle.fontStyle = FontStyle.Bold;

            // Кнопка закрытия/скрытия совета
            Button closeBtn = NewButton(rect, "CloseAdviceBtn", "✕", TextMuted, 11, null);
            RectTransform closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);
            closeRect.sizeDelta = new Vector2(22f, 22f);
            Image closeImg = closeBtn.GetComponent<Image>();
            closeImg.color = new Color(0.12f, 0.16f, 0.19f, 0.9f);
            closeBtn.onClick.AddListener(delegate { SetAdviceVisible(false); });

            _hintText = NewText(rect, "AdviceBody", "", 12, TextMuted, TextAnchor.UpperLeft);
            SetRect(_hintText.rectTransform, new Vector2(16f, -32f), new Vector2(408f, 32f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hintText.verticalOverflow = VerticalWrapMode.Truncate;

            // Свернутая кнопка для повторного открытия совета
            _hintMinimizedBtn = NewButton(root, "AdviceMinimizedBtn", "💡 СОВЕТ ДИРЕКТОРУ", AccentAmber, 11, _sprBannerAdviceBg);
            RectTransform minRect = _hintMinimizedBtn.GetComponent<RectTransform>();
            minRect.anchorMin = new Vector2(0f, 0f);
            minRect.anchorMax = new Vector2(0f, 0f);
            minRect.pivot = new Vector2(0f, 0f);
            minRect.anchoredPosition = new Vector2(24f, 20f);
            minRect.sizeDelta = new Vector2(165f, 30f);
            _hintMinimizedBtn.onClick.AddListener(delegate { SetAdviceVisible(true); });
            _hintMinimizedBtn.gameObject.SetActive(false);
        }

        public void SetAdviceVisible(bool visible)
        {
            _adviceVisible = visible;
            if (_hintCard != null) _hintCard.SetActive(_adviceVisible);
            if (_hintMinimizedBtn != null) _hintMinimizedBtn.gameObject.SetActive(!_adviceVisible);
        }

        // ------------------------------------------------------------- Sidebar
        void BuildSidebar(RectTransform root)
        {
            _sidebarPanel = NewRect(root, "SidebarPanel");
            _sidebarPanel.anchorMin = new Vector2(1f, 0f);
            _sidebarPanel.anchorMax = new Vector2(1f, 1f);
            _sidebarPanel.pivot = new Vector2(1f, 1f);
            _sidebarPanel.anchoredPosition = new Vector2(-20f, -74f);
            _sidebarPanel.sizeDelta = new Vector2(SidebarWidth, -94f);
            AddSlicedImage(_sidebarPanel, _sprSidebarBg);

            _sidebarCurrentX = -20f;
            _sidebarTargetX = -20f;

            // Кнопка сворачивания сайдбара
            Button toggleBtn = NewButton(_sidebarPanel, "ToggleCollapse", "", Color.white, 12, _sprSidebarToggle);
            RectTransform toggleRect = toggleBtn.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0f, 0.5f);
            toggleRect.anchorMax = new Vector2(0f, 0.5f);
            toggleRect.pivot = new Vector2(1f, 0.5f);
            toggleRect.anchoredPosition = new Vector2(0f, 0f);
            toggleRect.sizeDelta = new Vector2(36f, 56f);
            toggleBtn.onClick.AddListener(ToggleSidebar);

            // Иконка стрелочки
            RectTransform arrowRect = NewRect(toggleRect, "Arrow");
            arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
            arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(14f, 14f);
            Image arrowImg = arrowRect.gameObject.AddComponent<Image>();
            arrowImg.sprite = _sprArrowCollapse;
            arrowImg.color = AccentCyan;
            arrowImg.raycastTarget = false;
            _sidebarToggleArrow = arrowRect;

            // Точка статуса на кнопке
            RectTransform dotBadge = NewRect(toggleRect, "StatusDot");
            dotBadge.anchorMin = new Vector2(1f, 1f);
            dotBadge.anchorMax = new Vector2(1f, 1f);
            dotBadge.pivot = new Vector2(1f, 1f);
            dotBadge.anchoredPosition = new Vector2(-6f, -6f);
            dotBadge.sizeDelta = new Vector2(6f, 6f);
            Image dotImg = dotBadge.gameObject.AddComponent<Image>();
            dotImg.sprite = _sprStatusDot;
            dotImg.color = AccentGreen;
            dotImg.raycastTarget = false;

            // Вкладки линий
            BuildTabs(_sidebarPanel);

            // Карточки оборудования
            string[] titles = { "ЗАКУПЩИК", "ПРОИЗВОДИТЕЛЬ", "ПРОДАВЕЦ" };
            Color[] colors = { AccentAmber, AccentCyan, AccentGreen };
            float[] tops = { 64f, 154f, 244f };

            for (int i = 0; i < 3; i++)
            {
                MachineKind kind = (MachineKind)i;
                _cards[i] = BuildCard(_sidebarPanel, titles[i], colors[i], 14f, tops[i], 312f, 82f, _sprCardMachineBase,
                    delegate { _game.UpgradeMachine(_selected, kind); });
            }

            _generators[0] = BuildCard(_sidebarPanel, "ГЕНЕРАТОР 1", AccentGreen, 14f, 334f, 312f, 82f, _sprCardMachineBase,
                delegate { _game.UpgradeGenerator(0); });
            _generators[1] = BuildCard(_sidebarPanel, "ГЕНЕРАТОР 2", AccentGreen, 14f, 424f, 312f, 82f, _sprCardMachineBase,
                delegate { _game.UpgradeGenerator(1); });

            for (int i = 0; i < _assemblyCards.Length; i++)
            {
                int index = i;
                _assemblyCards[i] = BuildCard(_sidebarPanel, "СБОРКА · " + Recipes.All[i].Name.ToUpper(),
                    AccentPink, 14f, tops[i], 312f, 82f, _sprCardAssemblyBase,
                    delegate { _game.AssemblyAction(index); });
            }

            _build = BuildCard(_sidebarPanel, "НОВАЯ ЛИНИЯ", AccentPurple, 14f, 514f, 312f, 82f, _sprCardMachineBase,
                delegate { _game.BuildLine(_buildTarget); });
        }

        public void ToggleSidebar()
        {
            _sidebarCollapsed = !_sidebarCollapsed;
            _sidebarTargetX = _sidebarCollapsed ? (SidebarWidth - 4f) : -20f;
        }

        void BuildTabs(RectTransform parent)
        {
            RectTransform tabContainer = NewRect(parent, "TabsContainer");
            tabContainer.anchorMin = new Vector2(0f, 1f);
            tabContainer.anchorMax = new Vector2(1f, 1f);
            tabContainer.pivot = new Vector2(0.5f, 1f);
            tabContainer.anchoredPosition = new Vector2(0f, -14f);
            tabContainer.sizeDelta = new Vector2(-28f, 38f);
            AddSlicedImage(tabContainer, _sprTabContainerBg);

            HorizontalLayoutGroup layout = tabContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 3, 3);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            // Только линии + сборка (настройки перенесены в топбар)
            _tabs = new Button[ProductInfo.Count + 1];

            for (int i = 0; i < _tabs.Length; i++)
            {
                int index = i;
                string label = i < ProductInfo.Count
                    ? ProductInfo.ShortName((ProductKind)i)
                    : "СБОРКА";

                Button tab = NewButton(tabContainer, "Tab " + i, label, Color.white, 12, null);
                Image tabImg = tab.GetComponent<Image>();
                tabImg.type = Image.Type.Sliced;

                tab.onClick.AddListener(delegate { _selected = index; });
                _tabs[i] = tab;
            }
        }

        Card BuildCard(RectTransform parent, string title, Color accent, float x, float top, float width, float height, Sprite bgSprite, UnityEngine.Events.UnityAction onClick)
        {
            var card = new Card { Accent = accent };

            RectTransform rect = NewRect(parent, "Card " + title);
            AddSlicedImage(rect, bgSprite);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
            card.Root = rect;
            card.Background = rect.GetComponent<Image>();

            // Акцентная полоска слева (4px)
            RectTransform stripe = NewRect(rect, "Stripe");
            stripe.anchorMin = new Vector2(0f, 0.5f);
            stripe.anchorMax = new Vector2(0f, 0.5f);
            stripe.pivot = new Vector2(0f, 0.5f);
            stripe.anchoredPosition = new Vector2(4f, 0f);
            stripe.sizeDelta = new Vector2(4f, height - 24f);
            card.Stripe = stripe.gameObject.AddComponent<Image>();
            card.Stripe.sprite = _sprAccentBar;
            card.Stripe.type = Image.Type.Sliced;
            card.Stripe.color = accent;
            card.Stripe.raycastTarget = false;

            // Заголовок карточки
            card.Title = NewText(rect, "Title", title, 13, TextWhite, TextAnchor.UpperLeft);
            SetRect(card.Title.rectTransform, new Vector2(16f, -10f), new Vector2(190f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            card.Title.fontStyle = FontStyle.Bold;

            // Уровень / статус
            card.Level = NewText(rect, "Level", "УР. 1", 11, TextMuted, TextAnchor.UpperRight);
            SetRect(card.Level.rectTransform, new Vector2(-12f, -10f), new Vector2(80f, 18f), new Vector2(1f, 1f), new Vector2(1f, 1f));

            // Характеристики (две строки)
            card.Stats = NewText(rect, "Stats", "", 11, TextMuted, TextAnchor.UpperLeft);
            SetRect(card.Stats.rectTransform, new Vector2(16f, -32f), new Vector2(180f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            card.Power = NewText(rect, "Power", "", 11, TextMuted, TextAnchor.UpperLeft);
            SetRect(card.Power.rectTransform, new Vector2(16f, -50f), new Vector2(180f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            // Кнопка действия (CTA)
            card.Button = NewButton(rect, "ActionBtn", "УЛУЧШИТЬ", Color.white, 11, _sprBtnAmber);
            card.ButtonImage = card.Button.GetComponent<Image>();
            RectTransform btnRect = card.Button.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.anchoredPosition = new Vector2(-10f, -8f);
            btnRect.sizeDelta = new Vector2(98f, 28f);
            card.ButtonLabel = card.Button.GetComponentInChildren<Text>();
            card.Button.onClick.AddListener(onClick);

            return card;
        }

        // ------------------------------------------------------------- Modals
        void BuildTable(RectTransform root)
        {
            _table = NewRect(root, "TableModal").gameObject;
            RectTransform rect = _table.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(760f, 520f);
            AddSlicedImage(rect, _sprSidebarBg);

            Text title = NewText(rect, "TableTitle", "СВОДКА ПРОИЗВОДСТВА", 18, TextWhite, TextAnchor.UpperLeft);
            SetRect(title.rectTransform, new Vector2(24f, -18f), new Vector2(400f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            title.fontStyle = FontStyle.Bold;

            Button close = NewButton(rect, "CloseTable", "ЗАКРЫТЬ", Color.white, 12, _sprBtnNeutral);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(-24f, -18f), new Vector2(90f, 30f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            close.onClick.AddListener(ToggleTable);

            // Табличная сетка
            _rows = new Row[ProductInfo.Count + Recipes.All.Length];
            float startY = 70f;
            float rowHeight = 32f;

            // Заголовок таблицы
            Text header = NewText(rect, "TableHeader", "ЛИНИЯ / УЗЕЛ        СЫРЬЁ        СКЛАД        УРОВНИ        ЦЕНА        СТАТУС", 12, AccentCyan, TextAnchor.UpperLeft);
            SetRect(header.rectTransform, new Vector2(24f, -startY), new Vector2(710f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            header.fontStyle = FontStyle.Bold;
            startY += 26f;

            for (int i = 0; i < _rows.Length; i++)
            {
                Row row = new Row();
                float y = startY + i * rowHeight;

                row.Line = NewText(rect, "Line_" + i, "", 12, TextWhite, TextAnchor.MiddleLeft);
                SetRect(row.Line.rectTransform, new Vector2(24f, -y), new Vector2(140f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                row.Materials = NewText(rect, "Mat_" + i, "", 12, TextMuted, TextAnchor.MiddleLeft);
                SetRect(row.Materials.rectTransform, new Vector2(170f, -y), new Vector2(80f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                row.Products = NewText(rect, "Prod_" + i, "", 12, TextMuted, TextAnchor.MiddleLeft);
                SetRect(row.Products.rectTransform, new Vector2(260f, -y), new Vector2(80f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                row.Levels = NewText(rect, "Lvl_" + i, "", 12, TextMuted, TextAnchor.MiddleLeft);
                SetRect(row.Levels.rectTransform, new Vector2(350f, -y), new Vector2(90f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                row.Price = NewText(rect, "Price_" + i, "", 12, AccentAmber, TextAnchor.MiddleLeft);
                SetRect(row.Price.rectTransform, new Vector2(450f, -y), new Vector2(80f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                row.Status = NewText(rect, "Status_" + i, "", 12, AccentGreen, TextAnchor.MiddleLeft);
                SetRect(row.Status.rectTransform, new Vector2(540f, -y), new Vector2(180f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f));

                _rows[i] = row;
            }

            _tableTotals = NewText(rect, "TableTotals", "", 12, TextWhite, TextAnchor.UpperLeft);
            SetRect(_tableTotals.rectTransform, new Vector2(24f, -310f), new Vector2(710f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            Text logHeader = NewText(rect, "LogHeader", "ЖУРНАЛ СОБЫТИЙ", 12, AccentCyan, TextAnchor.UpperLeft);
            SetRect(logHeader.rectTransform, new Vector2(24f, -360f), new Vector2(300f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            logHeader.fontStyle = FontStyle.Bold;

            _journal = NewText(rect, "Journal", "", 11, TextMuted, TextAnchor.UpperLeft);
            SetRect(_journal.rectTransform, new Vector2(24f, -384f), new Vector2(710f, 110f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _table.SetActive(false);
        }

        void BuildTutorial(RectTransform root)
        {
            _tutorial = NewRect(root, "TutorialModal").gameObject;
            RectTransform rect = _tutorial.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(680f, 660f);
            AddSlicedImage(rect, _sprSidebarBg);

            _tutorialStepBadge = NewText(rect, "Badge", "ШАГ 1 ИЗ 5", 12, AccentAmber, TextAnchor.UpperLeft);
            SetRect(_tutorialStepBadge.rectTransform, new Vector2(24f, -14f), new Vector2(150f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _tutorialStepBadge.fontStyle = FontStyle.Bold;

            Button close = NewButton(rect, "CloseTutorial", "ПРОПУСТИТЬ", Color.white, 12, _sprBtnNeutral);
            SetRect(close.GetComponent<RectTransform>(), new Vector2(-24f, -16f), new Vector2(90f, 30f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            close.onClick.AddListener(ToggleTutorial);

            _tutorialStepTitle = NewText(rect, "Title", "", 18, TextWhite, TextAnchor.UpperLeft);
            SetRect(_tutorialStepTitle.rectTransform, new Vector2(24f, -44f), new Vector2(600f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _tutorialStepTitle.fontStyle = FontStyle.Bold;

            _tutorialSubTitle = NewText(rect, "Sub", "", 12, TextMuted, TextAnchor.UpperLeft);
            SetRect(_tutorialSubTitle.rectTransform, new Vector2(24f, -76f), new Vector2(630f, 28f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _tutorialPoint1Title = NewText(rect, "P1_T", "", 13, AccentCyan, TextAnchor.UpperLeft);
            SetRect(_tutorialPoint1Title.rectTransform, new Vector2(24f, -110f), new Vector2(630f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _tutorialPoint1Title.fontStyle = FontStyle.Bold;
            _tutorialPoint1Desc = NewText(rect, "P1_D", "", 12, TextMuted, TextAnchor.UpperLeft);
            SetRect(_tutorialPoint1Desc.rectTransform, new Vector2(24f, -130f), new Vector2(630f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _tutorialPoint2Title = NewText(rect, "P2_T", "", 13, AccentRed, TextAnchor.UpperLeft);
            SetRect(_tutorialPoint2Title.rectTransform, new Vector2(24f, -180f), new Vector2(630f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _tutorialPoint2Title.fontStyle = FontStyle.Bold;
            _tutorialPoint2Desc = NewText(rect, "P2_D", "", 12, TextMuted, TextAnchor.UpperLeft);
            SetRect(_tutorialPoint2Desc.rectTransform, new Vector2(24f, -200f), new Vector2(630f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _tutorialPoint3Title = NewText(rect, "P3_T", "", 13, AccentPink, TextAnchor.UpperLeft);
            SetRect(_tutorialPoint3Title.rectTransform, new Vector2(24f, -250f), new Vector2(630f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _tutorialPoint3Title.fontStyle = FontStyle.Bold;
            _tutorialPoint3Desc = NewText(rect, "P3_D", "", 12, TextMuted, TextAnchor.UpperLeft);
            SetRect(_tutorialPoint3Desc.rectTransform, new Vector2(24f, -270f), new Vector2(630f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _tutorialPoint4Title = NewText(rect, "P4_T", "", 13, AccentAmber, TextAnchor.UpperLeft);
            SetRect(_tutorialPoint4Title.rectTransform, new Vector2(24f, -320f), new Vector2(630f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _tutorialPoint4Title.fontStyle = FontStyle.Bold;
            _tutorialPoint4Desc = NewText(rect, "P4_D", "", 12, TextMuted, TextAnchor.UpperLeft);
            SetRect(_tutorialPoint4Desc.rectTransform, new Vector2(24f, -340f), new Vector2(630f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _tutorialTipText = NewText(rect, "Tip", "", 12, TextWhite, TextAnchor.UpperLeft);
            SetRect(_tutorialTipText.rectTransform, new Vector2(24f, -390f), new Vector2(630f, 44f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _tutorialPrev = NewButton(rect, "Prev", "НАЗАД", Color.white, 12, _sprBtnNeutral);
            SetRect(_tutorialPrev.GetComponent<RectTransform>(), new Vector2(24f, 20f), new Vector2(90f, 32f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            _tutorialPrev.onClick.AddListener(delegate { SetTutorialStep(_tutorialStep - 1); });

            _tutorialNext = NewButton(rect, "Next", "ДАЛЕЕ", Color.white, 12, _sprBtnAmber);
            SetRect(_tutorialNext.GetComponent<RectTransform>(), new Vector2(-24f, 20f), new Vector2(110f, 32f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            _tutorialNext.onClick.AddListener(OnTutorialNextClicked);
            _tutorialNextLabel = _tutorialNext.GetComponentInChildren<Text>();

            _tutorial.SetActive(false);
            SetTutorialStep(0);
        }

        void BuildPractice(RectTransform root)
        {
            // Плашка практики уходит влево над «Советом», а не в центр снизу:
            // иначе она наезжает на красную бракованную деталь на передней линии.
            _practice = NewRect(root, "PracticePanel").gameObject;
            RectTransform rect = _practice.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(24f, 100f);
            rect.sizeDelta = new Vector2(430f, 104f);
            AddSlicedImage(rect, _sprSidebarBg);

            _practiceBadge = NewText(rect, "PracticeBadge", "ПРАКТИКА · 1 ИЗ 5", 12, AccentAmber, TextAnchor.UpperLeft);
            SetRect(_practiceBadge.rectTransform, new Vector2(18f, -12f), new Vector2(200f, 18f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _practiceBadge.fontStyle = FontStyle.Bold;

            _practiceCounter = NewText(rect, "PracticeCounter", "0 / 5", 12, AccentCyan, TextAnchor.UpperRight);
            SetRect(_practiceCounter.rectTransform, new Vector2(-108f, -12f), new Vector2(90f, 18f), new Vector2(1f, 1f), new Vector2(1f, 1f));

            _practiceSkip = NewButton(rect, "PracticeSkip", "ПРОПУСТИТЬ", Color.white, 11, _sprBtnNeutral);
            SetRect(_practiceSkip.GetComponent<RectTransform>(), new Vector2(-16f, -10f), new Vector2(86f, 26f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            _practiceSkip.onClick.AddListener(SkipPractice);

            _practiceTask = NewText(rect, "PracticeTask", "", 12, TextWhite, TextAnchor.UpperLeft);
            SetRect(_practiceTask.rectTransform, new Vector2(18f, -40f), new Vector2(394f, 56f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            _practice.SetActive(false);
        }

        void BuildGameOver(RectTransform root)
        {
            _gameOver = NewRect(root, "GameOverModal").gameObject;
            RectTransform rect = _gameOver.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(500f, 280f);
            AddSlicedImage(rect, _sprSidebarBg);

            Text title = NewText(rect, "Title", "ЗАВОД ОСТАНОВЛЕН", 20, AccentRed, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, -30f), new Vector2(460f, 30f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            title.fontStyle = FontStyle.Bold;

            _gameOverText = NewText(rect, "Body", "", 13, TextWhite, TextAnchor.MiddleCenter);
            SetRect(_gameOverText.rectTransform, new Vector2(0f, -100f), new Vector2(440f, 90f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            Button restart = NewButton(rect, "RestartBtn", "НАЧАТЬ ЗАНОВО", Color.white, 13, _sprBtnAmber);
            SetRect(restart.GetComponent<RectTransform>(), new Vector2(0f, 30f), new Vector2(180f, 38f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            restart.onClick.AddListener(delegate { _game.Restart(); });

            _gameOver.SetActive(false);
        }

        // Размер модалки настроек и желоб под тонкую полосу прокрутки.
        const float SettingsWidth = 560f;
        const float SettingsHeight = 520f;
        const float SettingsHeaderHeight = 56f;
        const float ScrollbarGutter = 18f; // 6 px полоса + 12 px отступа

        void BuildSettings(RectTransform root)
        {
            _settings = NewRect(root, "SettingsModal").gameObject;
            RectTransform rect = _settings.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(SettingsWidth, SettingsHeight);
            AddSlicedImage(rect, _sprSidebarBg);

            const float pad = 24f;

            // Шапка: заголовок слева, кнопка закрытия справа — как у ТАБЛИЦЫ.
            Text title = NewText(rect, "SettingsTitle", "НАСТРОЙКИ", 20, AccentCyan, TextAnchor.MiddleLeft);
            SetRect(title.rectTransform, new Vector2(pad, -SettingsHeaderHeight * 0.5f), new Vector2(300f, 30f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            title.fontStyle = FontStyle.Bold;

            Button closeBtn = NewButton(rect, "CloseSettings", "ЗАКРЫТЬ", Color.white, 12, _sprBtnNeutral);
            SetRect(closeBtn.GetComponent<RectTransform>(), new Vector2(-pad, -SettingsHeaderHeight * 0.5f), new Vector2(96f, 32f),
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            closeBtn.onClick.AddListener(CloseSettings);

            // Разделитель под шапкой — тот же приём, что у остальных модалок.
            RectTransform headerSep = NewRect(rect, "HeaderSeparator");
            SetRect(headerSep, new Vector2(pad, -SettingsHeaderHeight), new Vector2(-pad * 2f, 1f),
                new Vector2(0f, 1f), new Vector2(0f, 1f));
            Image sepImg = headerSep.gameObject.AddComponent<Image>();
            sepImg.color = ColorBorder;
            sepImg.raycastTarget = false;

            // Прокрутка. Справа оставляем желоб, чтобы полоса не легла на строки.
            GameObject scrollGo = NewRect(rect, "SettingsScrollView").gameObject;
            ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
            RectTransform scrollRectTransform = scrollGo.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.pivot = new Vector2(0.5f, 1f);
            scrollRectTransform.anchoredPosition = Vector2.zero;
            scrollRectTransform.sizeDelta = Vector2.zero;
            scrollRectTransform.offsetMin = new Vector2(pad, 16f);
            scrollRectTransform.offsetMax = new Vector2(-pad - ScrollbarGutter, -SettingsHeaderHeight - 8f);
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.elasticity = 0.1f;

            // Viewport
            GameObject viewport = NewRect(scrollRectTransform, "Viewport").gameObject;
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 1f);
            viewportRect.anchoredPosition = Vector2.zero;
            viewportRect.sizeDelta = Vector2.zero;
            Image viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = new Color(0, 0, 0, 0);
            viewportImg.raycastTarget = true;
            // RectMask2D режет по прямоугольнику без трафарета. Обычный Mask
            // с прозрачной картинкой не писал стенсил, и весь контент пропадал.
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRect;

            // Content container with vertical layout
            _settingsContent = NewRect(viewportRect, "SettingsContent").gameObject;
            RectTransform contentRect = _settingsContent.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = new Vector2(0f, 0f);
            contentRect.sizeDelta = new Vector2(-16f, 300f);

            VerticalLayoutGroup layout = _settingsContent.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = _settingsContent.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // IMPORTANT: ScrollRect needs content assigned
            scrollRect.content = contentRect;

            BuildSettingsScrollbar(rect);
            _settingsScrollRect = scrollRect;

            // Build setting sections
            BuildAudioSection(_settingsContent.transform);
            BuildGraphicsSection(_settingsContent.transform);
            BuildGameplaySection(_settingsContent.transform);
            BuildOtherSection(_settingsContent.transform);

            _settings.SetActive(false);
        }

        // Тонкая полоса прокрутки в цветах игры: узкий трек + cyan-бегунок.
        // Стандартный юнитовский скроллбар в этом окне выглядел чужеродно.
        void BuildSettingsScrollbar(RectTransform parent)
        {
            GameObject sbGo = NewRect(parent, "SettingsScrollbar").gameObject;
            Scrollbar scrollbar = sbGo.AddComponent<Scrollbar>();
            RectTransform sbRect = sbGo.GetComponent<RectTransform>();
            sbRect.anchorMin = new Vector2(1f, 0f);
            sbRect.anchorMax = new Vector2(1f, 1f);
            sbRect.pivot = new Vector2(1f, 0.5f);
            // Полоса по вертикали выровнена по области прокрутки, а не по всей модалке.
            sbRect.anchoredPosition = new Vector2(-28f, -24f);
            sbRect.sizeDelta = new Vector2(6f, -SettingsHeaderHeight - 8f - 24f);
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            // Трек
            Image trackImg = sbGo.AddComponent<Image>();
            trackImg.sprite = null;
            trackImg.color = new Color(0.078f, 0.102f, 0.122f, 0.35f);
            trackImg.raycastTarget = true;

            // Область перетаскивания
            GameObject sliding = NewRect(sbRect, "Sliding Area").gameObject;
            RectTransform slidingRect = sliding.GetComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.sizeDelta = Vector2.zero;
            Image slidingImg = sliding.AddComponent<Image>();
            slidingImg.color = new Color(0, 0, 0, 0);
            slidingImg.raycastTarget = true;

            // Бегунок: полоска accent_bar, 2 px скругления, cyan с альфой.
            GameObject handle = NewRect(slidingRect, "Handle").gameObject;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = new Vector2(1f, 0f);
            handleRect.pivot = new Vector2(0.5f, 0f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(0f, 60f);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.sprite = _sprAccentBar;
            handleImg.type = Image.Type.Sliced;
            handleImg.color = new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.55f);
            handleImg.raycastTarget = true;

            scrollbar.targetGraphic = handleImg;
            scrollbar.handleRect = handleRect;
            _settingsScrollbar = scrollbar;
        }

        void BuildAudioSection(Transform parent)
        {
            // Section header
            GameObject section = CreateSettingSection(parent, "АУДИО");

            // Music volume
            CreateSliderSetting(section.transform, "Громкость музыки", "MusicVolume", 0.5f,
                OnMusicVolumeChanged,
                out _musicSlider, out _musicVolumeText);

            // SFX volume
            CreateSliderSetting(section.transform, "Громкость эффектов", "SfxVolume", 0.7f,
                value => {
                    if (_sfxVolumeText != null) _sfxVolumeText.text = Mathf.RoundToInt(value * 100f) + "%";
                    if (_game.Sim != null && _game.Sim.SfxSource != null)
                        _game.Sim.SfxSource.volume = value;
                    else if (_game != null && _game.SfxSource != null)
                        _game.SfxSource.volume = value;
                    PlayerPrefs.SetFloat("SfxVolume", value);
                    PlayerPrefs.Save();
                },
                out _sfxSlider, out _sfxVolumeText);
        }

        void BuildGraphicsSection(Transform parent)
        {
            GameObject section = CreateSettingSection(parent, "ГРАФИКА");

            CreateToggleSetting(section.transform, "Частицы", "ParticlesEnabled", true,
                value => {
                    PlayerPrefs.SetInt("ParticlesEnabled", value ? 1 : 0);
                    PlayerPrefs.Save();
                });

            CreateDropdownSetting(section.transform, "Качество", "GraphicsQuality",
                new[] { "Низкое", "Среднее", "Высокое" }, 2,
                value => {
                    PlayerPrefs.SetInt("GraphicsQuality", value);
                    PlayerPrefs.Save();
                });

            CreateToggleSetting(section.transform, "VSync", "VSync", true,
                value => {
                    QualitySettings.vSyncCount = value ? 1 : 0;
                    PlayerPrefs.SetInt("VSync", value ? 1 : 0);
                    PlayerPrefs.Save();
                });
        }

        void BuildGameplaySection(Transform parent)
        {
            GameObject section = CreateSettingSection(parent, "ИГРА");

            CreateToggleSetting(section.transform, "Автопауза при потере фокуса", "AutoPauseOnFocusLoss", true,
                value => {
                    PlayerPrefs.SetInt("AutoPauseOnFocusLoss", value ? 1 : 0);
                    PlayerPrefs.Save();
                });

            CreateToggleSetting(section.transform, "Подтверждение выхода", "ConfirmExit", true,
                value => {
                    PlayerPrefs.SetInt("ConfirmExit", value ? 1 : 0);
                    PlayerPrefs.Save();
                });

            CreateSliderSetting(section.transform, "Скорость игры", "GameSpeed", 1.0f,
                value => {
                    Time.timeScale = value;
                    PlayerPrefs.SetFloat("GameSpeed", value);
                    PlayerPrefs.Save();
                },
                out _, out _);
        }

        void BuildOtherSection(Transform parent)
        {
            GameObject section = CreateSettingSection(parent, "ДРУГОЕ");

            // Reset to defaults
            Button resetBtn = CreateButtonSetting(section.transform, "Сбросить настройки", "Сбросить",
                () => {
                    PlayerPrefs.DeleteAll();
                    PlayerPrefs.Save();
                    // Reset UI to defaults
                    if (_musicSlider != null) _musicSlider.value = 0.5f;
                    if (_musicVolumeText != null) _musicVolumeText.text = "50%";
                    if (_sfxSlider != null) _sfxSlider.value = 0.7f;
                    if (_sfxVolumeText != null) _sfxVolumeText.text = "70%";
                    PlayerPrefs.SetFloat("MusicVolume", 0.5f);
                    PlayerPrefs.SetFloat("SfxVolume", 0.7f);
                    if (_game.Sim != null)
                    {
                        if (_game.Sim.MusicSource != null) _game.Sim.MusicSource.volume = 0.5f;
                        if (_game.Sim.SfxSource != null) _game.Sim.SfxSource.volume = 0.7f;
                    }
                    // Also apply directly to FactoryGame's AudioSources
                    if (_game != null)
                    {
                        if (_game.MusicSource != null) _game.MusicSource.volume = 0.5f;
                        if (_game.SfxSource != null) _game.SfxSource.volume = 0.7f;
                    }
                    PlayerPrefs.SetInt("ParticlesEnabled", 1);
                    PlayerPrefs.SetInt("GraphicsQuality", 2);
                    PlayerPrefs.SetInt("VSync", 1);
                    PlayerPrefs.SetInt("AutoPauseOnFocusLoss", 1);
                    PlayerPrefs.SetInt("ConfirmExit", 1);
                    PlayerPrefs.SetFloat("GameSpeed", 1.0f);
                    PlayerPrefs.Save();
                });

            // Version info
            Text versionText = NewText(section.transform, "VersionInfo", "Версия 1.0.0", 11, TextMuted, TextAnchor.MiddleCenter);
            RectTransform vt = versionText.rectTransform;
            vt.anchorMin = new Vector2(0f, 0f);
            vt.anchorMax = new Vector2(1f, 0f);
            vt.pivot = new Vector2(0.5f, 0f);
            vt.anchoredPosition = new Vector2(0f, 8f);
            vt.sizeDelta = new Vector2(0f, 20f);
        }

        GameObject CreateSettingSection(Transform parent, string title)
        {
            GameObject section = NewRect(parent, "Section_" + title).gameObject;
            RectTransform sr = section.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.anchoredPosition = new Vector2(0f, 0f);
            sr.sizeDelta = new Vector2(0f, 0f); // Height will be controlled by layout group

            VerticalLayoutGroup vlg = section.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 8);
            vlg.spacing = 10f;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; // IMPORTANT: Let section expand to fit children
            vlg.childForceExpandHeight = false;

            // Also add ContentSizeFitter so parent (SettingsContent) can get preferred height
            ContentSizeFitter fitter = section.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Section title
            Text sectionTitle = NewText(section.transform, "SectionTitle_" + title, title, 13, AccentAmber, TextAnchor.MiddleLeft);
            RectTransform str = sectionTitle.rectTransform;
            str.anchorMin = new Vector2(0f, 1f);
            str.anchorMax = new Vector2(1f, 1f);
            str.pivot = new Vector2(0f, 1f);
            str.anchoredPosition = Vector2.zero;
            str.sizeDelta = new Vector2(0f, 22f);
            sectionTitle.fontStyle = FontStyle.Bold;
            // Add LayoutElement so layout group knows its height
            LayoutElement titleLayout = sectionTitle.gameObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 22f;

            // Separator
            GameObject sep = NewRect(section.transform, "Separator").gameObject;
            RectTransform spr = sep.GetComponent<RectTransform>();
            spr.anchorMin = new Vector2(0f, 1f);
            spr.anchorMax = new Vector2(1f, 1f);
            spr.pivot = new Vector2(0.5f, 1f);
            spr.anchoredPosition = new Vector2(0f, -28f);
            spr.sizeDelta = new Vector2(0f, 1f);
            Image sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(1f, 1f, 1f, 0.08f);
            sepImg.raycastTarget = false;
            LayoutElement sepLayout = sep.AddComponent<LayoutElement>();
            sepLayout.preferredHeight = 1f;

            return section;
        }

        void CreateSliderSetting(Transform parent, string label, string prefKey, float defaultValue,
            UnityEngine.Events.UnityAction<float> onChange, out Slider slider, out Text valueText)
        {
            GameObject setting = NewRect(parent, "Slider_" + label).gameObject;
            RectTransform r = setting.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(0f, 48f);

            // IMPORTANT: Add LayoutElement so parent VerticalLayoutGroup knows our height
            LayoutElement settingLayout = setting.AddComponent<LayoutElement>();
            settingLayout.preferredHeight = 48f;

            VerticalLayoutGroup vlg = setting.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 4f;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            // Label + value
            GameObject labelRow = NewRect(setting.transform, "LabelRow").gameObject;
            RectTransform lr = labelRow.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0f, 1f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.pivot = new Vector2(0.5f, 1f);
            lr.sizeDelta = new Vector2(0f, 20f);
            LayoutElement lrLayout = labelRow.AddComponent<LayoutElement>();
            lrLayout.preferredHeight = 20f;

            HorizontalLayoutGroup hl = labelRow.AddComponent<HorizontalLayoutGroup>();
            hl.childForceExpandWidth = true;

            Text labelText = NewText(labelRow.transform, "Label", label, 13, TextWhite, TextAnchor.MiddleLeft);
            valueText = NewText(labelRow.transform, "ValueText", Mathf.RoundToInt(defaultValue * 100f) + "%", 13, AccentCyan, TextAnchor.MiddleRight);

            // Slider
            slider = NewRect(setting.transform, "Slider").gameObject.AddComponent<Slider>();
            RectTransform sr = slider.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.sizeDelta = new Vector2(0f, 24f);
            LayoutElement sliderLayout = sr.gameObject.AddComponent<LayoutElement>();
            sliderLayout.preferredHeight = 24f;

            // Fill
            GameObject fillArea = NewRect(sr, "Fill Area").gameObject;
            RectTransform far = fillArea.GetComponent<RectTransform>();
            far.anchorMin = new Vector2(0f, 0.5f);
            far.anchorMax = new Vector2(0.5f, 0.5f);
            far.pivot = new Vector2(0f, 0.5f);
            far.sizeDelta = new Vector2(-10f, 16f);
            far.anchoredPosition = new Vector2(10f, 0f);
            Image fillImg = fillArea.AddComponent<Image>();
            fillImg.sprite = _sprAccentBar;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = AccentCyan;
            slider.fillRect = far;

            // Handle
            GameObject handle = NewRect(sr, "Handle Slide Area/Handle").gameObject;
            RectTransform hr = handle.GetComponent<RectTransform>();
            hr.sizeDelta = new Vector2(20f, 24f);
            Image handleImg = handle.AddComponent<Image>();
            handleImg.sprite = _sprBtnAmber;
            handleImg.type = Image.Type.Sliced;
            slider.handleRect = hr;

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = PlayerPrefs.GetFloat(prefKey, defaultValue);
            slider.onValueChanged.AddListener(onChange);
        }

        void CreateToggleSetting(Transform parent, string label, string prefKey, bool defaultValue, UnityEngine.Events.UnityAction<bool> onChange)
        {
            GameObject setting = NewRect(parent, "Toggle_" + label).gameObject;
            RectTransform r = setting.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(0f, 36f);

            // IMPORTANT: Add LayoutElement so parent VerticalLayoutGroup knows our height
            LayoutElement settingLayout = setting.AddComponent<LayoutElement>();
            settingLayout.preferredHeight = 36f;

            HorizontalLayoutGroup hl = setting.AddComponent<HorizontalLayoutGroup>();
            hl.childForceExpandWidth = true;

            Text labelText = NewText(setting.transform, "Label", label, 13, TextWhite, TextAnchor.MiddleLeft);
            RectTransform ltr = labelText.rectTransform;
            ltr.sizeDelta = new Vector2(0f, 36f);

            Toggle toggle = NewRect(setting.transform, "Toggle").gameObject.AddComponent<Toggle>();
            RectTransform tr = toggle.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(1f, 0.5f);
            tr.anchorMax = new Vector2(1f, 0.5f);
            tr.pivot = new Vector2(1f, 0.5f);
            tr.sizeDelta = new Vector2(52f, 28f);

            Image bg = tr.gameObject.AddComponent<Image>();
            bg.sprite = _sprBtnNeutral;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            toggle.targetGraphic = bg;

            GameObject check = NewRect(tr, "Checkmark").gameObject;
            RectTransform cr = check.GetComponent<RectTransform>();
            // Заполняем сам переключатель, иначе у новой RectTransform
            // остаётся размер 100×100 и галочка вылезает за рамку.
            cr.anchorMin = Vector2.zero;
            cr.anchorMax = Vector2.one;
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = Vector2.zero;
            cr.sizeDelta = new Vector2(-8f, -8f);
            Image checkImg = check.AddComponent<Image>();
            checkImg.sprite = _sprBtnAmber;
            checkImg.type = Image.Type.Sliced;
            checkImg.color = AccentCyan;
            toggle.graphic = checkImg;

            toggle.isOn = PlayerPrefs.GetInt(prefKey, defaultValue ? 1 : 0) == 1;
            toggle.onValueChanged.AddListener(onChange);
        }

        void CreateDropdownSetting(Transform parent, string label, string prefKey, string[] options, int defaultIndex, UnityEngine.Events.UnityAction<int> onChange)
        {
            // Вместо Dropdown — ряд кнопок-вариантов. У Dropdown без шаблона
            // не было ни подписи, ни списка, и он выглядел пустым прямоугольником.
            GameObject setting = NewRect(parent, "Dropdown_" + label).gameObject;
            RectTransform r = setting.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(0f, 36f);

            LayoutElement settingLayout = setting.AddComponent<LayoutElement>();
            settingLayout.preferredHeight = 36f;

            HorizontalLayoutGroup hl = setting.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 6f;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = false;
            hl.childControlHeight = false;
            hl.childForceExpandWidth = false;

            Text labelText = NewText(setting.transform, "Label", label, 13, TextWhite, TextAnchor.MiddleLeft);
            labelText.rectTransform.sizeDelta = new Vector2(110f, 36f);
            LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 110f;
            labelLayout.preferredHeight = 36f;

            int current = PlayerPrefs.GetInt(prefKey, defaultIndex);
            if (current < 0 || current >= options.Length) current = defaultIndex;

            Button[] buttons = new Button[options.Length];
            Text[] labels = new Text[options.Length];

            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                Button b = NewButton(setting.transform, "Option_" + i, options[i], Color.white, 12, _sprBtnNeutral);
                LayoutElement le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = 82f;
                le.preferredHeight = 28f;

                buttons[i] = b;
                labels[i] = b.GetComponentInChildren<Text>();

                b.onClick.AddListener(() =>
                {
                    for (int j = 0; j < labels.Length; j++)
                        if (labels[j] != null) labels[j].color = j == index ? AccentAmber : Color.white;

                    PlayerPrefs.SetInt(prefKey, index);
                    PlayerPrefs.Save();
                    onChange(index);
                });
            }

            for (int j = 0; j < labels.Length; j++)
                if (labels[j] != null) labels[j].color = j == current ? AccentAmber : Color.white;
        }

        Button CreateButtonSetting(Transform parent, string label, string buttonText, UnityEngine.Events.UnityAction onClick)
        {
            GameObject setting = NewRect(parent, "Button_" + label).gameObject;
            RectTransform r = setting.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(0f, 40f);

            // IMPORTANT: Add LayoutElement so parent VerticalLayoutGroup knows our height
            LayoutElement settingLayout = setting.AddComponent<LayoutElement>();
            settingLayout.preferredHeight = 40f;

            HorizontalLayoutGroup hl = setting.AddComponent<HorizontalLayoutGroup>();
            hl.childForceExpandWidth = true;

            Text labelText = NewText(setting.transform, "Label", label, 13, TextWhite, TextAnchor.MiddleLeft);
            RectTransform ltr = labelText.rectTransform;
            ltr.sizeDelta = new Vector2(0f, 40f);

            Button btn = NewRect(setting.transform, "Button").gameObject.AddComponent<Button>();
            RectTransform br = btn.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(1f, 0.5f);
            br.anchorMax = new Vector2(1f, 0.5f);
            br.pivot = new Vector2(1f, 0.5f);
            br.sizeDelta = new Vector2(120f, 32f);

            Image bg = br.gameObject.AddComponent<Image>();
            bg.sprite = _sprBtnNeutral;
            bg.type = Image.Type.Sliced;

            Text btnText = NewText(br, "Text", buttonText, 13, Color.white, TextAnchor.MiddleCenter);
            btn.onClick.AddListener(onClick);

            return btn;
        }

        // Полоса рисуется, только если контент выше окна: иначе в желобе
// висит бегунок без причины.
        void UpdateSettingsScrollbar()
        {
            if (_settingsScrollRect == null || _settingsScrollbar == null) return;
            if (!_settings.activeSelf) return;

            float content = _settingsScrollRect.content != null ? _settingsScrollRect.content.rect.height : 0f;
            float viewport = _settingsScrollRect.viewport != null ? _settingsScrollRect.viewport.rect.height : 0f;
            bool needed = content - viewport > 1f;

            if (_settingsScrollbar.gameObject.activeSelf != needed)
                _settingsScrollbar.gameObject.SetActive(needed);
        }

        void OpenSettings()
        {
            if (_settings != null) _settings.SetActive(true);
            // Закрываем другие модалки
            if (_tutorial != null && _tutorial.activeSelf) CloseTutorial();
            if (_table != null && _table.activeSelf) ToggleTable();
            // Refresh slider values from PlayerPrefs
            RefreshSettingsSliders();
            // Force layout rebuild for settings content
            if (_settingsContent != null)
            {
                _game.Sim.Paused = true;
                Canvas.ForceUpdateCanvases();
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_settingsContent.GetComponent<RectTransform>());
                var scrollRect = _settings.GetComponentInChildren<ScrollRect>();
                if (scrollRect != null)
                {
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
                }
            }
        }

        void RefreshSettingsSliders()
        {
            if (_musicSlider != null)
            {
                _musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
                if (_musicVolumeText != null)
                    _musicVolumeText.text = Mathf.RoundToInt(_musicSlider.value * 100f) + "%";
                ApplyMusicVolume(_musicSlider.value);
            }
            if (_sfxSlider != null)
            {
                _sfxSlider.value = PlayerPrefs.GetFloat("SfxVolume", 0.7f);
                if (_sfxVolumeText != null)
                    _sfxVolumeText.text = Mathf.RoundToInt(_sfxSlider.value * 100f) + "%";
            }
        }

        void CloseSettings()
        {
            if (_settings != null)
            {
                _settings.SetActive(false);
                _game.Sim.Paused = false;
            }
        }

        void OnMusicVolumeChanged(float value)
        {
            if (_musicVolumeText != null)
                _musicVolumeText.text = Mathf.RoundToInt(value * 100f) + "%";

            ApplyMusicVolume(value);

            PlayerPrefs.SetFloat("MusicVolume", value);
            PlayerPrefs.Save();
        }

        // Одна точка применения громкости: и при движении слайдера,
        // и при открытии настроек, и после загрузки музыки.
        void ApplyMusicVolume(float value)
        {
            if (_game == null) return;

            if (_game.MusicSource != null) _game.MusicSource.volume = value;
            if (_game.Sim != null && _game.Sim.MusicSource != null) _game.Sim.MusicSource.volume = value;
        }

        // ------------------------------------------------------------- Logic & Updates
        public bool TutorialOpen
        {
            get { return _tutorial != null && _tutorial.activeSelf; }
        }

        public void TogglePause()
        {
            if (_game.Sim != null) _game.Sim.Paused = !_game.Sim.Paused;
        }

        void OnDefectClicked()
        {
            if (_defectLine >= 0 && _game.Sim.FixDefect(_defectLine))
            {
                _game.PlaySfx(_game.SfxDefectFix);
            }
        }

        public void ToggleTutorial()
        {
            if (_tutorial.activeSelf) CloseTutorial();
            else OpenTutorial();
        }

        // Открыть обучение можно и вручную кнопкой «ОБУЧЕНИЕ».
        // Показ всегда начинается с короткого интро, а не с середины.
        void OpenTutorial()
        {
            CancelPractice();
            _tutorial.SetActive(true);
            SetTutorialStep(0);
            if (_table != null && _table.activeSelf) ToggleTable();
            if (_tutorialButtonLabel != null) _tutorialButtonLabel.text = "СКРЫТЬ";
        }

        void CloseTutorial()
        {
            _tutorial.SetActive(false);
            if (_tutorialButtonLabel != null) _tutorialButtonLabel.text = "ОБУЧЕНИЕ";
            PlayerPrefs.SetInt(TutorialDoneKey, 1);
            PlayerPrefs.Save();
        }

        public void ToggleTable()
        {
            bool open = !_table.activeSelf;
            _table.SetActive(open);
            if (open && _tutorial != null && _tutorial.activeSelf) CloseTutorial();
            if (_tableButtonLabel != null) _tableButtonLabel.text = open ? "СКРЫТЬ" : "ТАБЛИЦА";
        }

        public void Select(int line)
        {
            _selected = line;
        }

        public void Tick()
        {
            FactorySim sim = _game.Sim;
            if (sim == null) return;

            // Esc закрывает окно настроек — как «ЗАКРЫТЬ», только с клавиатуры.
            if (_settings != null && _settings.activeSelf && Input.GetKeyDown(KeyCode.Escape)) CloseSettings();

            // Полоса прокрутки нужна только когда контент не влезает.
            UpdateSettingsScrollbar();

            TickPractice();

            // Плавная анимация сворачивания сайдбара
            if (Mathf.Abs(_sidebarCurrentX - _sidebarTargetX) > 0.1f)
            {
                _sidebarCurrentX = Mathf.Lerp(_sidebarCurrentX, _sidebarTargetX, Time.unscaledDeltaTime * 14f);
                _sidebarPanel.anchoredPosition = new Vector2(_sidebarCurrentX, -74f);
            }

            if (_sidebarToggleArrow != null)
            {
                float targetAngle = _sidebarCollapsed ? 180f : 0f;
                float currentAngle = _sidebarToggleArrow.localEulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, Time.unscaledDeltaTime * 720f);
                _sidebarToggleArrow.localEulerAngles = new Vector3(0f, 0f, newAngle);
            }

            // TopBar значения
            _money.text = "$ " + Mathf.FloorToInt(sim.Money);
            _day.text = "ДЕНЬ " + sim.Day + (sim.Strikes > 0 ? " • ✕" + sim.Strikes : "");
            _day.color = sim.Strikes > 0 ? AccentRed : TextWhite;
            bool paused = sim.Paused || TutorialOpen;
            _phase.text = paused ? "ПАУЗА" : sim.PhaseText;
            _phase.color = paused ? AccentAmber : TextMuted;

            if (_pauseButtonLabel != null)
                _pauseButtonLabel.text = sim.Paused ? "ПРОДОЛЖИТЬ" : "ПАУЗА";

            bool planMet = sim.PlanComplete;
            _plan.text = "ПЛАН " + sim.PlanDoneCount + "/" + sim.Plan.Count;
            _plan.color = planMet ? AccentGreen : (sim.ShiftActive ? AccentCyan : AccentAmber);

            float totalDone = 0f;
            float totalNeed = 0f;
            for (int i = 0; i < sim.Plan.Count; i++)
            {
                var item = sim.Plan[i];
                totalDone += Mathf.Min(sim.DoneFor(item.Part), item.Need);
                totalNeed += item.Need;
            }
            float ratio = totalNeed > 0f ? Mathf.Clamp01(totalDone / totalNeed) : 0f;

            if (_planProgressBar != null)
            {
                _planProgressBar.sizeDelta = new Vector2(70f * ratio, 0f);
                _planProgressBar.gameObject.SetActive(ratio > 0.005f);
            }

            _planDetail.text = "ПЛАН ДНЯ: " + sim.PlanText();
            _planDetail.color = planMet ? AccentGreen : new Color(0.56f, 0.81f, 1f);

            // Ищем активный брак (он всегда один) и показываем красную кнопку.
            _defectLine = -1;
            for (int i = 0; i < sim.Lines.Count; i++)
            {
                if (!sim.Lines[i].Defect) continue;
                _defectLine = i;
                break;
            }

            if (_defectButton != null)
            {
                bool show = _defectLine >= 0;
                if (_defectButton.gameObject.activeSelf != show) _defectButton.gameObject.SetActive(show);

                if (show && _defectButtonLabel != null)
                {
                    LineState bad = sim.Lines[_defectLine];
                    _defectButtonLabel.text = "БРАК: " + bad.Name.ToUpper() + " · " +
                                               Mathf.CeilToInt(bad.DefectLeft) + "с · УБРАТЬ";
                }
            }

            bool energyOk = sim.EnergyOk;
            _energy.text = "ЭНЕРГИЯ " + sim.EnergyUsed + " / " + sim.EnergyCapacity;
            _energy.color = energyOk ? AccentGreen : AccentRed;
            if (_energyDot != null) _energyDot.color = energyOk ? AccentGreen : AccentRed;

            _materials.text = "МАТ " + sim.TotalMaterials + "/" + (ProductInfo.MaterialCap * ProductInfo.Count) +
                              " • ТОВ " + sim.TotalProducts + "/" + (ProductInfo.ProductCap * ProductInfo.Count);
            _upkeep.text = "ТОПЛИВО $" + sim.PendingUpkeep + "/день";

            if (sim.GameOver)
            {
                if (_table.activeSelf) ToggleTable();
                if (_tutorial != null && _tutorial.activeSelf) CloseTutorial();
                CancelPractice();
                _gameOverText.text = "План провален " + sim.Strikes + " раза. Дней продержались: " + (sim.Day - 1) +
                                     ", продано " + sim.TotalSold + " шт.\n" +
                                     "Следующая попытка: держите план, улучшайте машины и открывайте новые линии.";
            }

            if (_gameOver.activeSelf != sim.GameOver) _gameOver.SetActive(sim.GameOver);

            if (_selected < 0 || _selected > sim.Lines.Count) _selected = 0;

            UpdateTabs(sim);
            UpdateCardsVisible(sim);
            if (_selected < sim.Lines.Count) UpdateMachines(sim);
            else UpdateAssemblyCards(sim);
            UpdateGenerators(sim);
            UpdateBuild(sim);
            UpdateTable(sim);
            UpdateHint(sim);
        }

        void UpdateCardsVisible(FactorySim sim)
        {
            bool machines = _selected < sim.Lines.Count;
            bool assembly = _selected == sim.Lines.Count;

            for (int i = 0; i < _cards.Length; i++)
                if (_cards[i].Root != null) _cards[i].Root.gameObject.SetActive(machines);
            for (int i = 0; i < _assemblyCards.Length; i++)
                if (_assemblyCards[i].Root != null) _assemblyCards[i].Root.gameObject.SetActive(assembly);
            if (_build != null && _build.Root != null) _build.Root.gameObject.SetActive(assembly);
        }

        void UpdateTabs(FactorySim sim)
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                bool active = i == _selected;
                Image tabImg = _tabs[i].GetComponent<Image>();
                tabImg.sprite = active ? _sprTabActive : null;
                tabImg.type = Image.Type.Sliced;
                tabImg.color = active ? Color.white : Color.clear;

                Text label = _tabs[i].GetComponentInChildren<Text>();
                if (label != null)
                {
                    if (active)
                    {
                        label.color = TextWhite;
                        label.fontStyle = FontStyle.Bold;
                    }
                    else
                    {
                        label.color = TextMuted;
                        label.fontStyle = FontStyle.Normal;
                    }
                }
            }
        }

        static bool AnyAssemblyBuilt(FactorySim sim)
        {
            for (int i = 0; i < sim.Assembly.Count; i++)
                if (sim.Assembly[i].Built) return true;
            return false;
        }

        void UpdateMachines(FactorySim sim)
        {
            LineState line = sim.Lines[_selected];
            for (int i = 0; i < _cards.Length; i++)
            {
                Card card = _cards[i];
                MachineState machine = line.Machine((MachineKind)i);
                card.Title.text = MachineCaption(i, line.Built);
                card.Level.text = line.Built ? "УР. " + machine.Level : "—";
                card.Power.text = line.Built ? "Мощность: " + machine.EnergyDraw + " ед." : "";

                if (!line.Built)
                {
                    card.Stats.text = "Линия не построена";
                    SetButton(card, false, "НЕДОСТУПНО", false);
                    continue;
                }

                if (machine.Kind == MachineKind.Buyer)
                {
                    card.Stats.text = "Партия: " + machine.BuyBatch + " шт • $" + sim.MaterialPrice + "/шт";
                    card.Power.text = "Цикл: " + machine.BuyInterval.ToString("0.00") + "с";
                }
                else if (machine.Kind == MachineKind.Producer)
                {
                    card.Stats.text = "Расход: " + ProductInfo.Materials(line.Kind) + " мат. • " + machine.CraftTime.ToString("0.00") + "с";
                    card.Power.text = "Склад деталей: " + line.Products + "/" + ProductInfo.ProductCap + " шт.";
                }
                else
                {
                    card.Stats.text = "Цена: $" + sim.SalePrice(line) + " (+$3/ур)";
                    card.Power.text = "Цикл продажи: " + machine.SellInterval.ToString("0.00") + "с";
                }

                string reason;
                bool can = CanUpgradeMachine(sim, line, machine, out reason);
                SetButton(card, can, reason, false);
            }
        }

        static string MachineCaption(int kind, bool built)
        {
            if (!built) return "ЗАКРЫТО";
            switch (kind)
            {
                case 0: return "ЗАКУПЩИК";
                case 1: return "ПРОИЗВОДИТЕЛЬ";
                default: return "ПРОДАВЕЦ";
            }
        }

        void UpdateGenerators(FactorySim sim)
        {
            for (int i = 0; i < _generators.Length && i < sim.Generators.Count; i++)
            {
                Card card = _generators[i];
                GeneratorState gen = sim.Generators[i];
                card.Title.text = "ГЕНЕРАТОР " + (i + 1);
                card.Level.text = "УР. " + gen.Level;
                card.Stats.text = "Ёмкость: +" + gen.Capacity + " энергии • $" + gen.DailyUpkeep + "/д";
                card.Power.text = "Статус: В норме (100%)";

                string reason;
                bool can = CanUpgradeGenerator(sim, gen, out reason);
                SetButton(card, can, reason, false);
            }
        }

        void UpdateAssemblyCards(FactorySim sim)
        {
            for (int i = 0; i < _assemblyCards.Length && i < sim.Assembly.Count; i++)
            {
                AssemblyLine a = sim.Assembly[i];
                Card card = _assemblyCards[i];

                card.Title.text = "СБОРКА · " + a.Recipe.Name.ToUpper();
                card.Level.text = a.Built ? "УР. " + a.Level : "НЕТ";
                card.Power.text = a.Built ? "Мощность: " + a.EnergyDraw + " ед." : "";

                if (!a.Built)
                {
                    card.Stats.text = RecipeLine(a.Recipe) + "\nЦена $" + sim.AssemblyPrice(i) + " • " + a.CraftTime.ToString("0.0") + "с";
                    string reason;
                    SetButton(card, CanBuildAssembly(sim, i, out reason), reason, true);
                    continue;
                }

                card.Stats.text = RecipeLine(a.Recipe) + "\n" +
                                  "Склад " + a.Output + "/" + AssemblyLine.Cap + " • Сделано " + a.Produced + " • $" + sim.AssemblyPrice(i);
                card.Power.text = "Детали: " + a.Hopper[0] + " + " + a.Hopper[1];

                string why;
                SetButton(card, CanUpgradeAssembly(sim, i, out why), why, true);
            }
        }

        static string RecipeLine(Recipe recipe)
        {
            return recipe.AN + " " + Recipes.PartName(recipe.A) + " + " +
                   recipe.BN + " " + Recipes.PartName(recipe.B) + " → " + recipe.Name.ToUpper();
        }

        bool CanBuildAssembly(FactorySim sim, int index, out string reason)
        {
            AssemblyLine a = sim.Assembly[index];
            int cost = sim.BuildAssemblyCost(a);
            if (sim.Money + 0.001f < cost) { reason = "НУЖНО $" + cost; return false; }
            if (sim.EnergyUsed + a.EnergyDraw > sim.EnergyCapacity) { reason = "НЕТ ЭНЕРГИИ"; return false; }

            reason = "ПОСТРОИТЬ $" + cost;
            return true;
        }

        bool CanUpgradeAssembly(FactorySim sim, int index, out string reason)
        {
            AssemblyLine a = sim.Assembly[index];
            if (a.Level >= FactorySim.MaxLevel) { reason = "МАКС. УРОВЕНЬ"; return false; }
            int cost = sim.UpgradeAssemblyCost(a);
            if (sim.Money + 0.001f < cost) { reason = "НУЖНО $" + cost; return false; }
            if (sim.EnergyUsed - a.EnergyDraw + a.NextEnergyDraw > sim.EnergyCapacity) { reason = "НЕТ ЭНЕРГИИ"; return false; }

            reason = "УЛУЧШИТЬ $" + cost;
            return true;
        }

        // БЕЗОПАСНЫЙ UpdateBuild: проверяет границы коллекции, исключая ArgumentOutOfRangeException
        void UpdateBuild(FactorySim sim)
        {
            int target = -1;
            if (_selected < sim.Lines.Count && !sim.Lines[_selected].Built)
            {
                target = _selected;
            }
            else
            {
                for (int i = 0; i < sim.Lines.Count; i++)
                {
                    if (!sim.Lines[i].Built) { target = i; break; }
                }
            }

            if (target < 0)
            {
                _buildTarget = -1;
                _build.Title.text = "ВСЕ ЛИНИИ ПОСТРОЕНЫ";
                _build.Level.text = "";
                _build.Stats.text = "Свободных мест на полу больше нет";
                _build.Power.text = "";
                _build.Button.gameObject.SetActive(false);
                return;
            }

            LineState line = sim.Lines[target];
            _buildTarget = target;
            _build.Button.gameObject.SetActive(true);
            _build.Title.text = "ЛИНИЯ «" + line.Name.ToUpper() + "»";
            _build.Level.text = "ЗАКРЫТО";
            int buildCost = sim.BuildLineCost(line);
            _build.Stats.text = "Стоимость: $" + buildCost + " • Энергия: " + FactorySim.LineEnergy;
            _build.Power.text = "Свободно энергии: " + (sim.EnergyCapacity - sim.EnergyUsed) + " ед.";

            string reason;
            bool ok = true;
            if (sim.Money + 0.001f < buildCost) { reason = "НУЖНО $" + buildCost; ok = false; }
            else if (sim.EnergyUsed + FactorySim.LineEnergy > sim.EnergyCapacity) { reason = "НЕТ ЭНЕРГИИ"; ok = false; }
            else reason = "ПОСТРОИТЬ $" + buildCost;

            _build.Accent = LineColors[target];
            SetButton(_build, ok, reason, false);
        }

        bool CanUpgradeMachine(FactorySim sim, LineState line, MachineState machine, out string reason)
        {
            if (machine.Level >= FactorySim.MaxLevel) { reason = "МАКС. УР."; return false; }
            int cost = sim.UpgradeMachineCost(machine);
            if (sim.Money + 0.001f < cost) { reason = "НУЖНО $" + cost; return false; }

            int newUsed = sim.EnergyUsed - machine.EnergyDraw + machine.NextEnergyDraw;
            if (newUsed > sim.EnergyCapacity) { reason = "НЕТ ЭНЕРГИИ"; return false; }

            reason = "УЛУЧШИТЬ $" + cost;
            return true;
        }

        bool CanUpgradeGenerator(FactorySim sim, GeneratorState generator, out string reason)
        {
            if (generator.Level >= FactorySim.MaxLevel) { reason = "МАКС. УР."; return false; }
            int cost = sim.UpgradeGeneratorCost(generator);
            if (sim.Money + 0.001f < cost) { reason = "НУЖНО $" + cost; return false; }

            reason = "УЛУЧШИТЬ $" + cost;
            return true;
        }

        void SetButton(Card card, bool enabled, string label, bool isPink)
        {
            card.Button.interactable = enabled;
            card.ButtonLabel.text = label;

            if (enabled)
            {
                card.ButtonImage.sprite = isPink ? _sprBtnPink : _sprBtnAmber;
                card.ButtonLabel.color = isPink ? TextWhite : ColorSurface0;
            }
            else
            {
                card.ButtonImage.sprite = _sprBtnDisabled;
                card.ButtonLabel.color = TextMuted;
            }
        }

        void UpdateTable(FactorySim sim)
        {
            if (!_table.activeSelf) return;

            var journal = new System.Text.StringBuilder();
            int start = Mathf.Max(0, sim.Log.Count - 5);
            for (int i = start; i < sim.Log.Count; i++)
            {
                journal.Append("• ").Append(sim.Log[i]);
                if (i < sim.Log.Count - 1) journal.Append('\n');
            }
            _journal.text = journal.ToString();

            for (int i = 0; i < ProductInfo.Count; i++)
            {
                LineState line = sim.Lines[i];
                Row row = _rows[i];
                row.Line.text = ProductInfo.ShortName(line.Kind);
                row.Materials.text = line.Built ? line.Materials + "/" + ProductInfo.MaterialCap : "—";
                row.Products.text = line.Built ? line.Products + "/" + ProductInfo.ProductCap : "—";
                row.Levels.text = line.Built ? line.Buyer.Level + "/" + line.Producer.Level + "/" + line.Seller.Level : "—";
                row.Price.text = line.Built ? "$" + sim.SalePrice(line) : "—";

                if (!line.Built)
                {
                    row.Status.text = "Линия закрыта";
                    row.Status.color = TextMuted;
                }
                else if (!sim.EnergyOk)
                {
                    row.Status.text = "НЕТ ЭНЕРГИИ";
                    row.Status.color = AccentRed;
                }
                else if (!sim.ShiftActive)
                {
                    row.Status.text = sim.Skipped ? "ПРОПУСК" : "ПЕРЕРЫВ";
                    row.Status.color = AccentAmber;
                }
                else
                {
                    row.Status.text = RowState(line);
                    row.Status.color = AccentGreen;
                }
            }

            for (int i = 0; i < sim.Assembly.Count; i++)
            {
                AssemblyLine a = sim.Assembly[i];
                Row row = _rows[ProductInfo.Count + i];
                row.Line.text = a.Built ? a.Name + " • сборка" : a.Name + " • нет";
                row.Line.color = a.Built ? AccentPink : TextMuted;
                row.Materials.text = a.Built ? a.Hopper[0] + "/" + a.Recipe.AN + " • " + a.Hopper[1] + "/" + a.Recipe.BN : "—";
                row.Products.text = a.Built ? a.Output + "/" + AssemblyLine.Cap : "—";
                row.Levels.text = a.Built ? "Сб " + a.Level + " • " + a.Produced + " шт." : "—";
                row.Price.text = "$" + sim.AssemblyPrice(i);

                if (!a.Built)
                {
                    row.Status.text = "НЕ ПОСТРОЕНА";
                    row.Status.color = TextMuted;
                }
                else if (!sim.EnergyOk)
                {
                    row.Status.text = "НЕТ ЭНЕРГИИ";
                    row.Status.color = AccentRed;
                }
                else if (!sim.ShiftActive)
                {
                    row.Status.text = sim.Skipped ? "ПРОПУСК" : "ПЕРЕРЫВ";
                    row.Status.color = AccentAmber;
                }
                else
                {
                    row.Status.text = a.Status;
                    row.Status.color = AccentGreen;
                }
            }

            _tableTotals.text =
                "Куплено " + sim.TotalBought + " мат. • Произведено " + sim.TotalProduced + " шт. • Продано " + sim.TotalSold + " шт.\n" +
                "Выручка $" + Mathf.FloorToInt(sim.TotalRevenue) + " • Расходы $" + Mathf.FloorToInt(sim.TotalSpent) +
                " • Субсидии $" + Mathf.FloorToInt(sim.TotalSubsidy) + " • Энергия " + sim.EnergyUsed + "/" + sim.EnergyCapacity + "\n" +
                "Деталей: " + sim.Lines[0].Produced + " шест. • " + sim.Lines[1].Produced + " корп. • " + sim.Lines[2].Produced + " микр.\n" +
                "Собрано: " + sim.Assembly[0].Produced + " ред. • " + sim.Assembly[1].Produced + " прив. • " + sim.Assembly[2].Produced + " турб.";
        }

        static string RowState(LineState line)
        {
            if (line.Products >= ProductInfo.ProductCap) return "СКЛАД ПОЛОН";
            if (line.Materials < ProductInfo.Materials(line.Kind)) return "ЖДЁТ МАТЕРИАЛ";
            return "РАБОТА";
        }

        void UpdateHint(FactorySim sim)
        {
            if (!_adviceVisible || _hintCard == null || !_hintCard.activeSelf) return;

            if (_selected == 3)
            {
                _hintTitle.text = "СОВЕТ ДИРЕКТОРУ: СБОРКА";
                _hintText.text = "Посты сборки берут детали со складов и создают ценные узлы (Редукторы, Приводы, Турбины).";
                return;
            }

            _hintTitle.text = "СОВЕТ ДИРЕКТОРУ";
            LineState line = sim.Lines[_selected];
            if (!line.Built)
            {
                _hintText.text = "Откройте линию «" + line.Name + "» для роста оборота. Нужна энергия генератора.";
            }
            else if (line.Materials == 0 && line.Buyer.Level <= line.Producer.Level)
            {
                _hintText.text = "Не хватает сырья для станков. Улучшите «Закупщика».";
            }
            else if (line.Products >= ProductInfo.ProductCap)
            {
                _hintText.text = "Склад деталей переполнен! Улучшите «Продавца» для сбыта.";
            }
            else if (sim.EnergyCapacity - sim.EnergyUsed < 2)
            {
                _hintText.text = "Энергия на исходе. Улучшите генератор, иначе станки остановятся.";
            }
            else
            {
                _hintText.text = "Смотрите на «ПЛАН ДНЯ» вверху: выполняйте позиции плана, чтобы получать крупные субсидии.";
            }
        }

        // Интро короткое: одно окно, без листания. Дальше игрок сразу
        // делает пять действий руками — это и есть обучение.
        void SetTutorialStep(int step)
        {
            _tutorialStep = 0;
            _tutorialStepBadge.text = "ОБУЧЕНИЕ · МИНУТА ДВЕ";
            _tutorialPrev.gameObject.SetActive(false);
            _tutorialNextLabel.text = "ПОПРОБУЕМ";

            _tutorialStepTitle.text = "ПРИВЕТ, НОВЫЙ СМЕННЫЙ";
            _tutorialSubTitle.text = "Я покажу основы за пару минут. Потом — практика на живом заводе, смена не уйдёт.";

            _tutorialPoint1Title.text = "Пауза — твой лучший друг";
            _tutorialPoint1Desc.text = "Кнопка «ПАУЗА» вверху (или пробел) останавливает смену. Нужно подумать, отвлечься, полить цветок — жми. Ещё раз — и завод снова работает.";

            _tutorialPoint2Title.text = "Брак не ждёт";
            _tutorialPoint2Desc.text = "На складе иногда всплывает красная деталь. 12 секунд — и она испортит партию. Кликни по ней в цехе или по красной плашке сверху. Быстрее — лучше.";

            _tutorialPoint3Title.text = "Станки растут — завод растёт";
            _tutorialPoint3Desc.text = "В сайдбаре карточки линий. Жми кнопку улучшения — закупщик, производитель или продавец поднимут уровень. Дороже — но быстрее и больше денег.";

            _tutorialPoint4Title.text = "Сборка — деньги побольше";
            _tutorialPoint4Desc.text = "Вкладка «СБОРКА» над карточками. Там из готовых деталей делают редукторы, приводы, турбины. Выгоднее, чем продавать поштучно.";

            _tutorialTipText.text = "Практика: пауза → убрать брак → улучшить закупщика → открыть сборку → построить линию. Смена стоит, спешить некуда.";
        }

        void OnTutorialNextClicked()
        {
            StartPractice();
        }

        // ------------------------------------------------------------- Practice
        void StartPractice()
        {
            FactorySim sim = _game.Sim;
            if (sim == null || sim.GameOver)
            {
                CloseTutorial();
                return;
            }

            CloseTutorial();
            _practice.SetActive(true);

            // Смена не идёт, случайный брак не появляется: игрок учится без давления.
            sim.Paused = false;
            sim.TutorialMode = true;
            sim.ClearTutorialDefects();
            BeginPracticeStep(0);
        }

        void BeginPracticeStep(int step)
        {
            _practiceStep = step;
            _practiceBadge.text = "ПРАКТИКА · ШАГ " + (step + 1) + " ИЗ 5";
            _practiceCounter.text = step + " / 5";

            FactorySim sim = _game.Sim;

            switch (step)
            {
                case 0:
                    _practicePausePressed = false;
                    _practiceTask.text = "Жми «ПАУЗУ» вверху (или пробел). Завод встанет — проверь, как работает.";
                    _practiceTask.color = TextWhite;
                    break;

                case 1:
                    _practiceTask.text = "Появился брак — красная деталь на складе. Кликни по ней в цехе или по плашке сверху. 12 секунд на реакцию.";
                    _practiceTask.color = AccentRed;
                    if (sim != null && !sim.Lines[0].Defect) sim.SpawnTutorialDefect(0);
                    break;

                case 2:
                    _practiceTask.text = "Улучши закупщика на линии «Шестерёнки». Карточка в сайдбаре, кнопка улучшения — тыкни.";
                    _practiceTask.color = AccentCyan;
                    if (sim != null) _practiceBuyerLevel = sim.Lines[0].Buyer.Level;
                    break;

                case 3:
                    _practiceTask.text = "Открой вкладку «СБОРКА» над карточками. Там редукторы, приводы, турбины — денег больше.";
                    _practiceTask.color = AccentPink;
                    break;

                case 4:
                    _practiceTask.text = "Построй линию «Корпуса» (вторая карточка). Кнопка постройки — и новая линия заработает.";
                    _practiceTask.color = AccentAmber;
                    break;
            }
        }

        void TickPractice()
        {
            if (_practiceStep < 0) return;

            FactorySim sim = _game.Sim;
            if (sim == null || sim.GameOver)
            {
                CancelPractice();
                return;
            }

            switch (_practiceStep)
            {
                case 0:
                    // Нужно именно нажать паузу, а потом снять её.
                    if (!_practicePausePressed && sim.Paused)
                    {
                        _practicePausePressed = true;
                        _practiceTask.text = "Пауза работает. Теперь жми «ПРОДОЛЖИТЬ», чтобы вернуть смену.";
                        _practiceTask.color = AccentGreen;
                    }
                    else if (_practicePausePressed && !sim.Paused)
                    {
                        CompletePracticeStep();
                    }
                    break;

                case 1:
                    // Учебный брак исчезает только после клика игрока.
                    if (!sim.Lines[0].Defect) CompletePracticeStep();
                    break;

                case 2:
                    if (sim.Lines[0].Buyer.Level > _practiceBuyerLevel) CompletePracticeStep();
                    break;

                case 3:
                    if (_selected == sim.Lines.Count) CompletePracticeStep();
                    break;

                case 4:
                    // Вторая линия (Корпуса) построена
                    if (sim.Lines[1].Built) CompletePracticeStep();
                    break;
            }
        }

        void CompletePracticeStep()
        {
            int done = _practiceStep + 1;
            if (done >= 5)
            {
                CompletePractice();
                return;
            }

            _practiceCounter.text = done + " / 5";
            BeginPracticeStep(done);
        }

        void CompletePractice()
        {
            FactorySim sim = _game.Sim;
            if (sim != null)
            {
                sim.TutorialMode = false;
                sim.Paused = false;
                sim.ClearTutorialDefects();
                sim.SetEvent("Обучение пройдено: пауза, брак, улучшение, сборка и новая линия — ты готов", EventTone.Good);
            }

            FinishPractice();
        }

        void SkipPractice()
        {
            FactorySim sim = _game.Sim;
            if (sim != null)
            {
                sim.TutorialMode = false;
                sim.ClearTutorialDefects();
            }

            FinishPractice();
        }

        // Отмена без «молодца»: игрок открыл таблицу или обучение заново.
        void CancelPractice()
        {
            if (_practiceStep < 0 && (_practice == null || !_practice.activeSelf)) return;

            FactorySim sim = _game.Sim;
            if (sim != null)
            {
                sim.TutorialMode = false;
                sim.ClearTutorialDefects();
            }

            FinishPractice();
        }

        void FinishPractice()
        {
            _practiceStep = -1;
            if (_practice != null) _practice.SetActive(false);

            // Больше не открываем интро само при каждом запуске.
            PlayerPrefs.SetInt(TutorialDoneKey, 1);
            PlayerPrefs.Save();
        }

        // debug: один вход на все действия практики, чтобы её можно было
        // прогнать из консоли без мыши.
        public void DebugTutorialAction(string action)
        {
            switch (action)
            {
                case "start":
                    OpenTutorial();
                    StartPractice();
                    break;
                case "pause":
                    if (_game.Sim != null) _game.Sim.Paused = !_game.Sim.Paused;
                    break;
                case "fix":
                    if (_game.Sim != null)
                        for (int i = 0; i < _game.Sim.Lines.Count; i++)
                            if (_game.Sim.Lines[i].Defect) _game.Sim.FixDefect(i);
                    break;
                case "buyer":
                    _game.UpgradeMachine(0, MachineKind.Buyer);
                    break;
                case "assembly":
                    Select(_game.Sim.Lines.Count);
                    break;
            }
        }

        // ------------------------------------------------------------- Helpers
        static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void AddSlicedImage(RectTransform rect, Sprite sprite)
        {
            Image img = rect.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;
        }

        static Text NewText(Transform parent, string name, string value, int size, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            return text;
        }

        static Button NewButton(Transform parent, string name, string label, Color textColor, int fontSize, Sprite background)
        {
            RectTransform rect = NewRect(parent, name);
            Image image = rect.gameObject.AddComponent<Image>();
            if (background != null)
            {
                image.sprite = background;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = Color.clear;
            }
            image.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();

            // Текст строго по центру кнопки
            GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(rect, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            Text text = textGo.GetComponent<Text>();
            text.font = _font;
            text.text = label;
            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            return button;
        }

        static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
