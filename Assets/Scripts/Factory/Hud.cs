using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Factory
{
    public class Hud
    {
        class Card
        {
            public RectTransform Root;
            public Text Title;
            public Text Level;
            public Text Stats;
            public Text Power;
            public Text ButtonLabel;
            public Button Button;
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

        readonly FactoryGame _game;

        Text _money;
        Text _day;
        Text _plan;
        Text _phase;
        Text _energy;
        Text _materials;
        Text _upkeep;
        Text _banner;
        Text _tableTotals;
        Text _journal;
        Text _gameOverText;
        GameObject _gameOver;

        readonly Card[] _cards = new Card[3];
        readonly Card[] _generators = new Card[2];
        Card _build;
        Button[] _tabs;
        Row[] _rows;

        GameObject _table;
        Text _tableButtonLabel;

        GameObject _tutorial;
        Text _tutorialButtonLabel;
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
        Text _tutorialTipText;
        Button _tutorialPrev;
        Button _tutorialNext;
        Text _tutorialNextLabel;
        Button[] _tutorialStepTabs;

        GameObject _hintCard;
        Text _hintText;

        int _selected;

        static Font _font;
        static readonly Color Panel = new Color(0.075f, 0.085f, 0.11f, 0.94f);
        static readonly Color CardColor = new Color(0.12f, 0.135f, 0.17f, 0.97f);
        static readonly Color TextColor = new Color(0.88f, 0.91f, 0.95f);
        static readonly Color Muted = new Color(0.60f, 0.65f, 0.73f);
        static readonly Color Dim = new Color(0.20f, 0.22f, 0.27f);

        static readonly Color[] LineColors =
        {
            new Color(0.98f, 0.74f, 0.28f),
            new Color(0.30f, 0.82f, 0.86f),
            new Color(0.72f, 0.56f, 0.98f)
        };

        static readonly Color BuyerColor = new Color(0.98f, 0.62f, 0.22f);
        static readonly Color ProducerColor = new Color(0.35f, 0.78f, 0.98f);
        static readonly Color SellerColor = new Color(0.55f, 0.90f, 0.45f);
        static readonly Color GeneratorColor = new Color(0.99f, 0.85f, 0.30f);

        public Hud(FactoryGame game)
        {
            _game = game;
            _font = LoadFont();
            Build();
        }

        static Font LoadFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { }
            if (font != null) return font;
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch { }
            return font;
        }

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

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasGo.GetComponent<RectTransform>();

            BuildTopBar(root);
            BuildBanner(root);
            BuildSidebar(root);
            BuildTable(root);
            BuildTutorial(root);
            BuildHintCard(root);
            BuildGameOver(root);
        }

        void BuildTopBar(RectTransform root)
        {
            RectTransform bar = NewRect(root, "Top Bar");
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, 74f);
            AddImage(bar, Panel);

            _money = Stat(bar, "Money", 20f, 150f, "$ 250", new Color(1f, 0.83f, 0.33f), 30, FontStyle.Bold);
            _day = Stat(bar, "Day", 175f, 130f, "ДЕНЬ 1", TextColor, 23, FontStyle.Bold);
            _plan = Stat(bar, "Plan", 310f, 270f, "ПЛАН $0 / $200", new Color(0.85f, 0.90f, 1f), 21, FontStyle.Bold);
            _phase = Stat(bar, "Phase", 585f, 170f, "СМЕНА 40.0с", new Color(0.75f, 0.85f, 1f), 21, FontStyle.Normal);
            _energy = Stat(bar, "Energy", 760f, 180f, "ЭНЕРГИЯ 6 / 8", new Color(0.55f, 0.9f, 0.6f), 23, FontStyle.Bold);
            _materials = Stat(bar, "Materials", 945f, 220f, "МАТ 0/30 · ТОВ 0/30", TextColor, 20, FontStyle.Normal);
            _upkeep = Stat(bar, "Upkeep", 1170f, 220f, "ТОПЛИВО $20/день", new Color(0.95f, 0.78f, 0.42f), 20, FontStyle.Normal);

            Button tutorial = NewButton(bar, "Tutorial", "ОБУЧЕНИЕ", new Color(0.24f, 0.62f, 0.55f), 17);
            RectTransform tutRect = tutorial.GetComponent<RectTransform>();
            tutRect.anchorMin = new Vector2(1f, 0.5f);
            tutRect.anchorMax = new Vector2(1f, 0.5f);
            tutRect.pivot = new Vector2(1f, 0.5f);
            tutRect.anchoredPosition = new Vector2(-332f, 0f);
            tutRect.sizeDelta = new Vector2(150f, 42f);
            tutorial.onClick.AddListener(ToggleTutorial);
            _tutorialButtonLabel = tutorial.GetComponentInChildren<Text>();

            Button table = NewButton(bar, "Table", "ТАБЛИЦА", new Color(0.28f, 0.45f, 0.72f), 17);
            RectTransform tableRect = table.GetComponent<RectTransform>();
            tableRect.anchorMin = new Vector2(1f, 0.5f);
            tableRect.anchorMax = new Vector2(1f, 0.5f);
            tableRect.pivot = new Vector2(1f, 0.5f);
            tableRect.anchoredPosition = new Vector2(-168f, 0f);
            tableRect.sizeDelta = new Vector2(150f, 42f);
            table.onClick.AddListener(ToggleTable);
            _tableButtonLabel = table.GetComponentInChildren<Text>();

            Button restart = NewButton(bar, "Restart", "ЗАНОВО", new Color(0.32f, 0.35f, 0.42f), 17);
            RectTransform rect = restart.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-14f, 0f);
            rect.sizeDelta = new Vector2(140f, 42f);
            restart.onClick.AddListener(delegate { _game.Restart(); });
        }

        Text Stat(RectTransform parent, string name, float x, float width, string value, Color color, int size, FontStyle style)
        {
            Text text = NewText(parent, name, value, size, color, TextAnchor.MiddleLeft);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 0f);
            text.fontStyle = style;
            return text;
        }

        void BuildBanner(RectTransform root)
        {
            _banner = NewText(root, "Banner", "", 25, TextColor, TextAnchor.MiddleCenter);
            RectTransform rect = _banner.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(-180f, -84f);
            rect.sizeDelta = new Vector2(1200f, 48f);
            _banner.fontStyle = FontStyle.Bold;
        }

        void BuildSidebar(RectTransform root)
        {
            RectTransform panel = NewRect(root, "Sidebar");
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-16f, -136f);
            panel.sizeDelta = new Vector2(400f, 866f);
            AddImage(panel, Panel);

            BuildTabs(panel);

            string[] titles = { "ЗАКУПЩИК", "ПРОИЗВОДИТЕЛЬ", "ПРОДАВЕЦ" };
            Color[] colors = { BuyerColor, ProducerColor, SellerColor };
            float[] tops = { 62f, 190f, 338f };
            float[] heights = { 120f, 140f, 120f };

            for (int i = 0; i < 3; i++)
            {
                MachineKind kind = (MachineKind)i;
                _cards[i] = BuildCard(panel, titles[i], colors[i], 12f, tops[i], 376f, heights[i],
                    delegate { _game.UpgradeMachine(_selected, kind); });
            }

            _generators[0] = BuildCard(panel, "ГЕНЕРАТОР 1", GeneratorColor, 12f, 466f, 376f, 108f,
                delegate { _game.UpgradeGenerator(0); });
            _generators[1] = BuildCard(panel, "ГЕНЕРАТОР 2", GeneratorColor, 12f, 582f, 376f, 108f,
                delegate { _game.UpgradeGenerator(1); });

            _build = BuildCard(panel, "НОВАЯ ЛИНИЯ", new Color(0.50f, 0.72f, 0.98f), 12f, 698f, 376f, 108f,
                delegate { _game.BuildLine(_selected); });
        }

        void BuildTabs(RectTransform parent)
        {
            _tabs = new Button[ProductInfo.Count];
            float width = 118f;

            for (int i = 0; i < ProductInfo.Count; i++)
            {
                int index = i;
                Button tab = NewButton(parent, "Tab " + i, ProductInfo.ShortName((ProductKind)i), Dim, 16);
                RectTransform rect = tab.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(12f + i * (width + 8f), -12f);
                rect.sizeDelta = new Vector2(width, 38f);
                tab.onClick.AddListener(delegate { _selected = index; });
                _tabs[i] = tab;
            }
        }

        Card BuildCard(RectTransform parent, string title, Color accent, float x, float top, float width, float height, UnityEngine.Events.UnityAction onClick)
        {
            var card = new Card { Accent = accent };

            RectTransform rect = NewRect(parent, "Card " + title);
            AddImage(rect, CardColor);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
            card.Root = rect;

            RectTransform stripe = NewRect(rect, "Stripe");
            stripe.anchorMin = new Vector2(0f, 0f);
            stripe.anchorMax = new Vector2(0f, 1f);
            stripe.pivot = new Vector2(0f, 0.5f);
            stripe.anchoredPosition = Vector2.zero;
            stripe.sizeDelta = new Vector2(5f, 0f);
            AddImage(stripe, accent);

            card.Title = NewText(rect, "Title", title, 20, accent, TextAnchor.UpperLeft);
            SetRect(card.Title.rectTransform, new Vector2(16f, -8f), new Vector2(260f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            card.Title.fontStyle = FontStyle.Bold;

            card.Level = NewText(rect, "Level", "Lv 1", 18, TextColor, TextAnchor.UpperRight);
            SetRect(card.Level.rectTransform, new Vector2(-12f, -8f), new Vector2(110f, 26f), new Vector2(1f, 1f), new Vector2(1f, 1f));

            card.Stats = NewText(rect, "Stats", "", 15, Muted, TextAnchor.UpperLeft);
            SetRect(card.Stats.rectTransform, new Vector2(16f, -32f), new Vector2(346f, 40f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            card.Stats.horizontalOverflow = HorizontalWrapMode.Wrap;
            card.Stats.verticalOverflow = VerticalWrapMode.Overflow;

            card.Power = NewText(rect, "Power", "", 15, new Color(0.75f, 0.82f, 0.92f), TextAnchor.UpperLeft);
            SetRect(card.Power.rectTransform, new Vector2(16f, -72f), new Vector2(190f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            card.Button = NewButton(rect, "Upgrade", "УЛУЧШИТЬ", accent, 15);
            RectTransform buttonRect = card.Button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.anchoredPosition = new Vector2(-10f, 8f);
            buttonRect.sizeDelta = new Vector2(160f, 32f);
            card.ButtonLabel = card.Button.GetComponentInChildren<Text>();
            card.Button.onClick.AddListener(onClick);
            return card;
        }

        void BuildTable(RectTransform root)
        {
            _table = NewRect(root, "Table").gameObject;
            RectTransform shade = _table.GetComponent<RectTransform>();
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = Vector2.zero;
            shade.offsetMax = Vector2.zero;
            AddImage(shade, new Color(0.02f, 0.03f, 0.05f, 0.72f));

            RectTransform panel = NewRect(shade, "Table Panel");
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(920f, 500f);
            AddImage(panel, new Color(0.09f, 0.10f, 0.13f, 0.99f));

            Text title = NewText(panel, "Title", "МАТЕРИАЛЫ И ПРОДУКЦИЯ", 22, TextColor, TextAnchor.UpperLeft);
            SetRect(title.rectTransform, new Vector2(24f, -18f), new Vector2(500f, 30f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            title.fontStyle = FontStyle.Bold;

            Button close = NewButton(panel, "Close", "ЗАКРЫТЬ", new Color(0.32f, 0.35f, 0.42f), 15);
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-18f, -16f);
            closeRect.sizeDelta = new Vector2(140f, 34f);
            close.onClick.AddListener(ToggleTable);

            string[] heads = { "ЛИНИЯ", "МАТЕРИАЛЫ", "ПРОДУКТЫ", "УРОВНИ", "ЦЕНА", "СТАТУС" };
            float[] xs = { 24f, 220f, 340f, 460f, 660f, 760f };
            float[] widths = { 190f, 115f, 115f, 195f, 95f, 120f };

            for (int i = 0; i < heads.Length; i++)
            {
                Text head = NewText(panel, "Head " + i, heads[i], 15, new Color(0.55f, 0.72f, 0.95f), TextAnchor.UpperLeft);
                SetRect(head.rectTransform, new Vector2(xs[i], -62f), new Vector2(widths[i], 22f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                head.fontStyle = FontStyle.Bold;
            }

            _rows = new Row[ProductInfo.Count];
            for (int i = 0; i < ProductInfo.Count; i++)
            {
                float y = -92f - i * 66f;
                RectTransform row = NewRect(panel, "Row " + i);
                SetRect(row, new Vector2(16f, y), new Vector2(868f, 58f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                AddImage(row, i % 2 == 0 ? new Color(0.13f, 0.145f, 0.18f, 0.95f) : new Color(0.10f, 0.115f, 0.145f, 0.95f));

                _rows[i] = new Row();
                _rows[i].Line = NewText(row, "Line", "", 18, LineColors[i], TextAnchor.MiddleLeft);
                SetRect(_rows[i].Line.rectTransform, new Vector2(10f, 0f), new Vector2(190f, 50f), new Vector2(0f, 0f), new Vector2(0f, 1f));
                _rows[i].Line.fontStyle = FontStyle.Bold;

                _rows[i].Materials = TableText(row, "Materials", 10f + 196f - 16f, 100f);
                _rows[i].Products = TableText(row, "Products", 10f + 196f + 120f - 16f, 100f);
                _rows[i].Levels = TableText(row, "Levels", 10f + 196f + 240f - 16f, 195f);
                _rows[i].Price = TableText(row, "Price", 10f + 196f + 440f - 16f, 90f);
                _rows[i].Status = TableText(row, "Status", 10f + 196f + 540f - 16f, 140f);
            }

            _tableTotals = NewText(panel, "Totals", "", 15, Muted, TextAnchor.UpperLeft);
            SetRect(_tableTotals.rectTransform, new Vector2(24f, -300f), new Vector2(872f, 46f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _tableTotals.horizontalOverflow = HorizontalWrapMode.Wrap;

            Text journalTitle = NewText(panel, "Journal Title", "ЖУРНАЛ", 15, new Color(0.55f, 0.72f, 0.95f), TextAnchor.UpperLeft);
            SetRect(journalTitle.rectTransform, new Vector2(24f, -352f), new Vector2(300f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            journalTitle.fontStyle = FontStyle.Bold;

            _journal = NewText(panel, "Journal", "", 15, Muted, TextAnchor.UpperLeft);
            SetRect(_journal.rectTransform, new Vector2(24f, -378f), new Vector2(872f, 100f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _journal.horizontalOverflow = HorizontalWrapMode.Wrap;
            _journal.verticalOverflow = VerticalWrapMode.Truncate;

            Text note = NewText(panel, "Note",
                "Закупщик берёт материалы за деньги. Производитель ждёт материалы, продавец ждёт готовый товар.\n" +
                "Каждая линия — отдельные машины, отдельный склад и отдельные улучшения.",
                14, new Color(0.52f, 0.57f, 0.65f), TextAnchor.LowerLeft);
            SetRect(note.rectTransform, new Vector2(24f, 10f), new Vector2(872f, 42f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            _table.SetActive(false);
        }

        Text TableText(RectTransform row, string name, float x, float width)
        {
            Text text = NewText(row, name, "", 16, TextColor, TextAnchor.MiddleLeft);
            SetRect(text.rectTransform, new Vector2(x, 0f), new Vector2(width, 50f), new Vector2(0f, 0f), new Vector2(0f, 1f));
            return text;
        }

        void BuildGameOver(RectTransform root)
        {
            _gameOver = NewRect(root, "Game Over").gameObject;
            RectTransform shade = _gameOver.GetComponent<RectTransform>();
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = Vector2.zero;
            shade.offsetMax = Vector2.zero;
            AddImage(shade, new Color(0.02f, 0.03f, 0.05f, 0.88f));

            RectTransform panel = NewRect(shade, "Panel");
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(780f, 360f);
            AddImage(panel, new Color(0.10f, 0.11f, 0.15f, 1f));

            RectTransform stripe = NewRect(panel, "Stripe");
            stripe.anchorMin = new Vector2(0f, 0f);
            stripe.anchorMax = new Vector2(1f, 0f);
            stripe.pivot = new Vector2(0.5f, 0f);
            stripe.anchoredPosition = new Vector2(0f, 0f);
            stripe.sizeDelta = new Vector2(0f, 6f);
            AddImage(stripe, new Color(0.95f, 0.36f, 0.32f));

            Text title = NewText(panel, "Title", "ПРОИЗВОДСТВО ОСТАНОВЛЕНО", 32, new Color(1f, 0.48f, 0.42f), TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0f, -30f), new Vector2(780f, 48f), new Vector2(0f, 1f), new Vector2(1f, 1f));
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -54f);

            _gameOverText = NewText(panel, "Text", "", 19, new Color(0.80f, 0.85f, 0.92f), TextAnchor.UpperCenter);
            SetRect(_gameOverText.rectTransform, new Vector2(0f, -108f), new Vector2(700f, 130f), new Vector2(0f, 1f), new Vector2(1f, 1f));
            _gameOverText.rectTransform.pivot = new Vector2(0.5f, 1f);
            _gameOverText.rectTransform.anchoredPosition = new Vector2(0f, -108f);
            _gameOverText.horizontalOverflow = HorizontalWrapMode.Wrap;

            Button restart = NewButton(panel, "Restart", "НАЧАТЬ ЗАНОВО", new Color(0.55f, 0.78f, 0.50f), 19);
            RectTransform restartRect = restart.GetComponent<RectTransform>();
            restartRect.anchorMin = new Vector2(0.5f, 0f);
            restartRect.anchorMax = new Vector2(0.5f, 0f);
            restartRect.pivot = new Vector2(0.5f, 0f);
            restartRect.anchoredPosition = new Vector2(0f, 34f);
            restartRect.sizeDelta = new Vector2(280f, 48f);
            restart.onClick.AddListener(delegate { _game.Restart(); });

            _gameOver.SetActive(false);
        }

        void BuildTutorial(RectTransform root)
        {
            _tutorial = NewRect(root, "Tutorial").gameObject;
            RectTransform shade = _tutorial.GetComponent<RectTransform>();
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = Vector2.zero;
            shade.offsetMax = Vector2.zero;
            AddImage(shade, new Color(0.02f, 0.03f, 0.05f, 0.78f));

            RectTransform panel = NewRect(shade, "Tutorial Panel");
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(880f, 540f);
            AddImage(panel, new Color(0.09f, 0.10f, 0.13f, 0.99f));

            RectTransform stripe = NewRect(panel, "Stripe");
            stripe.anchorMin = new Vector2(0f, 1f);
            stripe.anchorMax = new Vector2(1f, 1f);
            stripe.pivot = new Vector2(0.5f, 1f);
            stripe.anchoredPosition = Vector2.zero;
            stripe.sizeDelta = new Vector2(0f, 4f);
            AddImage(stripe, new Color(0.24f, 0.62f, 0.55f));

            Text title = NewText(panel, "Title", "ОБУЧЕНИЕ: КАК РАБОТАЕТ ЗАВОД", 22, TextColor, TextAnchor.UpperLeft);
            SetRect(title.rectTransform, new Vector2(24f, -18f), new Vector2(500f, 30f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            title.fontStyle = FontStyle.Bold;

            Button close = NewButton(panel, "Close", "ЗАКРЫТЬ", new Color(0.32f, 0.35f, 0.42f), 15);
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-18f, -16f);
            closeRect.sizeDelta = new Vector2(130f, 34f);
            close.onClick.AddListener(ToggleTutorial);

            string[] tabNames = { "1. ЦЕПОЧКА", "2. ЭНЕРГИЯ", "3. СМЕНЫ И ПЛАН", "4. РАЗВИТИЕ" };
            _tutorialStepTabs = new Button[4];
            float tabW = 202f;
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                Button stepTab = NewButton(panel, "StepTab " + i, tabNames[i], Dim, 15);
                RectTransform tabRect = stepTab.GetComponent<RectTransform>();
                SetRect(tabRect, new Vector2(24f + i * (tabW + 8f), -58f), new Vector2(tabW, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f));
                stepTab.onClick.AddListener(delegate { SetTutorialStep(idx); });
                _tutorialStepTabs[i] = stepTab;
            }

            RectTransform card = NewRect(panel, "Content Card");
            SetRect(card, new Vector2(24f, -104f), new Vector2(832f, 350f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            AddImage(card, new Color(0.12f, 0.135f, 0.17f, 0.95f));

            _tutorialStepTitle = NewText(card, "StepTitle", "", 20, new Color(0.95f, 0.96f, 0.98f), TextAnchor.UpperLeft);
            SetRect(_tutorialStepTitle.rectTransform, new Vector2(18f, -14f), new Vector2(796f, 26f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _tutorialStepTitle.fontStyle = FontStyle.Bold;

            _tutorialSubTitle = NewText(card, "SubTitle", "", 15, Muted, TextAnchor.UpperLeft);
            SetRect(_tutorialSubTitle.rectTransform, new Vector2(18f, -42f), new Vector2(796f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            float[] blockTops = { 70f, 138f, 206f };
            _tutorialPoint1Title = CreateBulletTitle(card, "P1Title", blockTops[0]);
            _tutorialPoint1Desc = CreateBulletDesc(card, "P1Desc", blockTops[0]);

            _tutorialPoint2Title = CreateBulletTitle(card, "P2Title", blockTops[1]);
            _tutorialPoint2Desc = CreateBulletDesc(card, "P2Desc", blockTops[1]);

            _tutorialPoint3Title = CreateBulletTitle(card, "P3Title", blockTops[2]);
            _tutorialPoint3Desc = CreateBulletDesc(card, "P3Desc", blockTops[2]);

            RectTransform tipBox = NewRect(card, "TipBox");
            SetRect(tipBox, new Vector2(14f, 14f), new Vector2(804f, 54f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            AddImage(tipBox, new Color(0.08f, 0.09f, 0.11f, 0.95f));

            RectTransform tipStripe = NewRect(tipBox, "TipStripe");
            SetRect(tipStripe, Vector2.zero, new Vector2(4f, 54f), new Vector2(0f, 0f), new Vector2(0f, 1f));
            AddImage(tipStripe, new Color(0.98f, 0.74f, 0.28f));

            _tutorialTipText = NewText(tipBox, "TipText", "", 14, new Color(0.85f, 0.88f, 0.92f), TextAnchor.MiddleLeft);
            SetRect(_tutorialTipText.rectTransform, new Vector2(14f, 0f), new Vector2(776f, 50f), new Vector2(0f, 0f), new Vector2(0f, 1f));
            _tutorialTipText.horizontalOverflow = HorizontalWrapMode.Wrap;

            _tutorialPrev = NewButton(panel, "Prev", "« НАЗАД", new Color(0.28f, 0.30f, 0.36f), 15);
            RectTransform prevRect = _tutorialPrev.GetComponent<RectTransform>();
            SetRect(prevRect, new Vector2(24f, 20f), new Vector2(150f, 40f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            _tutorialPrev.onClick.AddListener(delegate { SetTutorialStep(_tutorialStep - 1); });

            _tutorialStepBadge = NewText(panel, "Badge", "ШАГ 1 ИЗ 4", 16, new Color(0.60f, 0.65f, 0.73f), TextAnchor.MiddleCenter);
            SetRect(_tutorialStepBadge.rectTransform, new Vector2(190f, 20f), new Vector2(500f, 40f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            _tutorialStepBadge.fontStyle = FontStyle.Bold;

            _tutorialNext = NewButton(panel, "Next", "ДАЛЕЕ »", new Color(0.24f, 0.62f, 0.55f), 15);
            RectTransform nextRect = _tutorialNext.GetComponent<RectTransform>();
            SetRect(nextRect, new Vector2(-24f, 20f), new Vector2(190f, 40f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            _tutorialNext.onClick.AddListener(OnTutorialNextClicked);
            _tutorialNextLabel = _tutorialNext.GetComponentInChildren<Text>();

            _tutorial.SetActive(false);
            SetTutorialStep(0);
        }

        void BuildHintCard(RectTransform root)
        {
            _hintCard = NewRect(root, "Hint Card").gameObject;
            RectTransform rect = _hintCard.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(20f, 16f);
            rect.sizeDelta = new Vector2(440f, 88f);
            AddImage(rect, Panel);

            RectTransform stripe = NewRect(rect, "Stripe");
            stripe.anchorMin = new Vector2(0f, 0f);
            stripe.anchorMax = new Vector2(0f, 1f);
            stripe.pivot = new Vector2(0f, 0.5f);
            stripe.anchoredPosition = Vector2.zero;
            stripe.sizeDelta = new Vector2(4f, 0f);
            AddImage(stripe, new Color(0.24f, 0.62f, 0.55f));

            Text title = NewText(rect, "Title", "СОВЕТ ДИРЕКТОРУ", 14, new Color(0.24f, 0.62f, 0.55f), TextAnchor.UpperLeft);
            SetRect(title.rectTransform, new Vector2(14f, -8f), new Vector2(240f, 20f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            title.fontStyle = FontStyle.Bold;

            _hintText = NewText(rect, "Text", "", 13, TextColor, TextAnchor.UpperLeft);
            SetRect(_hintText.rectTransform, new Vector2(14f, -28f), new Vector2(412f, 36f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _hintText.horizontalOverflow = HorizontalWrapMode.Wrap;

            Button openTut = NewButton(rect, "OpenTut", "ОБУЧЕНИЕ", new Color(0.24f, 0.62f, 0.55f), 12);
            RectTransform openTutRect = openTut.GetComponent<RectTransform>();
            SetRect(openTutRect, new Vector2(14f, 8f), new Vector2(100f, 24f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            openTut.onClick.AddListener(ToggleTutorial);

            Button hide = NewButton(rect, "Hide", "СКРЫТЬ", new Color(0.28f, 0.30f, 0.36f), 12);
            RectTransform hideRect = hide.GetComponent<RectTransform>();
            SetRect(hideRect, new Vector2(122f, 8f), new Vector2(80f, 24f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            hide.onClick.AddListener(delegate { _hintCard.SetActive(false); });
        }

        Text CreateBulletTitle(RectTransform parent, string name, float top)
        {
            Text text = NewText(parent, name, "", 16, TextColor, TextAnchor.UpperLeft);
            SetRect(text.rectTransform, new Vector2(18f, -top), new Vector2(220f, 24f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            text.fontStyle = FontStyle.Bold;
            return text;
        }

        Text CreateBulletDesc(RectTransform parent, string name, float top)
        {
            Text text = NewText(parent, name, "", 15, TextColor, TextAnchor.UpperLeft);
            SetRect(text.rectTransform, new Vector2(244f, -top), new Vector2(570f, 54f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        void SetTutorialStep(int step)
        {
            _tutorialStep = Mathf.Clamp(step, 0, 3);
            _tutorialStepBadge.text = "ШАГ " + (_tutorialStep + 1) + " ИЗ 4";

            for (int i = 0; i < _tutorialStepTabs.Length; i++)
            {
                bool active = i == _tutorialStep;
                Image img = _tutorialStepTabs[i].GetComponent<Image>();
                img.color = active ? new Color(0.24f, 0.62f, 0.55f) : Dim;
                Text label = _tutorialStepTabs[i].GetComponentInChildren<Text>();
                label.color = active ? new Color(0.07f, 0.08f, 0.10f) : TextColor;
            }

            _tutorialPrev.interactable = _tutorialStep > 0;
            _tutorialNextLabel.text = _tutorialStep == 3 ? "ПОНЯТНО, В БОЙ!" : "ДАЛЕЕ »";

            if (_tutorialStep == 0)
            {
                _tutorialStepTitle.text = "1. ПРОИЗВОДСТВЕННАЯ ЦЕПОЧКА (3 СТАНКА)";
                _tutorialSubTitle.text = "Каждая рабочая линия состоит из трёх станков, работающих автоматически:";

                _tutorialPoint1Title.text = "· ЗАКУПЩИК";
                _tutorialPoint1Title.color = BuyerColor;
                _tutorialPoint1Desc.text = "Закупает сырьё партиями по рыночной цене и отправляет на склад материалов (вместимость 10 шт).";

                _tutorialPoint2Title.text = "· ПРОИЗВОДИТЕЛЬ";
                _tutorialPoint2Title.color = ProducerColor;
                _tutorialPoint2Desc.text = "Забирает сырьё со склада и перерабатывает его в готовую продукцию. Если сырья нет — ожидает поставку.";

                _tutorialPoint3Title.text = "· ПРОДАВЕЦ";
                _tutorialPoint3Title.color = SellerColor;
                _tutorialPoint3Desc.text = "Отгружает готовую продукцию со склада и приносит вам деньги за каждый проданный товар.";

                _tutorialTipText.text = "СОВЕТ: Следите за балансом скоростей станков, чтобы склад материалов или товаров не переполнялся.";
            }
            else if (_tutorialStep == 1)
            {
                _tutorialStepTitle.text = "2. ЭНЕРГОСЕТЬ И ГЕНЕРАТОРЫ";
                _tutorialSubTitle.text = "Завод полностью зависит от стабильного электроснабжения:";

                _tutorialPoint1Title.text = "· РАСХОД ЭНЕРГИИ";
                _tutorialPoint1Title.color = GeneratorColor;
                _tutorialPoint1Desc.text = "Каждая линия и каждый уровень улучшения машин потребляют энергию (см. «ЭНЕРГИЯ X / Y» вверху).";

                _tutorialPoint2Title.text = "· ОБЕСТОЧИВАНИЕ";
                _tutorialPoint2Title.color = new Color(1f, 0.45f, 0.42f);
                _tutorialPoint2Desc.text = "Если расход превысит ёмкость генераторов — завод обесточится, и все станки остановятся!";

                _tutorialPoint3Title.text = "· ГЕНЕРАТОРЫ";
                _tutorialPoint3Title.color = GeneratorColor;
                _tutorialPoint3Desc.text = "Улучшайте генераторы 1 и 2 внизу правой панели (+4 энергии за уровень). Учтите: растёт плата за топливо.";

                _tutorialTipText.text = "СОВЕТ: Перед открытием новой линии или прокачкой машин убедитесь, что есть свободная энергия.";
            }
            else if (_tutorialStep == 2)
            {
                _tutorialStepTitle.text = "3. СМЕНЫ, ПЛАН И БАНКРОТСТВО";
                _tutorialSubTitle.text = "Удерживайте завод на плаву и выполняйте ежедневные нормы выручки:";

                _tutorialPoint1Title.text = "· СМЕНА 40 СЕКУНД";
                _tutorialPoint1Title.color = new Color(0.85f, 0.90f, 1f);
                _tutorialPoint1Desc.text = "Каждый день длится 40 секунд смены. В верхнем баре виден таймер смены и прогресс выполнения плана.";

                _tutorialPoint2Title.text = "· ПРЕМИЯ ЗА ПЛАН";
                _tutorialPoint2Title.color = new Color(0.55f, 0.95f, 0.60f);
                _tutorialPoint2Desc.text = "Выполнение плана дня даёт щедрую денежную премию. Завтра план вырастет — готовьтесь!";

                _tutorialPoint3Title.text = "· 3 СТРАЙКА = КОНЕЦ";
                _tutorialPoint3Title.color = new Color(1f, 0.48f, 0.42f);
                _tutorialPoint3Desc.text = "Провал плана или нехватка денег на топливо дают штраф (страйк ✕). 3 страйка — банкротство и конец игры.";

                _tutorialTipText.text = "СОВЕТ: Не держите прибыль мёртвым грузом — инвестируйте деньги в прокачку станков и новые линии.";
            }
            else
            {
                _tutorialStepTitle.text = "4. РАСШИРЕНИЕ И СТАТИСТИКА";
                _tutorialSubTitle.text = "Как масштабировать завод до максимальной прибыли:";

                _tutorialPoint1Title.text = "· ВКЛАДКИ ЛИНИЙ";
                _tutorialPoint1Title.color = LineColors[1];
                _tutorialPoint1Desc.text = "Вверху правой панели переключайтесь между линиями: «ШЕСТ.» (Шестерёнки), «КОРПУС» и «МИКРО».";

                _tutorialPoint2Title.text = "· ПРОКАЧКА СТАНКОВ";
                _tutorialPoint2Title.color = SellerColor;
                _tutorialPoint2Desc.text = "Продавец увеличивает цену продажи, а Закупщик и Производитель ускоряют свои циклы.";

                _tutorialPoint3Title.text = "· ТАБЛИЦА БАЛАНСА";
                _tutorialPoint3Title.color = new Color(0.28f, 0.45f, 0.72f);
                _tutorialPoint3Desc.text = "Кнопка «ТАБЛИЦА» вверху открывает подробную финансовую статистику, остатки сырья и журнал событий.";

                _tutorialTipText.text = "СОВЕТ: Линия Корпусов требует 2 сырья, но приносит больше выручки; Микросхемы — самый доходный товар.";
            }
        }

        void OnTutorialNextClicked()
        {
            if (_tutorialStep < 3) SetTutorialStep(_tutorialStep + 1);
            else ToggleTutorial();
        }

        public void ToggleTutorial()
        {
            if (_tutorial == null) return;
            bool open = !_tutorial.activeSelf;
            _tutorial.SetActive(open);
            if (open && _table != null && _table.activeSelf) ToggleTable();
            if (_tutorialButtonLabel != null) _tutorialButtonLabel.text = open ? "СКРЫТЬ" : "ОБУЧЕНИЕ";
        }

        void UpdateHint(FactorySim sim)
        {
            if (_hintCard == null || !_hintCard.activeSelf || _hintText == null) return;

            if (sim.Day == 1 && sim.TotalSold == 0)
            {
                _hintText.text = "Линия шестерёнок запущена! Станки работают сами: следите за ростом денег вверху.";
            }
            else if (sim.EnergyUsed >= sim.EnergyCapacity)
            {
                _hintText.text = "Энергия на пределе! Улучшите Генератор 1 или 2 внизу правой панели.";
            }
            else if (sim.Money >= 45f && sim.Lines[0].Buyer.Level == 1)
            {
                _hintText.text = "Есть деньги на улучшение! Нажмите «УЛУЧШИТЬ» у Закупщика или Производителя.";
            }
            else if (sim.ShiftRevenue + 0.5f >= sim.PlanTarget)
            {
                _hintText.text = "План дня выполнен! В конце смены будет премия. Копите на линию «КОРПУСА».";
            }
            else if (sim.Day >= 2 && !sim.Lines[1].Built && sim.Money >= 180f && sim.EnergyCapacity - sim.EnergyUsed >= FactorySim.LineEnergy)
            {
                _hintText.text = "Хватает средств на вторую линию! Переключитесь на «КОРПУС» и постройте её.";
            }
            else
            {
                _hintText.text = "Нажмите «ОБУЧЕНИЕ» в любой момент, чтобы изучить подробные правила завода.";
            }
        }

        public void ToggleTable()
        {
            bool open = !_table.activeSelf;
            _table.SetActive(open);
            if (open && _tutorial != null && _tutorial.activeSelf) ToggleTutorial();
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

            _money.text = "$ " + Mathf.FloorToInt(sim.Money);
            _day.text = "ДЕНЬ " + sim.Day + (sim.Strikes > 0 ? " · ✕" + sim.Strikes : "");
            _day.color = sim.Strikes > 0 ? new Color(1f, 0.55f, 0.48f) : TextColor;
            _phase.text = sim.PhaseText;

            bool planMet = sim.ShiftRevenue + 0.5f >= sim.PlanTarget;
            _plan.text = "ПЛАН $" + Mathf.FloorToInt(sim.ShiftRevenue) + " / $" + sim.PlanTarget;
            _plan.color = planMet
                ? new Color(0.55f, 0.95f, 0.60f)
                : (sim.ShiftActive ? new Color(0.85f, 0.90f, 1f) : new Color(0.95f, 0.78f, 0.42f));

            bool energyOk = sim.EnergyOk;
            _energy.text = "ЭНЕРГИЯ " + sim.EnergyUsed + " / " + sim.EnergyCapacity;
            _energy.color = energyOk ? new Color(0.55f, 0.9f, 0.6f) : new Color(1f, 0.38f, 0.35f);

            _materials.text = "МАТ " + sim.TotalMaterials + "/" + (ProductInfo.MaterialCap * ProductInfo.Count) +
                              " · ТОВ " + sim.TotalProducts + "/" + (ProductInfo.ProductCap * ProductInfo.Count);
            _upkeep.text = "ТОПЛИВО $" + sim.TotalUpkeep + "/день";

            _banner.text = sim.LastEvent;
            if (sim.LastTone == EventTone.Bad) _banner.color = new Color(1f, 0.45f, 0.42f);
            else if (sim.LastTone == EventTone.Good) _banner.color = new Color(0.55f, 0.95f, 0.60f);
            else _banner.color = new Color(0.82f, 0.88f, 1f);

            if (sim.GameOver)
            {
                if (_table.activeSelf) ToggleTable();
                if (_tutorial != null && _tutorial.activeSelf) ToggleTutorial();
                _gameOverText.text = "План провален " + sim.Strikes + " раза. Дней продержались: " + (sim.Day - 1) +
                                     ", продано " + sim.TotalSold + " шт.\n" +
                                     "Следующая попытка: держите план, улучшайте машины и открывайте новые линии.";
            }

            if (_gameOver.activeSelf != sim.GameOver) _gameOver.SetActive(sim.GameOver);

            if (_selected < 0 || _selected >= sim.Lines.Count) _selected = 0;

            UpdateTabs(sim);
            UpdateMachines(sim);
            UpdateGenerators(sim);
            UpdateBuild(sim);
            UpdateTable(sim);
            UpdateHint(sim);
        }

        void UpdateTabs(FactorySim sim)
        {
            for (int i = 0; i < _tabs.Length && i < sim.Lines.Count; i++)
            {
                bool active = i == _selected;
                bool built = sim.Lines[i].Built;
                Image image = _tabs[i].GetComponent<Image>();
                image.color = active ? LineColors[i] : (built ? new Color(0.18f, 0.20f, 0.25f) : new Color(0.14f, 0.15f, 0.18f));

                Text label = _tabs[i].GetComponentInChildren<Text>();
                label.color = active ? new Color(0.07f, 0.08f, 0.10f) : (built ? TextColor : new Color(0.48f, 0.52f, 0.58f));
            }
        }

        void UpdateMachines(FactorySim sim)
        {
            LineState line = sim.Lines[_selected];
            for (int i = 0; i < _cards.Length; i++)
            {
                Card card = _cards[i];
                MachineState machine = line.Machine((MachineKind)i);
                card.Title.text = MachineCaption(i, line.Built);
                card.Level.text = line.Built ? "Lv " + machine.Level : "—";
                card.Power.text = line.Built ? "Мощность: " + machine.EnergyDraw + " ед." : "";

                if (!line.Built)
                {
                    card.Stats.text = "Линия не построена";
                    SetButton(card, false, "НЕДОСТУПНО");
                    continue;
                }

                if (machine.Kind == MachineKind.Buyer)
                {
                    card.Stats.text = "Партия " + machine.BuyBatch + " шт · $" + sim.MaterialPrice + "/шт\n" +
                                      "Цикл " + machine.BuyInterval.ToString("0.00") + "с";
                }
                else if (machine.Kind == MachineKind.Producer)
                {
                    card.Stats.text = "Нужно " + ProductInfo.Materials(line.Kind) + " мат. · " + machine.CraftTime.ToString("0.00") + "с\n" +
                                      "На складе " + line.Products + "/" + ProductInfo.ProductCap;
                }
                else
                {
                    card.Stats.text = "Цена $" + sim.SalePrice(line) + " · +$3/уровень\n" +
                                      "Цикл " + machine.SellInterval.ToString("0.00") + "с";
                }

                string reason;
                SetButton(card, CanUpgradeMachine(sim, line, machine, out reason), reason);
            }
        }

        static string MachineCaption(int kind, bool built)
        {
            string suffix = built ? "" : " · НЕТ";
            switch (kind)
            {
                case 0: return "ЗАКУПЩИК" + suffix;
                case 1: return "ПРОИЗВОДИТЕЛЬ" + suffix;
                default: return "ПРОДАВЕЦ" + suffix;
            }
        }

        void UpdateGenerators(FactorySim sim)
        {
            for (int i = 0; i < _generators.Length && i < sim.Generators.Count; i++)
            {
                Card card = _generators[i];
                GeneratorState generator = sim.Generators[i];

                card.Level.text = "Lv " + generator.Level;
                card.Stats.text = "Ёмкость: +" + generator.Capacity + " энергии\nТопливо: $" + generator.DailyUpkeep + "/день";
                card.Power.text = "";

                string reason;
                SetButton(card, CanUpgradeGenerator(sim, generator, out reason), reason);
            }
        }

        void UpdateBuild(FactorySim sim)
        {
            int target = -1;
            if (!sim.Lines[_selected].Built) target = _selected;
            else
            {
                for (int i = 0; i < sim.Lines.Count; i++)
                {
                    if (!sim.Lines[i].Built) { target = i; break; }
                }
            }

            if (target < 0)
            {
                _build.Title.text = "ВСЕ ЛИНИИ ПОСТРОЕНЫ";
                _build.Level.text = "";
                _build.Stats.text = "Свободных мест на полу больше нет";
                _build.Power.text = "";
                _build.Button.gameObject.SetActive(false);
                return;
            }

            LineState line = sim.Lines[target];
            _build.Button.gameObject.SetActive(true);
            _build.Title.text = "ЛИНИЯ «" + line.Name.ToUpper() + "»";
            _build.Level.text = "";
            _build.Stats.text = "Стоимость $" + line.BuildCost + " · энергия " + FactorySim.LineEnergy + "\n" +
                                "Свободно энергии: " + (sim.EnergyCapacity - sim.EnergyUsed);
            _build.Power.text = "";

            string reason;
            bool ok = true;
            if (sim.Money + 0.001f < line.BuildCost) { reason = "НУЖНО $" + line.BuildCost; ok = false; }
            else if (sim.EnergyUsed + FactorySim.LineEnergy > sim.EnergyCapacity) { reason = "НЕТ ЭНЕРГИИ"; ok = false; }
            else reason = "ПОСТРОИТЬ $" + line.BuildCost;

            _build.Accent = LineColors[target];
            SetButton(_build, ok, reason);
        }

        bool CanUpgradeMachine(FactorySim sim, LineState line, MachineState machine, out string reason)
        {
            if (machine.Level >= FactorySim.MaxLevel) { reason = "МАКС. УРОВЕНЬ"; return false; }
            if (sim.Money + 0.001f < machine.UpgradeCost) { reason = "НУЖНО $" + machine.UpgradeCost; return false; }

            int newUsed = sim.EnergyUsed - machine.EnergyDraw + machine.NextEnergyDraw;
            if (newUsed > sim.EnergyCapacity) { reason = "НЕТ ЭНЕРГИИ"; return false; }

            reason = "УЛУЧШИТЬ $" + machine.UpgradeCost;
            return true;
        }

        bool CanUpgradeGenerator(FactorySim sim, GeneratorState generator, out string reason)
        {
            if (generator.Level >= FactorySim.MaxLevel) { reason = "МАКС. УРОВЕНЬ"; return false; }
            if (sim.Money + 0.001f < generator.UpgradeCost) { reason = "НУЖНО $" + generator.UpgradeCost; return false; }

            reason = "УЛУЧШИТЬ $" + generator.UpgradeCost;
            return true;
        }

        void SetButton(Card card, bool enabled, string label)
        {
            card.Button.interactable = enabled;
            card.ButtonLabel.text = label;

            ColorBlock colors = card.Button.colors;
            colors.normalColor = enabled ? card.Accent : new Color(0.28f, 0.30f, 0.35f);
            colors.highlightedColor = enabled ? Color.Lerp(card.Accent, Color.white, 0.18f) : new Color(0.32f, 0.34f, 0.39f);
            colors.pressedColor = enabled ? Color.Lerp(card.Accent, Color.black, 0.22f) : new Color(0.25f, 0.27f, 0.31f);
            colors.disabledColor = new Color(0.24f, 0.26f, 0.30f);
            card.Button.colors = colors;
            card.ButtonLabel.color = enabled ? new Color(0.07f, 0.08f, 0.10f) : new Color(0.62f, 0.66f, 0.72f);
        }

        void UpdateTable(FactorySim sim)
        {
            if (!_table.activeSelf) return;

            var journal = new System.Text.StringBuilder();
            int start = Mathf.Max(0, sim.Log.Count - 4);
            for (int i = start; i < sim.Log.Count; i++)
            {
                journal.Append("· ").Append(sim.Log[i]);
                if (i < sim.Log.Count - 1) journal.Append('\n');
            }
            _journal.text = journal.ToString();

            for (int i = 0; i < _rows.Length && i < sim.Lines.Count; i++)
            {
                LineState line = sim.Lines[i];
                Row row = _rows[i];

                row.Line.text = line.Built ? line.Name : line.Name + " · нет";
                row.Line.color = line.Built ? LineColors[i] : new Color(0.52f, 0.56f, 0.63f);

                row.Materials.text = line.Built ? line.Materials + " / " + ProductInfo.MaterialCap : "—";
                row.Products.text = line.Built ? line.Products + " / " + ProductInfo.ProductCap : "—";
                row.Levels.text = line.Built
                    ? "З " + line.Buyer.Level + " · П " + line.Producer.Level + " · Пр " + line.Seller.Level
                    : "—";
                row.Price.text = "$" + sim.SalePrice(line);

                if (!line.Built)
                {
                    row.Status.text = "НЕ ПОСТРОЕНА";
                    row.Status.color = new Color(0.55f, 0.58f, 0.64f);
                }
                else if (!sim.EnergyOk)
                {
                    row.Status.text = "НЕТ ЭНЕРГИИ";
                    row.Status.color = new Color(1f, 0.45f, 0.42f);
                }
                else if (!sim.ShiftActive)
                {
                    row.Status.text = sim.Skipped ? "ПРОПУСК" : "ПЕРЕРЫВ";
                    row.Status.color = new Color(0.92f, 0.80f, 0.45f);
                }
                else
                {
                    row.Status.text = RowState(line);
                    row.Status.color = new Color(0.60f, 0.90f, 0.62f);
                }
            }

            _tableTotals.text =
                "Куплено " + sim.TotalBought + " мат. · Произведено " + sim.TotalProduced + " шт. · Продано " + sim.TotalSold + " шт.\n" +
                "Выручка $" + Mathf.FloorToInt(sim.TotalRevenue) + " · Расходы $" + Mathf.FloorToInt(sim.TotalSpent) +
                " · Субсидии $" + Mathf.FloorToInt(sim.TotalSubsidy) + " · Энергия " + sim.EnergyUsed + "/" + sim.EnergyCapacity;
        }

        static string RowState(LineState line)
        {
            if (line.Products >= ProductInfo.ProductCap) return "СКЛАД ПОЛОН";
            if (line.Materials < ProductInfo.Materials(line.Kind)) return "ЖДЁТ МАТЕРИАЛ";
            return "РАБОТА";
        }

        static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static Text NewText(Transform parent, string name, string value, int size, Color color, TextAnchor anchor)
        {
            RectTransform rect = NewRect(parent, name);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            if (_font != null) text.font = _font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Button NewButton(Transform parent, string name, string label, Color color, int fontSize)
        {
            RectTransform rect = NewRect(parent, name);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = NewText(rect, "Label", label, fontSize, new Color(0.07f, 0.08f, 0.10f), TextAnchor.MiddleCenter);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4f, 2f);
            textRect.offsetMax = new Vector2(-4f, -2f);
            text.fontStyle = FontStyle.Bold;
            return button;
        }

        static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
