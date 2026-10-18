using System;
using System.Collections.Generic;

namespace ScamWithFriendsModded.Economy
{
    /// <summary>
    /// Tracks a price per coin that drifts each in-game day, with occasional pump and
    /// crash events. Deterministic from a seed so the host and clients agree and a save
    /// restores the same prices.
    /// </summary>
    public sealed class CryptoMarket
    {
        public sealed class Day
        {
            public int Index;
            public Dictionary<string, double> Prices = new Dictionary<string, double>();
            public string Headline;
        }

        public int Seed;
        public int CurrentDay;
        public readonly Dictionary<string, double> Prices = new Dictionary<string, double>();
        public string LastHeadline = "Markets open.";

        public event Action Changed;

        public CryptoMarket(int seed = 0)
        {
            Seed = seed == 0 ? Environment.TickCount : seed;
            foreach (var coin in Coin.All) Prices[coin.Ticker] = coin.BasePrice;
        }

        public double PriceOf(string ticker) => Prices.TryGetValue(ticker, out var price) ? price : 0;
        public double PriceOf(Coin coin) => PriceOf(coin.Ticker);

        /// <summary>Advances the market to <paramref name="day"/>. Idempotent per day.</summary>
        public void AdvanceTo(int day)
        {
            if (day <= CurrentDay) return;
            for (int d = CurrentDay + 1; d <= day; d++) Step(d);
            CurrentDay = day;
            Changed?.Invoke();
        }

        private void Step(int day)
        {
            // One RNG per day+seed so a replay to the same day gives identical prices.
            var rng = new Random(Seed ^ (day * 2654435761u).GetHashCode());
            double eventRoll = rng.NextDouble();
            double eventFactor = 1.0;
            LastHeadline = "Quiet trading day.";
            if (eventRoll < 0.12) { eventFactor = 1.4 + rng.NextDouble() * 0.6; LastHeadline = "A coin is pumping hard today."; }
            else if (eventRoll < 0.24) { eventFactor = 0.4 + rng.NextDouble() * 0.3; LastHeadline = "The market is crashing."; }

            foreach (var coin in Coin.All)
            {
                double price = Prices[coin.Ticker];
                double drift = (rng.NextDouble() * 2 - 1) * coin.Volatility;
                if (!coin.Stable && eventFactor != 1.0) drift += (eventFactor - 1.0);
                price *= 1.0 + drift;
                if (coin.Stable) price = Lerp(price, coin.BasePrice, 0.8); // stablecoins snap back
                Prices[coin.Ticker] = Math.Max(coin.BasePrice * 0.05, price);
            }
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
