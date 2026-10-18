using System;
using System.IO;
using BepInEx;
using Newtonsoft.Json;

namespace ScamWithFriendsModded.Economy
{
    /// <summary>
    /// Owns the market, wallet and heat, advances them each in-game day, and saves to
    /// disk so a lobby's crypto state survives a restart. Single instance via <see cref="Instance"/>.
    /// </summary>
    public sealed class CryptoBank
    {
        public static CryptoBank Instance { get; private set; }

        public readonly CryptoMarket Market;
        public readonly Wallet Wallet = new Wallet();
        public readonly Heat Heat = new Heat();
        public string LastRaidMessage;

        private readonly string savePath;
        private readonly Random rng = new Random();

        public event Action Changed;
        /// <summary>Raised after a raid with the value of the seized wallet.</summary>
        public event Action<double> Raided;

        private CryptoBank(int seed, string savePath)
        {
            Market = new CryptoMarket(seed);
            this.savePath = savePath;
            Market.Changed += Raise;
            Wallet.Changed += Raise;
            Heat.Changed += Raise;
        }

        /// <param name="folder">Where crypto-state.json lives; defaults to BepInEx/config/ScamWithFriendsModded.</param>
        /// <param name="seed">Market seed for a fresh save; 0 picks a random one.</param>
        public static void Init(string folder = null, int seed = 0)
        {
            string dir = folder ?? Path.Combine(Paths.ConfigPath, "ScamWithFriendsModded");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "crypto-state.json");
            Instance = new CryptoBank(seed == 0 ? Environment.TickCount : seed, path);
            Instance.Load();
        }

        private void Raise() => Changed?.Invoke();

        /// <summary>Advances market and heat to a new day. Safe to call repeatedly with the same day.</summary>
        public void OnDay(int day)
        {
            int previous = Market.CurrentDay;
            Market.AdvanceTo(day);
            if (day > previous)
            {
                if (Heat.RollDay(rng))
                {
                    double lost = Wallet.Value(Market);
                    Wallet.Clear();
                    Heat.Value = 25; // after a raid you're still on the radar, but it resets the clock
                    LastRaidMessage = $"Raided on day {day}. Everything in your wallet was seized ({Format.Money(lost)}).";
                    Plugin_Log($"Crypto: {LastRaidMessage}");
                    Raided?.Invoke(lost);
                }
                Save();
            }
        }

        private sealed class SaveData
        {
            public int Seed;
            public int Day;
            public System.Collections.Generic.Dictionary<string, double> Holdings;
            public double Heat;
            public bool Warned;
        }

        public void Save()
        {
            try
            {
                var data = new SaveData
                {
                    Seed = Market.Seed,
                    Day = Market.CurrentDay,
                    Holdings = Wallet.Holdings,
                    Heat = Heat.Value,
                    Warned = Heat.Warned,
                };
                File.WriteAllText(savePath, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception e) { Plugin_Log("Crypto: could not save state: " + e.Message); }
        }

        private void Load()
        {
            if (!File.Exists(savePath)) return;
            try
            {
                var data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(savePath));
                if (data == null) return;
                Market.Seed = data.Seed;
                foreach (var coin in Coin.All) Market.Prices[coin.Ticker] = coin.BasePrice;
                Market.CurrentDay = 0;
                Market.AdvanceTo(data.Day);
                if (data.Holdings != null) foreach (var pair in data.Holdings) Wallet.Holdings[pair.Key] = pair.Value;
                Heat.Value = data.Heat;
                Heat.Warned = data.Warned;
            }
            catch (Exception e) { Plugin_Log("Crypto: could not load state: " + e.Message); }
        }

        // Indirection so the Economy namespace doesn't hard-depend on the plugin's logger
        // (keeps it usable from tests where the logger may be null).
        private static void Plugin_Log(string message) => Mod.Log?.LogInfo(message);
    }
}
