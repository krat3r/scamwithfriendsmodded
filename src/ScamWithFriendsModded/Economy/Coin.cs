namespace ScamWithFriendsModded.Economy
{
    /// <summary>A made-up cryptocurrency. All tickers are fictional stand-ins.</summary>
    public sealed class Coin
    {
        public readonly string Ticker;
        public readonly string Name;
        /// <summary>Starting price in game currency per 1 coin.</summary>
        public readonly double BasePrice;
        /// <summary>Daily volatility, 0..1. Stablecoins are near 0; meme coins are high.</summary>
        public readonly double Volatility;
        public readonly bool Stable;

        public Coin(string ticker, string name, double basePrice, double volatility, bool stable = false)
        {
            Ticker = ticker;
            Name = name;
            BasePrice = basePrice;
            Volatility = volatility;
            Stable = stable;
        }

        // Fictional: Bitcorn echoes Bitcoin, Ethos echoes Ether, Dohdge a meme coin, Stabl a stablecoin.
        public static readonly Coin Bitcorn = new Coin("BTCO", "Bitcorn", 45000, 0.06);
        public static readonly Coin Ethos = new Coin("ETHO", "Ethos", 2500, 0.08);
        public static readonly Coin Dohdge = new Coin("DOHG", "Dohdge", 0.12, 0.22);
        public static readonly Coin Stabl = new Coin("USDS", "Stabl", 1.0, 0.01, stable: true);

        public static readonly Coin[] All = { Bitcorn, Ethos, Dohdge, Stabl };

        public static Coin ByTicker(string ticker)
        {
            foreach (var coin in All) if (coin.Ticker == ticker) return coin;
            return null;
        }
    }
}
