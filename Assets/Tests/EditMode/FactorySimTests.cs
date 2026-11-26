using NUnit.Framework;
using Game.Factory;

namespace Game.Tests
{
    // Тесты чистой симуляции: без сцены, без Unity-объектов, просто вызовы методов.
    // Здесь проверяется экономика: план, энергия, склады, сборка и апгрейды.
    // Гонять после каждого изменения баланса — они быстрые.
    public class FactorySimTests
    {
        static FactorySim NewSim()
        {
            return new FactorySim();
        }

        static void Freeze(MachineState machine)
        {
            if (machine.Kind == MachineKind.Producer) machine.Busy = true;
            machine.Timer = 999f;
        }

        [Test]
        public void NewSim_StartsWithOneBuiltLine()
        {
            var sim = NewSim();

            Assert.AreEqual(ProductInfo.Count, sim.Lines.Count);
            Assert.IsTrue(sim.Lines[0].Built);
            Assert.IsFalse(sim.Lines[1].Built);
            Assert.IsFalse(sim.Lines[2].Built);
            Assert.AreEqual(6, sim.EnergyUsed);
            Assert.AreEqual(8, sim.EnergyCapacity);
        }

        [Test]
        public void EachLine_HasOwnMachinesAndStock()
        {
            var sim = NewSim();
            LineState first = sim.Lines[0];
            LineState second = sim.Lines[1];

            Assert.AreNotSame(first.Buyer, second.Buyer);
            Assert.AreEqual(ProductKind.Gear, first.Buyer.Product);
            Assert.AreEqual(ProductKind.Case, second.Buyer.Product);

            first.Materials = 5;
            Assert.AreEqual(5, sim.TotalMaterials);
            Assert.AreEqual(0, second.Materials);
        }

        [Test]
        public void UnbuiltLine_DoesNotWorkAndTakesNoEnergy()
        {
            var sim = NewSim();
            Freeze(sim.Lines[0].Buyer);
            Freeze(sim.Lines[0].Producer);
            Freeze(sim.Lines[0].Seller);
            sim.Money = 1000f;

            sim.Tick(2f);

            Assert.AreEqual(0, sim.Lines[1].Materials);
            Assert.AreEqual(0, sim.Lines[1].Products);
            Assert.AreEqual(6, sim.EnergyUsed);
        }

        [Test]
        public void Buyer_BuysBatchAndSpendsMoney()
        {
            var sim = NewSim();
            LineState line = sim.Lines[0];
            Freeze(line.Producer);
            Freeze(line.Seller);

            float before = sim.Money;
            sim.Tick(0.02f);

            Assert.AreEqual(line.Buyer.BuyBatch, line.Materials);
            Assert.AreEqual(before - line.Buyer.BuyBatch * ProductInfo.MaterialCost, sim.Money, 0.01f);
        }

        [Test]
        public void Buyer_StopsAtStorageLimit()
        {
            var sim = NewSim();
            LineState line = sim.Lines[0];
            Freeze(line.Producer);
            Freeze(line.Seller);
            line.Materials = ProductInfo.MaterialCap;
            sim.Money = 1000f;

            sim.Tick(4f);

            Assert.AreEqual(ProductInfo.MaterialCap, line.Materials);
        }

        [Test]
        public void Producer_WaitsWithoutMaterials()
        {
            var sim = NewSim();
            LineState line = sim.Lines[0];
            sim.Money = 0f;
            line.Materials = 0;

            sim.Tick(3f);

            Assert.AreEqual(0, line.Products);
            Assert.IsFalse(line.Producer.Busy);
        }

        [Test]
        public void Producer_RequiresMaterialsForItsRecipe()
        {
            var sim = NewSim();
            LineState line = sim.Lines[0];
            Freeze(line.Seller);
            sim.Money = 0f;
            line.Materials = 2;

            sim.Tick(0.02f);
            Assert.IsTrue(line.Producer.Busy);
            Assert.AreEqual(1, line.Materials);

            sim.Tick(2f);
            Assert.AreEqual(1, line.Products);
            Assert.AreEqual(1, line.Materials);
        }

        [Test]
        public void Producer_StopsWhenProductStorageIsFull()
        {
            var sim = NewSim();
            LineState line = sim.Lines[0];
            Freeze(line.Seller);
            sim.Money = 0f;
            line.Materials = 5;
            line.Products = ProductInfo.ProductCap;

            sim.Tick(0.02f);

            Assert.IsFalse(line.Producer.Busy);
            Assert.AreEqual(5, line.Materials);
        }

        [Test]
        public void Seller_WaitsWithoutProducts()
        {
            var sim = NewSim();
            LineState line = sim.Lines[0];
            Freeze(line.Producer);
            sim.Money = 0f;

            sim.Tick(2f);

            Assert.AreEqual(0f, sim.Money, 0.01f);
        }

        [Test]
        public void Seller_SellsProductForBasePrice()
        {
            var sim = NewSim();
            LineState line = sim.Lines[0];
            Freeze(line.Producer);
            sim.Money = 0f;
            line.Products = 1;

            sim.Tick(0.02f);

            Assert.AreEqual(ProductInfo.BasePrice(ProductKind.Gear), sim.Money, 0.01f);
            Assert.AreEqual(0, line.Products);
        }

        [Test]
        public void Seller_PriceGrowsWithLevel()
        {
            var sim = NewSim();
            sim.Lines[0].Seller.Level = 3;

            Assert.AreEqual(ProductInfo.BasePrice(ProductKind.Gear) + 6, sim.Lines[0].Seller.BaseSalePrice(ProductKind.Gear));
        }

        [Test]
        public void MachineUpgrade_IncreasesEnergyDraw()
        {
            var sim = NewSim();
            sim.Money = 1000f;
            int before = sim.Lines[0].Buyer.EnergyDraw;

            Assert.AreEqual(UpgradeStatus.Ok, sim.TryUpgradeMachine(0, MachineKind.Buyer));
            Assert.AreEqual(before + 1, sim.Lines[0].Buyer.EnergyDraw);
            Assert.AreEqual(2, sim.Lines[0].Buyer.Level);
        }

        [Test]
        public void MachineUpgrade_IsBlockedWhenEnergyWouldExceedCapacity()
        {
            var sim = NewSim();
            sim.Money = 10000f;
            LineState line = sim.Lines[0];

            Assert.AreEqual(UpgradeStatus.Ok, sim.TryUpgradeMachine(0, MachineKind.Buyer));
            Assert.AreEqual(UpgradeStatus.Ok, sim.TryUpgradeMachine(0, MachineKind.Buyer));
            Assert.AreEqual(sim.EnergyCapacity, sim.EnergyUsed);
            Assert.AreEqual(UpgradeStatus.NotEnoughEnergy, sim.TryUpgradeMachine(0, MachineKind.Producer));
            Assert.AreEqual(UpgradeStatus.NotEnoughEnergy, sim.TryUpgradeMachine(0, MachineKind.Seller));
        }

        [Test]
        public void MachineUpgrade_IsBlockedWithoutMoney()
        {
            var sim = NewSim();
            sim.Money = 0f;

            Assert.AreEqual(UpgradeStatus.NotEnoughMoney, sim.TryUpgradeMachine(0, MachineKind.Buyer));
        }

        [Test]
        public void MachineUpgrade_IsBlockedOnUnbuiltLine()
        {
            var sim = NewSim();
            sim.Money = 10000f;

            Assert.AreEqual(UpgradeStatus.NotBuilt, sim.TryUpgradeMachine(1, MachineKind.Buyer));
        }

        [Test]
        public void BuildLine_NeedsFreeEnergy()
        {
            var sim = NewSim();
            sim.Money = 10000f;

            Assert.AreEqual(UpgradeStatus.NotEnoughEnergy, sim.TryBuildLine(1));
            Assert.IsFalse(sim.Lines[1].Built);
        }

        [Test]
        public void BuildLine_NeedsMoney()
        {
            var sim = NewSim();
            sim.Money = 0f;
            sim.TryUpgradeGenerator(0);

            Assert.AreEqual(UpgradeStatus.NotEnoughMoney, sim.TryBuildLine(2));
        }

        [Test]
        public void BuildLine_AddsMachinesEnergyAndCost()
        {
            var sim = NewSim();
            sim.Money = 2000f;
            sim.TryUpgradeGenerator(0);
            sim.TryUpgradeGenerator(1);

            float before = sim.Money;
            int energy = sim.EnergyUsed;

            Assert.AreEqual(UpgradeStatus.Ok, sim.TryBuildLine(1));
            Assert.IsTrue(sim.Lines[1].Built);
            Assert.AreEqual(before - sim.Lines[1].BuildCost, sim.Money, 0.01f);
            Assert.AreEqual(energy + FactorySim.LineEnergy, sim.EnergyUsed);
            Assert.IsTrue(sim.EnergyOk);
        }

        [Test]
        public void BuiltLine_ProducesItsOwnProduct()
        {
            var sim = NewSim();
            sim.Money = 2000f;
            sim.TryUpgradeGenerator(0);
            sim.TryUpgradeGenerator(1);
            sim.TryBuildLine(1);

            LineState line = sim.Lines[1];
            Freeze(line.Buyer);
            Freeze(line.Seller);
            Freeze(sim.Lines[0].Buyer);
            Freeze(sim.Lines[0].Producer);
            Freeze(sim.Lines[0].Seller);
            line.Materials = 4;

            sim.Tick(0.02f);
            Assert.IsTrue(line.Producer.Busy);
            Assert.AreEqual(2, line.Materials);

            sim.Tick(3.4f);
            Assert.AreEqual(1, sim.Lines[1].Products);
            Assert.AreEqual(0, sim.Lines[0].Products);
        }

        [Test]
        public void GeneratorUpgrade_RaisesCapacityAndUpkeep()
        {
            var sim = NewSim();
            sim.Money = 1000f;
            int capacity = sim.EnergyCapacity;
            int upkeep = sim.TotalUpkeep;

            Assert.AreEqual(UpgradeStatus.Ok, sim.TryUpgradeGenerator(0));
            Assert.Greater(sim.EnergyCapacity, capacity);
            Assert.Greater(sim.TotalUpkeep, upkeep);
        }

        [Test]
        public void DayStart_PaysGeneratorUpkeep()
        {
            var sim = NewSim();
            float money = sim.Money;
            sim.BeginDay();

            Assert.AreEqual(money - sim.TotalUpkeep, sim.Money, 0.01f);
            Assert.IsTrue(sim.ShiftActive);
            Assert.IsFalse(sim.Skipped);
        }

        [Test]
        public void SkippedDay_GivesSubsidySoNextDayCanRun()
        {
            var sim = NewSim();
            sim.Money = 0f;
            sim.BeginDay();

            Assert.IsFalse(sim.ShiftActive);
            Assert.IsTrue(sim.Skipped);
            Assert.Greater(sim.TotalSubsidy, 0f);

            int day = sim.Day;
            sim.Tick(3f);

            Assert.IsTrue(sim.ShiftActive);
            Assert.AreEqual(day + 1, sim.Day);
        }

        [Test]
        public void ShiftEndsAndNextDayStartsAfterPause()
        {
            var sim = NewSim();
            int day = sim.Day;

            sim.Tick(sim.ShiftLength + 0.1f);
            Assert.IsFalse(sim.ShiftActive);
            Assert.AreEqual(day, sim.Day);

            sim.Tick(3f);
            Assert.AreEqual(day + 1, sim.Day);
        }

        [Test]
        public void LongRun_KeepsStockInsideLimits()
        {
            var sim = NewSim();
            sim.Money = 5000f;

            for (int i = 0; i < 4000; i++) sim.Tick(0.02f);

            for (int l = 0; l < sim.Lines.Count; l++)
            {
                LineState line = sim.Lines[l];
                Assert.LessOrEqual(line.Materials, ProductInfo.MaterialCap, "materials line " + l);
                Assert.LessOrEqual(line.Products, ProductInfo.ProductCap, "products line " + l);
            }

            Assert.Greater(sim.TotalSold, 0);
        }
        // План — это список конкретных деталей, а не сумма денег.
        // Первый день самый простой: только шестерёнки.
        [Test]
        public void Plan_AsksForSpecificParts()
        {
            var sim = NewSim();

            Assert.AreEqual(1, sim.Plan.Count);
            Assert.AreEqual(0, sim.Plan[0].Part);
            Assert.AreEqual(12, sim.Plan[0].Need);
            Assert.IsFalse(sim.PlanComplete);
        }

        // Проверяем, что таблица планов действительно добавляет новые детали,
        // а не залипает на первом дне.
        [Test]
        public void Plan_BringsNewPartsOnLaterDays()
        {
            var sim = NewSim();
            int guard = 0;
            while (!HasPlanPart(sim, 1) && guard++ < 10) sim.BeginDay();

            Assert.IsTrue(HasPlanPart(sim, 1), "Case line never appeared in the plan");
        }

        [Test]
        public void Plan_PaysPremiumWhenQuotaIsMade()
        {
            var sim = NewSim();
            sim.ShiftMade[0] = sim.Plan[0].Need;
            float before = sim.Money;
            int premium = sim.Premium;

            sim.EndDay();

            Assert.IsTrue(sim.PlanDone);
            Assert.AreEqual(before + premium, sim.Money, 0.01f);
        }

        static bool HasPlanPart(FactorySim sim, int part)
        {
            for (int i = 0; i < sim.Plan.Count; i++)
                if (sim.Plan[i].Part == part) return true;
            return false;
        }

        [Test]
        public void Assembly_NeedsEnergyAndMoney()
        {
            var sim = NewSim();
            sim.Money = 1000f;

            Assert.AreEqual(UpgradeStatus.NotEnoughEnergy, sim.TryBuildAssembly(0));

            sim.TryUpgradeGenerator(0);
            Assert.AreEqual(UpgradeStatus.Ok, sim.TryBuildAssembly(0));
            Assert.IsTrue(sim.Assembly[0].Built);
            Assert.AreEqual(9, sim.EnergyUsed);
        }

        [Test]
        public void Assembly_MakesReducerFromGearsAndCases()
        {
            var sim = NewSim();
            sim.Money = 1000f;
            sim.TryUpgradeGenerator(0);
            sim.TryBuildAssembly(0);
            Freeze(sim.Lines[0].Buyer);
            Freeze(sim.Lines[0].Producer);
            Freeze(sim.Lines[0].Seller);

            sim.Lines[0].Products = 4;
            sim.Lines[1].Products = 2;
            int before = sim.TotalSold;

            for (int i = 0; i < 100; i++) sim.Tick(0.1f);

            Assert.GreaterOrEqual(sim.Assembly[0].Output + sim.TotalSold - before, 1);
            Assert.LessOrEqual(sim.Lines[0].Products, 2);
            Assert.LessOrEqual(sim.Lines[1].Products, 1);
        }

        [Test]
        public void Assembly_WaitsWithoutParts()
        {
            var sim = NewSim();
            sim.Money = 1000f;
            sim.TryUpgradeGenerator(0);
            sim.TryBuildAssembly(0);
            Freeze(sim.Lines[0].Producer);
            Freeze(sim.Lines[0].Seller);

            sim.Tick(3f);

            Assert.AreEqual(0, sim.Assembly[0].Output);
            Assert.AreEqual("ЖДЁТ ДЕТАЛИ", sim.Assembly[0].Status);
        }

        // Самая важная проверка сборки: редуктор должен уйти на привод,
        // а не в магазин. Именно на этом ломался старый вариант с одной сборкой.
        [Test]
        public void Assembly_ChainFeedsDriveFromReducers()
        {
            var sim = NewSim();
            sim.Money = 5000f;
            sim.TryUpgradeGenerator(0);
            sim.TryUpgradeGenerator(0);
            sim.TryUpgradeGenerator(1);

            Assert.AreEqual(UpgradeStatus.Ok, sim.TryBuildAssembly(0));
            Assert.AreEqual(UpgradeStatus.Ok, sim.TryBuildAssembly(1));

            for (int l = 0; l < sim.Lines.Count; l++)
            {
                Freeze(sim.Lines[l].Buyer);
                Freeze(sim.Lines[l].Producer);
                Freeze(sim.Lines[l].Seller);
            }

            sim.Lines[0].Products = 8;
            sim.Lines[1].Products = 4;
            sim.Lines[2].Products = 4;

            for (int i = 0; i < 300; i++) sim.Tick(0.1f);

            Assert.Greater(sim.Assembly[1].Produced, 0, "Drive line never got a reducer");
        }

        [Test]
        public void AssemblyPrice_IsHigherThanParts()
        {
            var sim = NewSim();
            int parts = ProductInfo.BasePrice(ProductKind.Gear) * 2 + ProductInfo.BasePrice(ProductKind.Case);

            Assert.Greater(sim.AssemblyPrice(0), parts);
        }

        [Test]
        public void AssemblyUpgrade_MakesItFasterAndPricier()
        {
            var sim = NewSim();
            sim.Money = 2000f;
            sim.TryUpgradeGenerator(0);
            sim.TryBuildAssembly(0);

            float time = sim.Assembly[0].CraftTime;
            int price = sim.AssemblyPrice(0);

            Assert.AreEqual(UpgradeStatus.Ok, sim.TryUpgradeAssembly(0));
            Assert.Less(sim.Assembly[0].CraftTime, time);
            Assert.Greater(sim.AssemblyPrice(0), price);
        }

        // Брак должен убираться кликом и давать маленькую награду.
        [Test]
        public void Defect_CanBeFixedForSmallReward()
        {
            var sim = NewSim();
            Assert.IsTrue(sim.SpawnDefect(0), "Defect should appear on a built line");

            float before = sim.Money;
            Assert.IsTrue(sim.FixDefect(0));

            Assert.IsFalse(sim.Lines[0].Defect);
            Assert.AreEqual(before + 5f, sim.Money, 0.01f);
            Assert.AreEqual(1, sim.DefectsCaught);
        }

        // Если брак не убрать — он съедает один готовый товар, без страйков.
        [Test]
        public void Defect_LeftAloneEatsOneProduct()
        {
            var sim = NewSim();
            sim.Lines[0].Products = 3;
            Assert.IsTrue(sim.SpawnDefect(0));

            sim.Lines[0].DefectLeft = 0f;
            sim.Tick(0.1f);

            Assert.IsFalse(sim.Lines[0].Defect);
            Assert.AreEqual(2, sim.Lines[0].Products);
            Assert.AreEqual(1, sim.DefectsMissed);
            Assert.AreEqual(0, sim.Strikes);
        }

        // Пауза замораживает смену полностью.
        [Test]
        public void Pause_StopsTheShift()
        {
            var sim = NewSim();
            sim.Paused = true;
            float before = sim.ShiftElapsed;

            sim.Tick(1f);

            Assert.AreEqual(before, sim.ShiftElapsed, 0.0001f);
        }
    }
}
