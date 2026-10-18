using Hotline.Economy;
using Hotline.UI.Toolkit;
using UnityEngine.UIElements;

namespace Hotline.UI
{
    /// <summary>Read-only view of heat, what's in the crypto wallet, and coin prices.</summary>
    internal static class HeatPage
    {
        public static void Build(VisualElement page)
        {
            var bank = CryptoBank.Instance;
            if (bank == null) { W.Label(page, "Heat tracking isn't running."); return; }
            var heat = bank.Heat;

            var box = W.Box(page);
            W.Heading(box, $"Heat: {heat.Value:0} / 100  ({heat.Tier})");
            W.Bar(box, (float)(heat.Value / 100));
            W.Label(box, heat.LastEvent);
            if (heat.Warned) W.Note(box, "You've been warned. Any day you stay above 50 can end in a raid.");

            W.Note(page, "How heat works: crypto scams add heat, more for richer targets. Regular scams add none. " +
                "Heat drops a little every day on its own. Above 50 you get one warning; after that, each day above 50 risks a raid that seizes your whole wallet.");

            W.Space(page);
            W.Label(page, $"Day {bank.Market.CurrentDay} · {bank.Market.LastHeadline}");
            var wallet = W.Box(page);
            foreach (var coin in Coin.All)
            {
                double held = bank.Wallet.AmountOf(coin.Ticker);
                double price = bank.Market.PriceOf(coin);
                W.Note(wallet, $"{coin.Name} ({coin.Ticker})  {Format.Money(price)}" +
                    (held > 0 ? $"   ·   holding {Format.Coins(held)} = {Format.Money(held * price)}" : ""));
            }
            W.FieldRow(wallet, "Wallet value", Format.Money(bank.Wallet.Value(bank.Market)));

            if (!string.IsNullOrEmpty(bank.LastRaidMessage)) W.Label(page, bank.LastRaidMessage);
            if (CryptoPayouts.RecentEvents.Count > 0)
            {
                W.Heading(page, "Recent");
                foreach (var line in CryptoPayouts.RecentEvents) W.Note(page, "  " + line);
            }
        }
    }
}
