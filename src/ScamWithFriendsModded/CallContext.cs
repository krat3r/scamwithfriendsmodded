using System;
using BepInEx.Configuration;

namespace ScamWithFriendsModded
{
    /// <summary>
    /// Per-call state shared by the caller-prompt hook and the session hook. The game may
    /// build the caller's prompt before or after the scam session, so whichever runs first
    /// rolls the tier and the other reuses it.
    /// </summary>
    internal static class CallContext
    {
        // Two hooks for the same call fire well within this window; a new call won't.
        private const int SameCallWindowMs = 45000;

        public static ConfigEntry<string> ForcedTier;
        public static ConfigEntry<bool> ScaleStockRewards;
        public static TargetTier Current { get; private set; }
        private static int rolledAt;
        private static readonly Random rng = new Random();

        public static void Bind(ConfigFile config)
        {
            ForcedTier = config.Bind("Targets", "ForcedTier", "random",
                new ConfigDescription("Force every caller to one wealth tier, or 'random'.",
                    new AcceptableValueList<string>("random", "broke", "regular", "welloff", "whale")));
            ScaleStockRewards = config.Bind("Targets", "ScaleStockRewards", true,
                "Also scale the game's own scams (credit card, gift card) by the caller's wealth tier.");
        }

        public static bool ShouldScaleStock => ScaleStockRewards == null || ScaleStockRewards.Value;

        public static TargetTier EnsureTier()
        {
            int now = Environment.TickCount;
            if (Current == null || unchecked(now - rolledAt) > SameCallWindowMs)
            {
                var forced = ForcedTier?.Value;
                Current = string.IsNullOrEmpty(forced) || forced == "random" ? TargetTier.Roll(rng) : TargetTier.ById(forced);
                if (Mod.LogGameEvents != null && Mod.LogGameEvents.Value) Mod.Log?.LogInfo($"New caller: {Current.Label} target.");
            }
            rolledAt = now;
            return Current;
        }

        /// <summary>Forgets the current caller so the next hook rolls a fresh tier. For tests.</summary>
        public static void Reset() => Current = null;
    }
}
