using System;
using System.Collections.Generic;

namespace Game.Factory
{
    // Здесь вся математика игры: деньги, дни, линии, станки, сборка и план.
    // Файл уже большой, его честно можно разбить на три, но пока так удобнее:
    // открыл один файл — и видишь весь баланс разом. Если что, я предупреждал.
    public enum MachineKind
    {
        Buyer,      // закупает сырьё
        Producer,   // делает товар
        Seller      // продаёт товар
    }

    public enum UpgradeStatus
    {
        Ok,
        NotEnoughMoney,
        NotEnoughEnergy,
        MaxLevel,
        AlreadyBuilt,
        NotBuilt
    }

    public enum EventTone
    {
        Info,   // обычная новость дня
        Good,   // план выполнен, что-то построено
        Bad     // провал, штраф, нет денег
    }

    public enum ProductKind
    {
        Gear,   // 0
        Case,   // 1
        Chip    // 2
    }

    // Справочник по товарам. Индекс ProductKind совпадает с индексом линии,
    // поэтому здесь всё в виде switch — быстро и без словарей.
    public static class ProductInfo
    {
        public const int Count = 3;

        // Цена сырья. Растёт каждый третий день (см. FactorySim.MaterialPrice).
        public const int MaterialCost = 4;

        // Склады маленькие специально: иначе можно было бы закупиться на год вперёд
        // и вообще не следить за балансом.
        public const int MaterialCap = 10;
        public const int ProductCap = 10;

        public static string Name(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Gear: return "Шестерёнки";
                case ProductKind.Case: return "Корпуса";
                default: return "Микросхемы";
            }
        }

        public static string ShortName(ProductKind kind)
        {
            // Короткие подписи для вкладок и таблицы.
            switch (kind)
            {
                case ProductKind.Gear: return "ШЕСТ";
                case ProductKind.Case: return "КОРПУС";
                default: return "МИКРО";
            }
        }

        // Сколько сырья съедает одна штука. Корпус дороже шестерёнки,
        // микросхема — самая дорогая, поэтому и линия открывается позже.
        public static int Materials(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Gear: return 1;
                case ProductKind.Case: return 2;
                default: return 3;
            }
        }

        // Цены продажи. Продавец потом добавляет +3 за уровень,
        // а инфляция умножает всё это на PriceScale.
        public static int BasePrice(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Gear: return 10;
                case ProductKind.Case: return 24;
                default: return 42;
            }
        }

        // Время производства одной штуки на первом уровне.
        // Микросхема делается почти в три раза дольше шестерёнки — это и есть её цена.
        public static float CraftTime(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Gear: return 1.6f;
                case ProductKind.Case: return 3.2f;
                default: return 4.8f;
            }
        }

        // Первая линия бесплатная, остальные покупаются. Иначе старт был бы слишком сложным.
        public static int BuildCost(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Case: return 180;
                case ProductKind.Chip: return 420;
                default: return 0;
            }
        }
    }

    // Один пункт дневного плана: «сделай N таких-то деталей за смену».
    // Part — тот же id, что и у входов рецепта: 0..2 это склады линий, 10+ это сборки.
    public class PlanItem
    {
        public readonly int Part;
        public readonly int Need;

        public PlanItem(int part, int need)
        {
            Part = part;
            Need = need;
        }

        public string Name { get { return Recipes.PartName(Part); } }
    }

    public class FactorySim
    {
        public const int MaxLevel = 15;
        public const int LineEnergy = 6;
        public const int MaxStrikes = 3;

        public float Money = 300f;
        public int Day;

        public float ShiftLength = 40f; // 40 секунд — нормально для прототипа, 60 было нудно
        public float ShiftElapsed;
        public bool ShiftActive;
        public bool Skipped;
        public float NextDayTimer;
        public int PendingUpkeep;

        // Пауза. Смену можно остановить кнопкой вверху или пробелом,
        // а ещё игра сама встаёт, пока открыто обучение.
        public bool Paused;

        // Брак на конвейере. Красная деталь висит на складе линии,
        // её надо успеть убрать кликом. 12 секунд — это нормально:
        // можно отвлечься на карточку, но и не «пропустил за 2 секунды».
        public const float DefectTime = 12f;
        public const int DefectsPerShift = 2;

        // Сколько секунд осталось до следующего брака.
        // Ставим 16-26 секунд, за 40-секундную смену выходит 1-2 штуки.
        public float DefectSpawnTimer;

        public int DefectsThisShift;
        public int DefectsCaught;
        public int DefectsMissed;

        // Свой рандом, чтобы не тащить UnityEngine в чистую симуляцию.
        readonly Random _rng = new Random();

        // Три провала плана — и всё, завод закрывается. Меньше двух было слишком строго,
        // больше четырёх — никто уже не боится.
        public int Strikes;
        public bool GameOver;
        public bool PlanDone;
        public int Premium;

        // План на текущий день. Раньше тут лежала одна цифра «заработай $X»,
        // но это скучно: можно было продать всё подряд. Теперь нужны конкретные детали.
        public readonly List<PlanItem> Plan = new List<PlanItem>();

        // Счётчики «сделано за смену» — отдельно от общего Produced,
        // потому что план сбрасывается каждый день.
        public readonly int[] ShiftMade = new int[ProductInfo.Count];
        public readonly int[] ShiftMadeAssembly = new int[Recipes.All.Length];

        // Общая статистика за всю игру — её показывает таблица.
        // ShiftSold/ShiftRevenue — только за текущую смену.
        public int TotalBought;
        public int TotalProduced;
        public int TotalSold;
        public float TotalRevenue;
        public float TotalSpent;
        public float TotalSubsidy;
        public float TotalBonus;

        public int ShiftSold;
        public float ShiftRevenue;

        // Последнее событие для баннера сверху и журнал для таблицы.
        // Журнал не чистим, но в таблице показываем только хвост.
        public string LastEvent = "";
        public EventTone LastTone = EventTone.Info;
        public readonly List<string> Log = new List<string>();

        public readonly List<LineState> Lines = new List<LineState>();
        public readonly List<GeneratorState> Generators = new List<GeneratorState>();

        // Сборка лежит отдельным списком, чтобы не трогать старые линии.
        // Индекс совпадает с Recipes.All — на этом завязано вообще всё в UI.
        public readonly List<AssemblyLine> Assembly = new List<AssemblyLine>();

        public FactorySim()
        {
            // На старте работает только линия шестерёнок. Корпуса и микросхемы
            // игрок открывает сам, когда накопит денег и энергии.
            Lines.Add(new LineState(ProductKind.Gear, true));
            Lines.Add(new LineState(ProductKind.Case, false));
            Lines.Add(new LineState(ProductKind.Chip, false));
            Generators.Add(new GeneratorState());
            Generators.Add(new GeneratorState());

            for (int i = 0; i < Recipes.All.Length; i++) Assembly.Add(new AssemblyLine(Recipes.All[i]));

            BeginDay();
        }

        public int EnergyUsed
        {
            get
            {
                // Считаем каждый раз заново. Списки маленькие, оптимизировать нечего,
                // зато невозможно забыть обновить поле после постройки.
                int used = 0;
                for (int i = 0; i < Lines.Count; i++) used += Lines[i].EnergyDraw;
                for (int i = 0; i < Assembly.Count; i++) if (Assembly[i].Built) used += Assembly[i].EnergyDraw;
                return used;
            }
        }

        public int EnergyCapacity
        {
            get
            {
                int capacity = 0;
                for (int i = 0; i < Generators.Count; i++) capacity += Generators[i].Capacity;
                return capacity;
            }
        }

        public bool EnergyOk { get { return EnergyUsed <= EnergyCapacity; } }

        // Инфляция: каждый день всё дороже на 7%. Без неё к десятому дню
        // денег становится столько, что играть уже неинтересно.
        public float PriceScale { get { return 1f + 0.07f * (Day - 1); } }

        // Сырьё дорожает медленнее — раз в три дня на $1.
        public int MaterialPrice { get { return ProductInfo.MaterialCost + (Day - 1) / 3; } }

        public int SalePrice(LineState line)
        {
            return (int)System.Math.Round(line.Seller.BaseSalePrice(line.Kind) * PriceScale);
        }

        public int AssemblyPrice(int index)
        {
            return (int)System.Math.Round(Assembly[index].Price * PriceScale);
        }

        // Дневные нормы. Колонки: шестерёнки, корпуса, микросхемы, редукторы, приводы, турбины.
        //
        // Считал от реальной скорости линий за 40-секундную смену:
        //   шестерёнки: 25 шт на Lv1, 32 на Lv2, 39 на Lv3
        //   корпуса:    12 на Lv1, 16 на Lv2, 19 на Lv3
        //   микросхемы:  8 шт на Lv1, 11 на Lv2, 13 на Lv3
        // План держу примерно на 70-85% этой ёмкости — остаётся запас на брак,
        // нехватку сырья и обычную человеческую невнимательность.
        //
        // Важно: сборка съедает детали. На 2 редуктора нужно ещё 4 шестерёнки
        // и 2 корпуса, а на привод — ещё редуктор и 2 микросхемы.
        // Поэтому в дни со сборкой нормы по линиям чуть ниже.
        //
        // Дальше 12-го дня таблица не кончается — план растёт на 8% в день.
        static readonly int[,] PlanTable =
        {
            { 12,  0,  0, 0, 0, 0 }, // 1 — только шестерёнки, спокойное знакомство
            { 15,  0,  0, 0, 0, 0 }, // 2
            { 17,  4,  0, 0, 0, 0 }, // 3 — пора открывать корпуса
            { 19,  6,  0, 0, 0, 0 }, // 4
            { 21,  8,  0, 0, 0, 0 }, // 5 — тут уже хочется прокачать шестерёнки
            { 23, 10,  0, 0, 0, 0 }, // 6
            { 24, 11,  3, 0, 0, 0 }, // 7 — подключаются микросхемы
            { 24, 11,  3, 1, 0, 0 }, // 8 — первый редуктор
            { 25, 12,  4, 2, 0, 0 }, // 9
            { 25, 13,  5, 2, 1, 0 }, // 10 — первый привод
            { 26, 13,  6, 3, 2, 0 }, // 11
            { 27, 12,  6, 3, 2, 1 }, // 12 — турбина, дальше масштабируем
        };

        // Премия растёт линейно, чтобы к поздним дням она всё ещё была заметной
        // на фоне выручки. Формулу можно крутить, она ни к чему не привязана.
        public static int PlanPremium(int day)
        {
            return 80 + 35 * (day - 1);
        }

        public void BuildPlan(int day)
        {
            Plan.Clear();

            int rows = PlanTable.GetLength(0);
            int row = Math.Min(day, rows) - 1;
            int extra = day - rows;

            for (int i = 0; i < 6; i++)
            {
                int need = PlanTable[row, i];
                if (need <= 0) continue;

                // после конца таблицы каждый пункт потихоньку растёт
                if (extra > 0) need += (int)Math.Ceiling(need * 0.08 * extra);

                int part = i < 3 ? i : 10 + (i - 3);
                Plan.Add(new PlanItem(part, need));
            }
        }

        // сколько единиц пункта плана сделано за эту смену
        public int DoneFor(int part)
        {
            if (part < 10) return ShiftMade[part];
            return ShiftMadeAssembly[part - 10];
        }

        public bool PlanComplete
        {
            get
            {
                if (Plan.Count == 0) return false;
                for (int i = 0; i < Plan.Count; i++)
                    if (DoneFor(Plan[i].Part) < Plan[i].Need) return false;
                return true;
            }
        }

        // сколько типов деталей из плана уже закрыто — для короткой надписи вверху
        public int PlanDoneCount
        {
            get
            {
                int done = 0;
                for (int i = 0; i < Plan.Count; i++)
                    if (DoneFor(Plan[i].Part) >= Plan[i].Need) done++;
                return done;
            }
        }

        public string PlanText()
        {
            string text = "";
            for (int i = 0; i < Plan.Count; i++)
            {
                PlanItem item = Plan[i];
                if (text.Length > 0) text += " · ";
                text += item.Name + " " + DoneFor(item.Part) + "/" + item.Need;
            }
            return text;
        }

        public int TotalUpkeep
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < Generators.Count; i++) sum += Generators[i].DailyUpkeep;
                return sum;
            }
        }

        public int TotalMaterials
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < Lines.Count; i++) sum += Lines[i].Materials;
                return sum;
            }
        }

        public int TotalProducts
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < Lines.Count; i++) sum += Lines[i].Products;
                return sum;
            }
        }

        public float ShiftRemaining
        {
            get { return ShiftActive ? Math.Max(0f, ShiftLength - ShiftElapsed) : 0f; }
        }

        public string PhaseText
        {
            get
            {
                if (ShiftActive) return "СМЕНА " + ShiftRemaining.ToString("0.0") + "с";
                if (Skipped) return "ПРОПУСК · " + Math.Max(0f, NextDayTimer).ToString("0.0") + "с";
                return "ПЕРЕРЫВ " + Math.Max(0f, NextDayTimer).ToString("0.0") + "с";
            }
        }

        public static string MachineName(MachineKind kind)
        {
            switch (kind)
            {
                case MachineKind.Buyer: return "Закупщик";
                case MachineKind.Producer: return "Производитель";
                default: return "Продавец";
            }
        }

        // Начало дня. Сначала строим план и сбрасываем счётчики,
        // потом списываем топливо. Если денег на топливо нет — субсидия и страйк,
        // но день всё равно считается пропущенным, работать нельзя.
        public void BeginDay()
        {
            Day++;
            ShiftElapsed = 0f;
            Skipped = false;
            PlanDone = false;
            ShiftSold = 0;
            ShiftRevenue = 0f;
            BuildPlan(Day);
            Premium = PlanPremium(Day);
            PendingUpkeep = TotalUpkeep;

            // Брак в новый день начинается с чистого листа.
            // Первую красную деталь подкидываем не сразу, чтобы игрок успел втянуться.
            DefectsThisShift = 0;
            DefectSpawnTimer = _rng.Next(16, 26);
            for (int i = 0; i < Lines.Count; i++)
            {
                Lines[i].Defect = false;
                Lines[i].DefectLeft = 0f;
            }

            for (int i = 0; i < ShiftMade.Length; i++) ShiftMade[i] = 0;
            for (int i = 0; i < ShiftMadeAssembly.Length; i++) ShiftMadeAssembly[i] = 0;

            // Если кто-то был в процессе — не сбрасываем таймер, пусть доделает.
            // А вот продажу и простаивающие станки стартуем с чистого листа.

            for (int i = 0; i < Lines.Count; i++)
            {
                LineState line = Lines[i];
                if (!line.Built) continue;

                ResetTimer(line.Buyer);
                ResetTimer(line.Producer);
                ResetTimer(line.Seller);
            }

            for (int i = 0; i < Assembly.Count; i++)
            {
                if (!Assembly[i].Busy) Assembly[i].Timer = 0f;
                Assembly[i].SellTimer = 0f;
            }

            if (Money + 0.001f < PendingUpkeep)
            {
                float subsidy = PendingUpkeep - Money + 30f;
                Money += subsidy;
                TotalSubsidy += subsidy;
                ShiftActive = false;
                Skipped = true;
                NextDayTimer = 2.5f;
                Strike();
                SetEvent("День " + Day + ": топливо не оплачено. Субсидия +$" + subsidy.ToString("0") +
                         " · провал " + Strikes + "/" + MaxStrikes, EventTone.Bad);
                return;
            }

            Money -= PendingUpkeep;
            TotalSpent += PendingUpkeep;
            ShiftActive = true;
            NextDayTimer = 0f;
            SetEvent("День " + Day + ": план " + PlanText() + " · топливо −$" + PendingUpkeep, EventTone.Info);
        }

        void Strike()
        {
            Strikes++;
            if (Strikes >= MaxStrikes) GameOver = true;
        }

        static void ResetTimer(MachineState machine)
        {
            if (!machine.Busy) machine.Timer = 0f;
        }

        // Конец смены: смотрим, закрыт ли план по деталям.
        // Если да — платим премию, если нет — страйк (на третьем игра заканчивается).
        public void EndDay()
        {
            ShiftActive = false;
            ShiftElapsed = ShiftLength;
            NextDayTimer = 2.5f;

            if (PlanComplete)
            {
                PlanDone = true;
                Money += Premium;
                TotalRevenue += Premium;
                TotalBonus += Premium;
                SetEvent("День " + Day + " закрыт: план выполнен (" + PlanText() + ") · премия +$" + Premium, EventTone.Good);
            }
            else
            {
                Strike();
                SetEvent("День " + Day + " закрыт: план провален (" + PlanText() + ") · провал " + Strikes + "/" + MaxStrikes, EventTone.Bad);
            }
        }

        // Главный тик. Порядок важен:
        //   1) сборка (ей нужны детали, сделанные в прошлый тик),
        //   2) линии (закупка -> производство -> продажа),
        //   3) конец смены / следующий день.
        // Если переставить местами — появятся странные скачки складов.
        public void Tick(float dt)
        {
            // На паузе вообще ничего не считаем: ни деньги, ни таймеры, ни брак.
            if (dt <= 0f || GameOver || Paused) return;

            if (!ShiftActive)
            {
                if (NextDayTimer > 0f)
                {
                    NextDayTimer -= dt;
                    if (NextDayTimer <= 0f) BeginDay();
                }
                return;
            }

            ShiftElapsed += dt;
            if (ShiftElapsed >= ShiftLength)
            {
                EndDay();
                return;
            }

            // Брак проверяем после таймера смены, но до станков:
            // если деталь уже испортилась, она должна исчезнуть со склада в этом же тике.
            TickDefects(dt);

            bool energyOk = EnergyOk;

            // Сначала сборка, потом обычные линии — сборке нужны детали,
            // которые производитель успел сделать в прошлый тик.
            // (Если поменять порядок, детали будут уходить продавцу раньше цеха.)
            for (int i = 0; i < Assembly.Count; i++)
            {
                AssemblyLine shop = Assembly[i];
                if (!shop.Built) continue;

                if (energyOk) TickAssembly(shop, i, dt);
                else
                {
                    shop.Busy = false;
                    shop.Status = "НЕТ ЭНЕРГИИ";
                }
            }

            for (int i = 0; i < Lines.Count; i++)
            {
                LineState line = Lines[i];
                if (!line.Built) continue;

                for (int m = 0; m < 3; m++)
                {
                    MachineState machine = line.Machine((MachineKind)m);
                    machine.Blocked = !energyOk;
                    if (!energyOk) continue;

                    switch (machine.Kind)
                    {
                        case MachineKind.Buyer:
                            TickBuyer(line, machine, dt);
                            break;
                        case MachineKind.Producer:
                            TickProducer(line, machine, dt);
                            break;
                        default:
                            TickSeller(line, machine, dt);
                            break;
                    }
                }
            }
        }

        // Брак живёт своей жизнью: тикает, может испортиться и иногда появляется заново.
        // Специально держим не больше одной красной детали сразу — иначе это уже не
        // «занять паузу», а вторая работа.
        void TickDefects(float dt)
        {
            bool active = false;

            for (int i = 0; i < Lines.Count; i++)
            {
                LineState line = Lines[i];
                if (!line.Defect) continue;

                line.DefectLeft -= dt;
                if (line.DefectLeft > 0f)
                {
                    active = true;
                    continue;
                }

                // Не успели убрать. Никаких страйков: просто минус один готовый товар.
                // Если склад и так пустой, считаем, что обошлось.
                line.Defect = false;
                line.DefectLeft = 0f;
                DefectsMissed++;

                if (line.Products > 0)
                {
                    line.Products--;
                    SetEvent("Брак на «" + line.Name + "» уехал в партию · −1 товар", EventTone.Bad);
                }
                else
                {
                    SetEvent("Брак на «" + line.Name + "» уехал в партию, но склад был пуст", EventTone.Info);
                }
            }

            if (active) return;
            if (DefectsThisShift >= DefectsPerShift) return;

            DefectSpawnTimer -= dt;
            if (DefectSpawnTimer > 0f) return;

            SpawnRandomDefect();
        }

        void SpawnRandomDefect()
        {
            // Ищем линии, где есть куда класть брак.
            // Проще собрать индексы в список, чем изобретать выбор без повторов.
            var candidates = new List<int>();
            for (int i = 0; i < Lines.Count; i++)
                if (Lines[i].Built) candidates.Add(i);

            if (candidates.Count == 0)
            {
                DefectSpawnTimer = 5f;
                return;
            }

            int index = candidates[_rng.Next(candidates.Count)];
            SpawnDefect(index);
        }

        // Публичная версия нужна тестам и отладке: поставить брак на конкретную линию.
        public bool SpawnDefect(int index)
        {
            if (index < 0 || index >= Lines.Count) return false;

            LineState line = Lines[index];
            if (!line.Built) return false;
            if (line.Defect) return false;
            if (!ShiftActive) return false;

            line.Defect = true;
            line.DefectLeft = DefectTime;
            DefectsThisShift++;
            DefectSpawnTimer = _rng.Next(18, 29);
            SetEvent("Брак на «" + line.Name + "»! Успейте убрать за " + (int)DefectTime + "с", EventTone.Bad);
            return true;
        }

        // Игрок кликнул по красной детали (в зале или по кнопке вверху).
        // Награда крошечная, чтобы не было смысла специально ждать брак.
        public bool FixDefect(int index)
        {
            if (index < 0 || index >= Lines.Count) return false;

            LineState line = Lines[index];
            if (!line.Defect) return false;

            line.Defect = false;
            line.DefectLeft = 0f;
            DefectsCaught++;
            const int reward = 5;

            Money += reward;
            TotalRevenue += reward;
            ShiftRevenue += reward;
            TotalBonus += reward;
            SetEvent("Брак на «" + line.Name + "» убран · +$" + reward, EventTone.Good);
            return true;
        }

        // Закупщик берёт партию по 2 штуки, чтобы не тратить деньги каждый кадр.
        // Если склад полон или денег нет — просто ждёт и пробует снова через 0.3с.
        void TickBuyer(LineState line, MachineState machine, float dt)
        {
            machine.Timer -= dt;
            if (machine.Timer > 0f) return;

            int bought = 0;
            int unit = MaterialPrice;
            while (bought < machine.BuyBatch &&
                   line.Materials < ProductInfo.MaterialCap &&
                   Money + 0.001f >= unit)
            {
                Money -= unit;
                TotalSpent += unit;
                line.Materials++;
                TotalBought++;
                bought++;
            }

            if (bought > 0)
            {
                machine.Status = "ЗАКУПКА";
                machine.Timer = machine.BuyInterval;
            }
            else
            {
                machine.Status = line.Materials >= ProductInfo.MaterialCap ? "СКЛАД ПОЛОН" : "НЕТ ДЕНЕГ";
                machine.Timer = 0.3f;
            }
        }

        // Производитель. Пока Busy — просто тикает таймер.
        // После готовности стоит 0.12с: без этой паузы за один кадр
        // могло выйти несколько деталей подряд, и склады прыгали.
        void TickProducer(LineState line, MachineState machine, float dt)
        {
            if (machine.Busy)
            {
                machine.Timer -= dt;
                machine.Status = "РАБОТА";
                if (machine.Timer > 0f) return;

                line.Products++;
                line.Produced++;
                TotalProduced++;
                ShiftMade[(int)line.Kind]++;
                machine.Busy = false;
                machine.Timer = 0.12f;
                machine.Status = "ГОТОВО";
                return;
            }

            machine.Timer -= dt;
            if (machine.Timer > 0f) return;

            if (line.Products >= ProductInfo.ProductCap)
            {
                machine.Status = "СКЛАД ПОЛОН";
                machine.Timer = 0.3f;
                return;
            }

            int need = ProductInfo.Materials(line.Kind);
            if (line.Materials < need)
            {
                machine.Status = "ЖДЁТ МАТЕРИАЛ";
                machine.Timer = 0.3f;
                return;
            }

            line.Materials -= need;
            machine.Busy = true;
            machine.Timer = machine.CraftTime;
            machine.Status = "РАБОТА";
        }

        // Продавец. Тут же начисляется выручка смены — по ней раньше считался план,
        // теперь план по деталям, но деньги всё равно нужны.
        void TickSeller(LineState line, MachineState machine, float dt)
        {
            machine.Timer -= dt;
            if (machine.Timer > 0f) return;

            if (line.Products <= 0)
            {
                machine.Status = "ЖДЁТ ТОВАР";
                machine.Timer = 0.3f;
                return;
            }

            line.Products--;
            int price = SalePrice(line);
            Money += price;
            TotalRevenue += price;
            ShiftRevenue += price;
            TotalSold++;
            ShiftSold++;
            machine.Status = "ПРОДАЖА";
            machine.Timer = machine.SellInterval;
        }

        // Самая хитрая часть. Сначала детали по одной перетаскиваются в Hopper,
        // потом продаётся готовое (если его не ждёт следующая сборка),
        // и только потом пост пытается начать новый цикл.
        void TickAssembly(AssemblyLine shop, int index, float dt)
        {
            Recipe r = shop.Recipe;

            // Потихоньку тащим детали на верстак. Отдельного общего склада нет:
            // берём прямо из готовой продукции линий (или другой сборки).
            // Один слот за тик — специально, иначе за кадр можно утащить всё и продавец останется с носом.
            if (shop.Hopper[0] < r.AN + 1 && PartStock(r.A) > 0)
            {
                TakePart(r.A, 1);
                shop.Hopper[0]++;
            }

            if (shop.Hopper[1] < r.BN + 1 && PartStock(r.B) > 0)
            {
                TakePart(r.B, 1);
                shop.Hopper[1]++;
            }

            // продажа готового
            shop.SellTimer -= dt;
            if (shop.SellTimer <= 0f && shop.Output > 0 && !WantsPart(index))
            {
                shop.Output--;
                int price = AssemblyPrice(index);
                Money += price;
                TotalRevenue += price;
                ShiftRevenue += price;
                TotalSold++;
                ShiftSold++;
                shop.SellTimer = shop.SellTime;
            }

            if (shop.Busy)
            {
                shop.Timer -= dt;
                if (shop.Timer > 0f)
                {
                    shop.Status = "СБОРКА";
                    return;
                }

                shop.Busy = false;
                shop.Output++;
                shop.Produced++;
                TotalProduced++;
                ShiftMadeAssembly[index]++;
                shop.Status = "ГОТОВО";
                return;
            }

            if (shop.Output >= AssemblyLine.Cap)
            {
                shop.Status = "СКЛАД ПОЛОН";
                return;
            }

            if (shop.Hopper[0] < r.AN || shop.Hopper[1] < r.BN)
            {
                shop.Status = "ЖДЁТ ДЕТАЛИ";
                return;
            }

            shop.Hopper[0] -= r.AN;
            shop.Hopper[1] -= r.BN;
            shop.Busy = true;
            shop.Timer = shop.CraftTime;
            shop.Status = "СБОРКА";
        }

        // Если следующая сборка ждёт эту деталь — не продаём, пусть сначала дойдёт до цеха.
        // Иначе редукторы уходили бы в магазин быстрее, чем приводы успевали их забрать.
        bool WantsPart(int index)
        {
            int id = 10 + index;
            for (int i = 0; i < Assembly.Count; i++)
            {
                AssemblyLine shop = Assembly[i];
                if (!shop.Built) continue;
                if (shop.Recipe.A == id && shop.Hopper[0] < shop.Recipe.AN + 1) return true;
                if (shop.Recipe.B == id && shop.Hopper[1] < shop.Recipe.BN + 1) return true;
            }

            return false;
        }

        // id < 10 — это склад линии, 10+ — склад сборки
        int PartStock(int id)
        {
            if (id < 10) return Lines[id].Products;
            return Assembly[id - 10].Output;
        }

        void TakePart(int id, int count)
        {
            if (id < 10) Lines[id].Products -= count;
            else Assembly[id - 10].Output -= count;
        }

        // Постройка линии. Проверяем деньги и энергию ДО списания,
        // иначе можно уйти в минус и получить странное состояние.
        // 0.001f — костыль для сравнения float: у нас деньги дробные из-за премий.
        public UpgradeStatus TryBuildLine(int index)
        {
            if (index < 0 || index >= Lines.Count) return UpgradeStatus.MaxLevel;
            LineState line = Lines[index];
            if (line.Built) return UpgradeStatus.AlreadyBuilt;

            int cost = line.BuildCost;
            if (Money + 0.001f < cost) return UpgradeStatus.NotEnoughMoney;
            if (EnergyUsed + LineEnergy > EnergyCapacity) return UpgradeStatus.NotEnoughEnergy;

            Money -= cost;
            TotalSpent += cost;
            line.Built = true;
            SetEvent("Линия «" + line.Name + "» запущена · −$" + cost, EventTone.Good);
            return UpgradeStatus.Ok;
        }

        // Апгрейд станка. Считаем, сколько энергии будет ПОСЛЕ апгрейда:
        // если не влезает в генераторы — отказываем, а не отключаем завод.
        public UpgradeStatus TryUpgradeMachine(int lineIndex, MachineKind kind)
        {
            if (lineIndex < 0 || lineIndex >= Lines.Count) return UpgradeStatus.MaxLevel;
            LineState line = Lines[lineIndex];
            if (!line.Built) return UpgradeStatus.NotBuilt;

            MachineState machine = line.Machine(kind);
            if (machine.Level >= MaxLevel) return UpgradeStatus.MaxLevel;

            int cost = machine.UpgradeCost;
            if (Money + 0.001f < cost) return UpgradeStatus.NotEnoughMoney;

            int newUsed = EnergyUsed - machine.EnergyDraw + machine.NextEnergyDraw;
            if (newUsed > EnergyCapacity) return UpgradeStatus.NotEnoughEnergy;

            Money -= cost;
            TotalSpent += cost;
            machine.Level++;
            SetEvent(line.Name + ": " + MachineName(kind) + " Lv " + machine.Level + " · −$" + cost, EventTone.Good);
            return UpgradeStatus.Ok;
        }

        // Сборка строится и качается почти как линия, только энергия у неё
        // зависит от рецепта (3 + уровень), а не фиксированные 6.
        public UpgradeStatus TryBuildAssembly(int index)
        {
            if (index < 0 || index >= Assembly.Count) return UpgradeStatus.MaxLevel; // кривой индекс — молча игнорим

            AssemblyLine shop = Assembly[index];
            if (shop.Built) return UpgradeStatus.AlreadyBuilt;
            if (Money + 0.001f < shop.BuildCost) return UpgradeStatus.NotEnoughMoney;
            if (EnergyUsed + shop.EnergyDraw > EnergyCapacity) return UpgradeStatus.NotEnoughEnergy;

            Money -= shop.BuildCost;
            TotalSpent += shop.BuildCost;
            shop.Built = true;
            shop.Status = "ЖДЁТ ДЕТАЛИ";
            SetEvent("Сборка «" + shop.Name + "» запущена · −$" + shop.BuildCost, EventTone.Good);
            return UpgradeStatus.Ok;
        }

        public UpgradeStatus TryUpgradeAssembly(int index)
        {
            if (index < 0 || index >= Assembly.Count) return UpgradeStatus.MaxLevel;

            AssemblyLine shop = Assembly[index];
            if (!shop.Built) return UpgradeStatus.NotBuilt;
            if (shop.Level >= MaxLevel) return UpgradeStatus.MaxLevel;

            int cost = shop.UpgradeCost;
            if (Money + 0.001f < cost) return UpgradeStatus.NotEnoughMoney;

            int newUsed = EnergyUsed - shop.EnergyDraw + shop.NextEnergyDraw;
            if (newUsed > EnergyCapacity) return UpgradeStatus.NotEnoughEnergy;

            Money -= cost;
            TotalSpent += cost;
            shop.Level++;
            SetEvent("Сборка «" + shop.Name + "» Lv " + shop.Level + " · −$" + cost, EventTone.Good);
            return UpgradeStatus.Ok;
        }

        // Генератор энергию не тратит, только даёт. Зато за него каждый день
        // берут топливо, так что качать его «на всякий случай» невыгодно.
        public UpgradeStatus TryUpgradeGenerator(int index)
        {
            if (index < 0 || index >= Generators.Count) return UpgradeStatus.MaxLevel;
            GeneratorState generator = Generators[index];
            if (generator.Level >= MaxLevel) return UpgradeStatus.MaxLevel;

            int cost = generator.UpgradeCost;
            if (Money + 0.001f < cost) return UpgradeStatus.NotEnoughMoney;

            Money -= cost;
            TotalSpent += cost;
            generator.Level++;
            SetEvent("Генератор " + (index + 1) + " Lv " + generator.Level + " · +" + generator.Capacity + " энергии", EventTone.Good);
            return UpgradeStatus.Ok;
        }

        // Журнал держим коротким: в таблице всё равно видно только последние строки.
        // 6 — с запасом, чтобы было что листать глазами.
        public void SetEvent(string text, EventTone tone)
        {
            LastEvent = text;
            LastTone = tone;
            Log.Add(text);
            while (Log.Count > 6) Log.RemoveAt(0);
        }
    }

    // Одна производственная линия = три станка + два склада.
    // Линии полностью независимы: своя закупка, свой производитель, свой продавец.
    // Поэтому в таблице у каждой линии отдельные уровни и остатки.
    public class LineState
    {
        public readonly ProductKind Kind;
        public bool Built;
        public int Materials;  // сырьё на складе
        public int Products;   // готовая продукция на складе
        public int Produced;   // всего сделано за игру, для статистики

        // Брак на складе: если не убрать за DefectTime секунд,
        // он испортит одну готовую деталь.
        public bool Defect;
        public float DefectLeft;

        public readonly MachineState Buyer;
        public readonly MachineState Producer;
        public readonly MachineState Seller;

        public LineState(ProductKind kind, bool built)
        {
            Kind = kind;
            Built = built;
            Buyer = new MachineState(MachineKind.Buyer, kind);
            Producer = new MachineState(MachineKind.Producer, kind);
            Seller = new MachineState(MachineKind.Seller, kind);
        }

        public string Name { get { return ProductInfo.Name(Kind); } }

        public int BuildCost { get { return ProductInfo.BuildCost(Kind); } }

        public int EnergyDraw
        {
            // Непостроенная линия энергии не ест — иначе старт был бы заблокирован.
            get { return Built ? Buyer.EnergyDraw + Producer.EnergyDraw + Seller.EnergyDraw : 0; }
        }

        public MachineState Machine(MachineKind kind)
        {
            switch (kind)
            {
                case MachineKind.Buyer: return Buyer;
                case MachineKind.Producer: return Producer;
                default: return Seller;
            }
        }
    }

    // Станок. Формулы специально простые, чтобы их можно было держать в голове:
    // каждый уровень = +28% скорости и +1 энергии.
    // Цена апгрейда растёт в 1.45 раза и округляется до 5.
    public class MachineState
    {
        public readonly MachineKind Kind;
        public readonly ProductKind Product;
        public int Level = 1;
        public float Timer;
        public bool Busy;
        public bool Blocked;
        public string Status = "ОЖИДАНИЕ";

        public MachineState(MachineKind kind, ProductKind product)
        {
            Kind = kind;
            Product = product;
        }

        public float Speed { get { return 1f + 0.28f * (Level - 1); } }
        public int EnergyDraw { get { return 1 + Level; } } // на первом уровне 2
        public int NextEnergyDraw { get { return EnergyDraw + 1; } }

        public float BuyInterval { get { return 3.2f / Speed; } }
        public int BuyBatch { get { return 2; } }
        public float SellInterval { get { return 1.6f / Speed; } }
        public float CraftTime { get { return ProductInfo.CraftTime(Product) / Speed; } }

        public int BaseSalePrice(ProductKind kind)
        {
            return ProductInfo.BasePrice(kind) + 3 * (Level - 1);
        }

        public int UpgradeCost
        {
            get
            {
                int baseCost = Kind == MachineKind.Buyer ? 45 : (Kind == MachineKind.Producer ? 60 : 75);
                return Round5(baseCost * Math.Pow(1.45, Level - 1));
            }
        }

        public float CycleProgress
        {
            get
            {
                if (Kind == MachineKind.Producer)
                {
                    if (!Busy) return 0f;
                    return Clamp01(1f - Timer / Math.Max(0.01f, CraftTime));
                }

                float interval = Kind == MachineKind.Buyer ? BuyInterval : SellInterval;
                return Clamp01(1f - Timer / Math.Max(0.01f, interval));
            }
        }

        static int Round5(double value)
        {
            return (int)(Math.Round(value / 5.0) * 5.0);
        }

        static float Clamp01(float value)
        {
            return value < 0f ? 0f : (value > 1f ? 1f : value);
        }
    }

    // Генератор. Даёт +4 энергии за уровень, но и топливо дорожает.
    // Два генератора на старте дают 8 — ровно хватает на одну линию с запасом.
    public class GeneratorState
    {
        public int Level = 1;

        public int Capacity { get { return 4 + 4 * (Level - 1); } }
        public int DailyUpkeep { get { return 10 + 5 * (Level - 1); } }

        public int UpgradeCost
        {
            get { return (int)(Math.Round(95.0 * Math.Pow(1.5, Level - 1) / 5.0) * 5.0); }
        }
    }
}
