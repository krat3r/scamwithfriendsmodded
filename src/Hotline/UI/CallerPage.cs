using System;
using Hotline.Economy;
using UnityEngine;

namespace Hotline.UI
{
    /// <summary>Dossier on the current (or last) caller, and the target preference setting.</summary>
    internal static class CallerPage
    {
        private static readonly string[] TierIds = { "random", "broke", "regular", "welloff", "whale" };
        private static readonly string[] TierLabels = { "Any", "Broke", "Regular", "Well-off", "Whale" };

        public static void Draw()
        {
            var tier = CallContext.Current;
            GUILayout.BeginVertical(GUI.skin.box);
            if (tier == null)
            {
                GUILayout.Label("No caller yet. Take a call and the dossier fills in.");
            }
            else
            {
                var age = DateTime.UtcNow - CallContext.RolledAtUtc;
                GUILayout.Label($"Caller dossier ({(age.TotalMinutes < 1 ? "current call" : $"{age.TotalMinutes:0} min ago")})");
                GUILayout.Label($"Wealth: {tier.Label}   ·   Estimated net worth: ~{Format.Money(Round(CallContext.NetWorthEstimate))}");
                GUILayout.Label($"Wariness: {tier.Wariness}   ·   Rewards x{tier.RewardMultiplier}");
                GUILayout.Label("Uses:");
                bool any = false;
                foreach (var pack in ScamRegistry.Active())
                {
                    if (!CallContext.Uses(pack)) continue;
                    any = true;
                    GUILayout.Label($"  • {pack.brand ?? pack.title}  →  {pack.title}{(string.IsNullOrEmpty(pack.payoutCoin) ? "" : " (crypto, adds heat)")}");
                }
                if (!any) GUILayout.Label("  None of the modded services. Try a stock scam.");
            }
            GUILayout.EndVertical();

            GUILayout.Space(8);
            GUILayout.Label("Target preference (applies from the next caller):");
            int current = Array.IndexOf(TierIds, CallContext.ForcedTier.Value);
            int picked = GUILayout.Toolbar(current < 0 ? 0 : current, TierLabels);
            if (picked != current) CallContext.ForcedTier.Value = TierIds[picked];
            GUILayout.Label("Richer targets pay more but are harder to convince, and crypto scams on them add more heat.");
        }

        // Estimates read better rounded to two significant figures.
        private static double Round(double value)
        {
            if (value <= 0) return 0;
            double scale = Math.Pow(10, Math.Floor(Math.Log10(value)) - 1);
            return Math.Round(value / scale) * scale;
        }
    }
}
