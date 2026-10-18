using System;

namespace ScamWithFriendsModded.Economy
{
    /// <summary>
    /// "Heat" rises with every crypto scam, more for richer targets, and only drops slowly
    /// on its own. Past a threshold there's a daily chance of getting "fedded": a raid that
    /// seizes everything in the wallet. There's no way to buy or hide your way out of it.
    /// </summary>
    public sealed class Heat
    {
        public double Value;      // 0..100
        public bool RaidPending;
        public string LastEvent = "Clean record.";

        public event Action Changed;

        public void Add(double amount, string reason)
        {
            if (amount <= 0) return;
            Value = Math.Min(100, Value + amount);
            LastEvent = reason;
            Changed?.Invoke();
        }

        public void Reduce(double amount)
        {
            Value = Math.Max(0, Value - amount);
            Changed?.Invoke();
        }

        /// <summary>Call once per in-game day. Decays heat and rolls for a raid.</summary>
        public bool RollDay(Random rng)
        {
            Reduce(8 + rng.NextDouble() * 6);
            double raidChance = Value <= 50 ? 0 : (Value - 50) / 100.0; // up to 50% at max heat
            bool raided = rng.NextDouble() < raidChance;
            RaidPending = raided;
            if (raided) LastEvent = "The Cyber Crimes Unit raided you. Your wallet was seized.";
            Changed?.Invoke();
            return raided;
        }

        public string Tier =>
            Value < 20 ? "Low" : Value < 50 ? "Watched" : Value < 80 ? "Wanted" : "Most Wanted";
    }
}
