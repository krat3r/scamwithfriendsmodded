using Hotline.Economy;
using UnityEngine;

namespace Hotline.UI
{
    /// <summary>Read-only view of heat, what's in the crypto wallet, and coin prices.</summary>
    internal static class HeatPage
    {
        public static void Draw()
        {
            var bank = CryptoBank.Instance;
            if (bank == null) { GUILayout.Label("Heat tracking isn't running."); return; }
            var heat = bank.Heat;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Heat: {heat.Value:0} / 100  ({heat.Tier})");
            DrawBar((float)(heat.Value / 100));
            GUILayout.Label(heat.LastEvent);
            if (heat.Warned) GUILayout.Label("You've been warned. Any day you stay above 50 can end in a raid.");
            GUILayout.EndVertical();

            GUILayout.Label("How heat works: crypto scams add heat, more for richer targets. Regular scams add none. " +
                "Heat drops a little every day on its own. Above 50 you get one warning; after that, each day above 50 risks a raid that seizes your whole wallet.");

            GUILayout.Space(8);
            GUILayout.Label($"Day {bank.Market.CurrentDay} · {bank.Market.LastHeadline}");
            GUILayout.BeginVertical(GUI.skin.box);
            foreach (var coin in Coin.All)
            {
                double held = bank.Wallet.AmountOf(coin.Ticker);
                double price = bank.Market.PriceOf(coin);
                GUILayout.Label($"{coin.Name} ({coin.Ticker})  {Format.Money(price)}" +
                    (held > 0 ? $"   ·   holding {Format.Coins(held)} = {Format.Money(held * price)}" : ""));
            }
            GUILayout.Label($"Wallet value: {Format.Money(bank.Wallet.Value(bank.Market))}");
            GUILayout.EndVertical();

            if (!string.IsNullOrEmpty(bank.LastRaidMessage)) GUILayout.Label(bank.LastRaidMessage);
            if (CryptoPayouts.RecentEvents.Count > 0)
            {
                GUILayout.Label("Recent:");
                foreach (var line in CryptoPayouts.RecentEvents) GUILayout.Label("  " + line);
            }
        }

        private static void DrawBar(float fraction)
        {
            var rect = GUILayoutUtility.GetRect(100, 14, GUILayout.ExpandWidth(true));
            var old = GUI.color;
            GUI.color = new Color(0.25f, 0.25f, 0.25f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = fraction < 0.2f ? new Color(0.3f, 0.75f, 0.35f)
                : fraction < 0.5f ? new Color(0.95f, 0.75f, 0.2f)
                : new Color(0.85f, 0.2f, 0.2f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
