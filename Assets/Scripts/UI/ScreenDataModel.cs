using System;

namespace Game.UI
{
    public enum MachineCardKind
    {
        Buyer,
        Producer,
        Seller,
        Generator1,
        Generator2,
        AssemblyUnit,
        BuildNewLine
    }

    public enum CardAccentTheme
    {
        Amber,
        Cyan,
        Green,
        Pink,
        Purple
    }

    [Serializable]
    public sealed class MachineCardData
    {
        public string Id;
        public MachineCardKind Kind;
        public string Title;
        public int Level;
        public string PrimarySpec;
        public string SecondarySpec;
        public int Cost;
        public bool IsAffordable;
        public bool IsLocked;
        public CardAccentTheme Theme;
    }

    [Serializable]
    public sealed class TopBarData
    {
        public int Balance;
        public int Day;
        public int PlanCurrent;
        public int PlanTotal;
        public float ShiftSecondsRemaining;
        public int EnergyUsed;
        public int EnergyMax;
        public int WarehouseMaterials;
        public int WarehouseGoods;
        public int WarehouseMax;
        public int FuelDailyCost;
    }
}
