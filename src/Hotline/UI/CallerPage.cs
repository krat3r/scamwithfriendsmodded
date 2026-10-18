using System;
using Hotline.Economy;
using Hotline.UI.Toolkit;
using UnityEngine.UIElements;

namespace Hotline.UI
{
    /// <summary>Dossier on the current (or last) caller, and the target preference setting.</summary>
    internal static class CallerPage
    {
        private static readonly string[] TierIds = { "random", "broke", "regular", "welloff", "whale" };
        private static readonly string[] TierLabels = { "Any", "Broke", "Regular", "Well-off", "Whale" };

        public static void Build(VisualElement page)
        {
            var tier = CallContext.Current;
            var box = W.Box(page);
            if (tier == null)
            {
                W.Label(box, "No caller yet. Take a call and the dossier fills in.");
            }
            else
            {
                var age = DateTime.UtcNow - CallContext.RolledAtUtc;
                W.Heading(box, $"Caller dossier ({(age.TotalMinutes < 1 ? "current call" : $"{age.TotalMinutes:0} min ago")})");
                W.FieldRow(box, "Wealth", tier.Label);
                W.FieldRow(box, "Estimated net worth", "~" + Format.Money(Round(CallContext.NetWorthEstimate)));
                W.FieldRow(box, "Wariness", tier.Wariness);
                W.FieldRow(box, "Rewards", "x" + tier.RewardMultiplier);
                W.Label(box, "Uses:");
                bool any = false;
                foreach (var pack in ScamRegistry.Active())
                {
                    if (!CallContext.Uses(pack)) continue;
                    any = true;
                    W.Note(box, $"  • {pack.brand ?? pack.title}  →  {pack.title}{(string.IsNullOrEmpty(pack.payoutCoin) ? "" : " (crypto, adds heat)")}");
                }
                if (!any) W.Note(box, "  None of the modded services. Try a stock scam.");
            }

            W.Space(page);
            W.Label(page, "Target preference (applies from the next caller):");
            int current = Array.IndexOf(TierIds, CallContext.ForcedTier.Value);
            W.Segmented(page, TierLabels, current < 0 ? 0 : current, picked =>
            {
                CallContext.ForcedTier.Value = TierIds[picked];
                ModMenu.Refresh();
            });
            W.Note(page, "Richer targets pay more but are harder to convince, and crypto scams on them add more heat.");
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
