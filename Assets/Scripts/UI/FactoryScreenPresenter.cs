using System;
using System.Collections.Generic;

namespace Game.UI
{
    /// <summary>
    /// Презентер экрана производственного цеха (паттерн MVP).
    /// Изолирует состояние и правила обновления от конкретной UI-системы.
    /// </summary>
    public sealed class FactoryScreenPresenter : IDisposable
    {
        private readonly FactoryScreenView _view;
        private readonly List<MachineCardData> _cachedCards = new(12);
        private int _currentTabIndex;

        public FactoryScreenPresenter(FactoryScreenView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));

            _view.OnTutorialClicked += HandleTutorial;
            _view.OnTableClicked += HandleTable;
            _view.OnRestartClicked += HandleRestart;
            _view.OnTabSelected += HandleTabChanged;
            _view.OnCardActionTriggered += HandleCardUpgrade;
        }

        public void Initialize()
        {
            _view.PlayEntranceAnimation();
            _view.SelectTabVisual(0);
            _view.SetBannerInformation(
                "ПЛАН ДНЯ: Шест. 0/5 • Корп. 0/2 • Редуктор 0/1 (Награда: +$150)",
                "СОВЕТ ДИРЕКТОРУ",
                "Смотрите на «ПЛАН ДНЯ» вверху: выполняйте позиции плана, чтобы получать крупные субсидии."
            );

            RefreshDisplayState();
        }

        public void Dispose()
        {
            _view.OnTutorialClicked -= HandleTutorial;
            _view.OnTableClicked -= HandleTable;
            _view.OnRestartClicked -= HandleRestart;
            _view.OnTabSelected -= HandleTabChanged;
            _view.OnCardActionTriggered -= HandleCardUpgrade;
        }

        private void RefreshDisplayState()
        {
            var topBar = new TopBarData
            {
                Balance = 250,
                Day = 1,
                PlanCurrent = 0,
                PlanTotal = 3,
                ShiftSecondsRemaining = 40.0f,
                EnergyUsed = 6,
                EnergyMax = 8,
                WarehouseMaterials = 0,
                WarehouseGoods = 0,
                WarehouseMax = 30,
                FuelDailyCost = 20
            };

            _view.UpdateTopBar(topBar);
            LoadTabCards(_currentTabIndex);
        }

        private void HandleTabChanged(int tabIndex)
        {
            _currentTabIndex = tabIndex;
            if (tabIndex == 3)
            {
                _view.SetBannerInformation(
                    "ПЛАН ДНЯ: Шест. 0/5 • Корп. 0/2 • Редуктор 0/1 (Награда: +$150)",
                    "СОВЕТ ДИРЕКТОРУ: СБОРКА",
                    "Посты сборки берут детали со складов и создают ценные узлы (Редукторы, Приводы, Турбины)."
                );
            }
            else
            {
                _view.SetBannerInformation(
                    "ПЛАН ДНЯ: Шест. 0/5 • Корп. 0/2 • Редуктор 0/1 (Награда: +$150)",
                    "СОВЕТ ДИРЕКТОРУ",
                    "Смотрите на «ПЛАН ДНЯ» вверху: выполняйте позиции плана, чтобы получать крупные субсидии."
                );
            }
            LoadTabCards(tabIndex);
        }

        private void LoadTabCards(int tabIndex)
        {
            _cachedCards.Clear();

            switch (tabIndex)
            {
                case 0:
                    _cachedCards.Add(new MachineCardData
                    {
                        Id = "buyer_gears",
                        Kind = MachineCardKind.Buyer,
                        Title = "Закупщик",
                        Level = 1,
                        PrimarySpec = "Партия: 1 шт • Цикл 2.00с",
                        SecondarySpec = "Мощность: 2 ед.",
                        Cost = 95,
                        IsAffordable = true,
                        IsLocked = false,
                        Theme = CardAccentTheme.Amber
                    });
                    _cachedCards.Add(new MachineCardData
                    {
                        Id = "producer_gears",
                        Kind = MachineCardKind.Producer,
                        Title = "Производитель",
                        Level = 1,
                        PrimarySpec = "Расход: 1 мат. • 1.60с",
                        SecondarySpec = "Склад деталей: 0 / 10 шт.",
                        Cost = 85,
                        IsAffordable = true,
                        IsLocked = false,
                        Theme = CardAccentTheme.Cyan
                    });
                    _cachedCards.Add(new MachineCardData
                    {
                        Id = "seller_gears",
                        Kind = MachineCardKind.Seller,
                        Title = "Продавец",
                        Level = 1,
                        PrimarySpec = "Цена: $12 (+$3/ур) • 1.60с",
                        SecondarySpec = "Мощность: 2 ед.",
                        Cost = 110,
                        IsAffordable = true,
                        IsLocked = false,
                        Theme = CardAccentTheme.Green
                    });
                    _cachedCards.Add(new MachineCardData
                    {
                        Id = "gen_1",
                        Kind = MachineCardKind.Generator1,
                        Title = "Генератор 1",
                        Level = 1,
                        PrimarySpec = "Ёмкость: +8 энергии • $20/д",
                        SecondarySpec = "Статус: В норме (100%)",
                        Cost = 140,
                        IsAffordable = true,
                        IsLocked = false,
                        Theme = CardAccentTheme.Green
                    });
                    _cachedCards.Add(new MachineCardData
                    {
                        Id = "gen_2",
                        Kind = MachineCardKind.Generator2,
                        Title = "Генератор 2",
                        Level = 1,
                        PrimarySpec = "Ёмкость: +8 энергии • $20/д",
                        SecondarySpec = "Статус: В норме (100%)",
                        Cost = 140,
                        IsAffordable = false,
                        IsLocked = false,
                        Theme = CardAccentTheme.Green
                    });
                    _cachedCards.Add(new MachineCardData
                    {
                        Id = "line_cases",
                        Kind = MachineCardKind.BuildNewLine,
                        Title = "Линия «Корпуса»",
                        Level = 0,
                        PrimarySpec = "Стоимость: $180 • Энергия: 6",
                        SecondarySpec = "Свободно энергии: 2 ед.",
                        Cost = 180,
                        IsAffordable = true,
                        IsLocked = true,
                        Theme = CardAccentTheme.Purple
                    });
                    break;

                case 3:
                    _cachedCards.Add(new MachineCardData
                    {
                        Id = "assembly_gearbox",
                        Kind = MachineCardKind.AssemblyUnit,
                        Title = "Сборка · Редуктор",
                        Level = 0,
                        PrimarySpec = "2 ШЕСТ. + 1 КОРПУС → РЕДУКТОР",
                        SecondarySpec = "Цена: $90 • Цикл 7.0с • 3 ед.",
                        Cost = 260,
                        IsAffordable = false,
                        IsLocked = false,
                        Theme = CardAccentTheme.Pink
                    });
                    _cachedCards.Add(new MachineCardData
                    {
                        Id = "assembly_drive",
                        Kind = MachineCardKind.AssemblyUnit,
                        Title = "Сборка · Привод",
                        Level = 0,
                        PrimarySpec = "1 РЕДУКТОР + 2 МИКРО → ПРИВОД",
                        SecondarySpec = "Цена: $240 • Цикл 10.0с • 4 ед.",
                        Cost = 450,
                        IsAffordable = false,
                        IsLocked = false,
                        Theme = CardAccentTheme.Pink
                    });
                    _cachedCards.Add(new MachineCardData
                    {
                        Id = "assembly_turbine",
                        Kind = MachineCardKind.AssemblyUnit,
                        Title = "Сборка · Турбина",
                        Level = 0,
                        PrimarySpec = "2 ПРИВОДА + 2 КОРПУСА → ТУРБИНА",
                        SecondarySpec = "Цена: $650 • Цикл 15.0с • 6 ед.",
                        Cost = 900,
                        IsAffordable = false,
                        IsLocked = false,
                        Theme = CardAccentTheme.Pink
                    });
                    break;
            }

            _view.RenderMachineCards(_cachedCards);
        }

        private void HandleCardUpgrade(string cardId)
        {
            RefreshDisplayState();
        }

        private void HandleTutorial() { }
        private void HandleTable() { }
        private void HandleRestart() { }
    }
}
