using NUnit.Framework;
using Game.Factory;

namespace Game.Tests
{
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
    }
}
