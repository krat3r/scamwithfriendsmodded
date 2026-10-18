using BepInEx.Configuration;

namespace Hotline.Cheats
{
    /// <summary>
    /// Money and level cheats on top of the values the player pins in the Cheats tab.
    /// Also the bridge crypto cash-outs and raid fines use to touch game money.
    /// In multiplayer only the host's values are authoritative.
    /// </summary>
    internal static class GameCheats
    {
        private static ConfigEntry<string> moneyKey, levelKey;
        public static ConfigEntry<bool> InfiniteMoney;
        public static ConfigEntry<int> InfiniteMoneyAmount;
        public static ConfigEntry<bool> LockLevel;
        public static ConfigEntry<int> LockedLevel;

        public static ValueRef Money { get; private set; }
        public static ValueRef Level { get; private set; }

        public static void Bind(ConfigFile config)
        {
            moneyKey = config.Bind("Cheats", "MoneyMember", "", "Pinned money field (Full.Type::Member). Set from the Cheats tab.");
            levelKey = config.Bind("Cheats", "LevelMember", "", "Pinned level/XP field (Full.Type::Member). Set from the Cheats tab.");
            InfiniteMoney = config.Bind("Cheats", "InfiniteMoney", false, "Keep money topped up to InfiniteMoneyAmount.");
            InfiniteMoneyAmount = config.Bind("Cheats", "InfiniteMoneyAmount", 1000000, "Amount infinite money keeps you at.");
            LockLevel = config.Bind("Cheats", "LockLevel", false, "Hold the pinned level/XP field at LockedLevel.");
            LockedLevel = config.Bind("Cheats", "LockedLevel", 99, "Value the level lock holds.");
            Money = ValueFinder.Resolve(moneyKey.Value);
            Level = ValueFinder.Resolve(levelKey.Value);
        }

        public static void PinMoney(ValueRef value) { Money = value; moneyKey.Value = value?.Key ?? ""; }
        public static void PinLevel(ValueRef value) { Level = value; levelKey.Value = value?.Key ?? ""; }

        public static bool MoneyBound => Money != null && Money.Available;

        /// <summary>Adds (or with a negative amount, removes) game money. False if no money field is pinned.</summary>
        public static bool AddMoney(double amount)
        {
            if (!MoneyBound) return false;
            var current = Money.Read();
            if (current == null) return false;
            return Money.Write(System.Math.Max(0, current.Value + amount));
        }

        /// <summary>Called a few times a second from the plugin's Update.</summary>
        public static void Tick()
        {
            if (InfiniteMoney.Value && MoneyBound)
            {
                var current = Money.Read();
                if (current != null && current.Value < InfiniteMoneyAmount.Value) Money.Write(InfiniteMoneyAmount.Value);
            }
            if (LockLevel.Value && Level != null && Level.Available)
            {
                var current = Level.Read();
                if (current != null && current.Value != LockedLevel.Value) Level.Write(LockedLevel.Value);
            }
        }
    }
}
