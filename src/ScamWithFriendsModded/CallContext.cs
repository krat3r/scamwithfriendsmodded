using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace ScamWithFriendsModded
{
    /// <summary>
    /// Per-call state shared by the caller-prompt hook and the session hook. The game may
    /// build the caller's prompt before or after the scam session, so whichever runs first
    /// rolls the caller and the other reuses it.
    /// </summary>
    internal static class CallContext
    {
        // Two hooks for the same call fire well within this window; a new call won't.
        private const int SameCallWindowMs = 45000;
        // Chance a caller uses any given modded service (Glimmail, Zoinbase, ...).
        private const double AccountChance = 0.5;

        public static ConfigEntry<string> ForcedTier;
        public static ConfigEntry<bool> ScaleStockRewards;
        public static TargetTier Current { get; private set; }
        /// <summary>Ids of the scam packs whose service this caller uses; only those backstories reach the AI.</summary>
        public static readonly HashSet<string> Accounts = new HashSet<string>();
        /// <summary>Rough net worth shown in the caller dossier.</summary>
        public static double NetWorthEstimate { get; private set; }
        public static DateTime RolledAtUtc { get; private set; }
        private static int rolledAt;
        private static readonly Random rng = new Random();

        public static void Bind(ConfigFile config)
        {
            ForcedTier = config.Bind("Targets", "ForcedTier", "random",
                new ConfigDescription("Target preference: force every caller to one wealth tier, or 'random'.",
                    new AcceptableValueList<string>("random", "broke", "regular", "welloff", "whale")));
            ScaleStockRewards = config.Bind("Targets", "ScaleStockRewards", true,
                "Also scale the game's own scams (credit card, gift card) by the caller's wealth tier.");
        }

        public static bool ShouldScaleStock => ScaleStockRewards == null || ScaleStockRewards.Value;

        public static TargetTier EnsureTier()
        {
            int now = Environment.TickCount;
            if (Current == null || unchecked(now - rolledAt) > SameCallWindowMs) Roll();
            rolledAt = now;
            return Current;
        }

        private static void Roll()
        {
            var forced = ForcedTier?.Value;
            Current = string.IsNullOrEmpty(forced) || forced == "random" ? TargetTier.Roll(rng) : TargetTier.ById(forced);
            NetWorthEstimate = Current.NetWorthMin + rng.NextDouble() * (Current.NetWorthMax - Current.NetWorthMin);
            RolledAtUtc = DateTime.UtcNow;

            Accounts.Clear();
            var active = new List<ScamPack>(ScamRegistry.Active());
            foreach (var pack in active) if (rng.NextDouble() < AccountChance) Accounts.Add(pack.id);
            // Everyone uses at least one service, so every caller can be pitched something.
            if (Accounts.Count == 0 && active.Count > 0) Accounts.Add(active[rng.Next(active.Count)].id);

            if (Mod.LogGameEvents != null && Mod.LogGameEvents.Value)
                Mod.Log?.LogInfo($"New caller: {Current.Label} target, uses {string.Join(", ", Accounts)}.");
        }

        public static bool Uses(ScamPack pack) => Accounts.Contains(pack.id);

        /// <summary>Forgets the current caller so the next hook rolls a fresh one. For tests.</summary>
        public static void Reset() => Current = null;
    }
}
