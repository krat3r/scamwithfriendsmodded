using System;
using System.Collections.Generic;

namespace Hotline.Economy
{
    /// <summary>Crypto earned from scams. Everything in it is lost if you get raided.</summary>
    public sealed class Wallet
    {
        public readonly Dictionary<string, double> Holdings = new Dictionary<string, double>();

        public event Action Changed;

        public double AmountOf(string ticker) => Holdings.TryGetValue(ticker, out var amount) ? amount : 0;

        public void Credit(string ticker, double amount)
        {
            if (amount <= 0) return;
            Holdings[ticker] = AmountOf(ticker) + amount;
            Changed?.Invoke();
        }

        public double Value(CryptoMarket market)
        {
            double total = 0;
            foreach (var pair in Holdings) total += pair.Value * market.PriceOf(pair.Key);
            return total;
        }

        public void Clear()
        {
            Holdings.Clear();
            Changed?.Invoke();
        }
    }

    internal static class Format
    {
        public static string Money(double value) => "$" + value.ToString("N0");
        public static string Coins(double value) => value.ToString("0.####");
    }
}
