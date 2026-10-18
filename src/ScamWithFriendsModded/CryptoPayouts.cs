using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ScamWithFriendsModded.Economy;

namespace ScamWithFriendsModded
{
    /// <summary>
    /// Watches live call sessions and pays crypto scams into the wallet when their
    /// final objective completes. The game pays these scams 0 money; this is their only
    /// payout. Each payout also adds heat, more for richer targets.
    /// </summary>
    internal static class CryptoPayouts
    {
        private const int MaxTracked = 16;
        private const int MaxEvents = 20;

        private sealed class Tracked
        {
            public WeakReference Session;
            public TargetTier Tier;
            public readonly HashSet<string> Settled = new HashSet<string>();
        }

        private static readonly List<Tracked> tracked = new List<Tracked>();

        /// <summary>Newest first. Shown on the Wallet page.</summary>
        public static readonly List<string> RecentEvents = new List<string>();
        public static int PayoutsMade;

        public static void Track(object session, TargetTier tier)
        {
            if (session == null) return;
            var entry = new Tracked { Session = new WeakReference(session), Tier = tier ?? TargetTier.Regular };
            tracked.Add(entry);
            if (tracked.Count > MaxTracked) tracked.RemoveAt(0);

            // React immediately when the session reports progress; Poll() is the fallback.
            var changed = session.GetType().GetEvent("Changed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (changed != null && changed.EventHandlerType == typeof(Action))
            {
                try { changed.AddEventHandler(session, (Action)(() => Check(entry))); }
                catch (Exception e) { Mod.Log?.LogWarning("Could not subscribe to session changes: " + e.Message); }
            }
        }

        /// <summary>Called from the plugin's Update about once a second.</summary>
        public static void Poll()
        {
            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                var entry = tracked[i];
                if (!entry.Session.IsAlive) { tracked.RemoveAt(i); continue; }
                Check(entry);
                if (Native.Get(entry.Session.Target, "disposed") is bool disposed && disposed) tracked.RemoveAt(i);
            }
        }

        private static void Check(Tracked entry)
        {
            var session = entry.Session.Target;
            var bank = CryptoBank.Instance;
            if (session == null || bank == null) return;
            try
            {
                var completedScams = CompletedScams(session);
                foreach (var scamId in completedScams)
                {
                    if (entry.Settled.Contains(scamId)) continue;
                    var pack = ScamRegistry.FindById(scamId);
                    if (pack == null || string.IsNullOrEmpty(pack.payoutCoin)) { entry.Settled.Add(scamId); continue; }
                    entry.Settled.Add(scamId);
                    Settle(pack, entry.Tier, completedScams, bank);
                }
            }
            catch (Exception e)
            {
                Mod.Log?.LogError("Crypto payout check failed: " + e);
            }
        }

        private static void Settle(ScamPack pack, TargetTier tier, HashSet<string> completedScams, CryptoBank bank)
        {
            if (pack.requiresAnyOf != null && pack.requiresAnyOf.Length > 0)
            {
                bool met = false;
                foreach (var required in pack.requiresAnyOf) if (completedScams.Contains(required)) { met = true; break; }
                if (!met)
                {
                    AddEvent($"{pack.title}: no payout. You needed access first ({string.Join(" or ", pack.requiresAnyOf)}).");
                    return;
                }
            }

            double value = pack.reward * tier.RewardMultiplier;
            double price = bank.Market.PriceOf(pack.payoutCoin);
            if (price <= 0) return;
            double coins = value / price;
            bank.Wallet.Credit(pack.payoutCoin, coins);
            bank.Heat.Add(HeatFor(tier), $"{pack.title} on a {tier.Label.ToLowerInvariant()} target drew attention.");
            bank.Save();
            PayoutsMade++;
            AddEvent($"{pack.title} ({tier.Label}): +{Format.Coins(coins)} {pack.payoutCoin} (~{Format.Money(value)}).");
        }

        private static double HeatFor(TargetTier tier) =>
            tier == TargetTier.Whale ? 8 : tier == TargetTier.WellOff ? 4 : tier == TargetTier.Broke ? 1 : 2;

        /// <summary>Scam ids in this session whose final objective is complete.</summary>
        private static HashSet<string> CompletedScams(object session)
        {
            var result = new HashSet<string>();
            if (!(Native.Get(session, "progress") is IDictionary progress)) return result;
            if (!(Native.Get(session, "catalog") is IEnumerable catalog)) return result;

            foreach (var definition in catalog)
            {
                var id = Native.GetString(definition, "id");
                if (id == null || !progress.Contains(id)) continue;
                if (!(Native.Get(progress[id], "Completed") is IEnumerable completed)) continue;

                var done = new HashSet<string>();
                foreach (var objectiveId in completed) if (objectiveId is string s) done.Add(s);
                if (!(Native.Get(definition, "objectives") is IEnumerable objectives)) continue;
                foreach (var objective in objectives)
                {
                    if (Native.Get(objective, "final") is bool final && final && done.Contains(Native.GetString(objective, "id")))
                    {
                        result.Add(id);
                        break;
                    }
                }
            }
            return result;
        }

        public static void AddEvent(string message)
        {
            RecentEvents.Insert(0, message);
            if (RecentEvents.Count > MaxEvents) RecentEvents.RemoveAt(RecentEvents.Count - 1);
            Mod.Log?.LogInfo("Crypto: " + message);
        }

        public static void ResetForTests()
        {
            tracked.Clear();
            RecentEvents.Clear();
            PayoutsMade = 0;
        }
    }
}
