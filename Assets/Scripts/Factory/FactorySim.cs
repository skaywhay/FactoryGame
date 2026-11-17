using System;
using System.Collections.Generic;

namespace Game.Factory
{
    public enum MachineKind
    {
        Buyer,
        Producer,
        Seller
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
        Info,
        Good,
        Bad
    }

    public enum ProductKind
    {
        Gear,
        Case,
        Chip
    }

    public static class ProductInfo
    {
        public const int Count = 3;
        public const int MaterialCost = 4;
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
            switch (kind)
            {
                case ProductKind.Gear: return "ШЕСТ.";
                case ProductKind.Case: return "КОРПУС";
                default: return "МИКРО";
            }
        }

        public static int Materials(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Gear: return 1;
                case ProductKind.Case: return 2;
                default: return 3;
            }
        }

        public static int BasePrice(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Gear: return 10;
                case ProductKind.Case: return 24;
                default: return 42;
            }
        }

        public static float CraftTime(ProductKind kind)
        {
            switch (kind)
            {
                case ProductKind.Gear: return 1.6f;
                case ProductKind.Case: return 3.2f;
                default: return 4.8f;
            }
        }

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

    public class FactorySim
    {
        public const int MaxLevel = 8;
        public const int LineEnergy = 6;
        public const int MaxStrikes = 3;

        public float Money = 300f;
        public int Day;

        public float ShiftLength = 40f;
        public float ShiftElapsed;
        public bool ShiftActive;
        public bool Skipped;
        public float NextDayTimer;
        public int PendingUpkeep;

        public int Strikes;
        public bool GameOver;
        public bool PlanDone;
        public int PlanTarget;
        public int Premium;

        public int TotalBought;
        public int TotalProduced;
        public int TotalSold;
        public float TotalRevenue;
        public float TotalSpent;
        public float TotalSubsidy;
        public float TotalBonus;

        public int ShiftSold;
        public float ShiftRevenue;

        public string LastEvent = "";
        public EventTone LastTone = EventTone.Info;
        public readonly List<string> Log = new List<string>();

        public readonly List<LineState> Lines = new List<LineState>();
        public readonly List<GeneratorState> Generators = new List<GeneratorState>();

        public FactorySim()
        {
            Lines.Add(new LineState(ProductKind.Gear, true));
            Lines.Add(new LineState(ProductKind.Case, false));
            Lines.Add(new LineState(ProductKind.Chip, false));
            Generators.Add(new GeneratorState());
            Generators.Add(new GeneratorState());
            BeginDay();
        }

        public int EnergyUsed
        {
            get
            {
                int used = 0;
                for (int i = 0; i < Lines.Count; i++) used += Lines[i].EnergyDraw;
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

        public float PriceScale { get { return 1f + 0.07f * (Day - 1); } }

        public int MaterialPrice { get { return ProductInfo.MaterialCost + (Day - 1) / 3; } }

        public int SalePrice(LineState line)
        {
            return (int)System.Math.Round(line.Seller.BaseSalePrice(line.Kind) * PriceScale);
        }

        public static int PlanFor(int day)
        {
            double value = 200.0 * System.Math.Pow(1.22, day - 1);
            return (int)(System.Math.Round(value / 10.0) * 10.0);
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

        public void BeginDay()
        {
            Day++;
            ShiftElapsed = 0f;
            Skipped = false;
            PlanDone = false;
            ShiftSold = 0;
            ShiftRevenue = 0f;
            PlanTarget = PlanFor(Day);
            Premium = 40 + 20 * (Day - 1);
            PendingUpkeep = TotalUpkeep;

            for (int i = 0; i < Lines.Count; i++)
            {
                LineState line = Lines[i];
                if (!line.Built) continue;

                ResetTimer(line.Buyer);
                ResetTimer(line.Producer);
                ResetTimer(line.Seller);
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
            SetEvent("День " + Day + ": план $" + PlanTarget + " · топливо −$" + PendingUpkeep, EventTone.Info);
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

        public void EndDay()
        {
            ShiftActive = false;
            ShiftElapsed = ShiftLength;
            NextDayTimer = 2.5f;

            if (ShiftRevenue + 0.5f >= PlanTarget)
            {
                PlanDone = true;
                Money += Premium;
                TotalRevenue += Premium;
                TotalBonus += Premium;
                SetEvent("День " + Day + " закрыт: план $" + PlanTarget + " выполнен · премия +$" + Premium +
                         " · завтра план $" + PlanFor(Day + 1), EventTone.Good);
            }
            else
            {
                Strike();
                SetEvent("День " + Day + " закрыт: план провален ($" + ShiftRevenue.ToString("0") + " из $" + PlanTarget +
                         ") · провал " + Strikes + "/" + MaxStrikes, EventTone.Bad);
            }
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || GameOver) return;

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

            bool energyOk = EnergyOk;
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

        void TickProducer(LineState line, MachineState machine, float dt)
        {
            if (machine.Busy)
            {
                machine.Timer -= dt;
                machine.Status = "РАБОТА";
                if (machine.Timer > 0f) return;

                line.Products++;
                TotalProduced++;
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

        public void SetEvent(string text, EventTone tone)
        {
            LastEvent = text;
            LastTone = tone;
            Log.Add(text);
            while (Log.Count > 6) Log.RemoveAt(0);
        }
    }

    public class LineState
    {
        public readonly ProductKind Kind;
        public bool Built;
        public int Materials;
        public int Products;

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
        public int EnergyDraw { get { return 1 + Level; } }
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
