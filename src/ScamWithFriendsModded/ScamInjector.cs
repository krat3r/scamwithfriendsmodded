using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ScamWithFriendsModded
{
    /// <summary>
    /// Adds scam packs to every call session and teaches AI callers the matching
    /// personas. Runs wherever calls are simulated (the host in multiplayer).
    /// </summary>
    internal static class ScamInjector
    {
        public static int SessionsStarted;
        public static int ScamsInjected;
        public static readonly List<string> InstalledHooks = new List<string>();
        /// <summary>The game's own catalog from the most recent call, for diagnostics.</summary>
        public static IList LastNativeCatalog;

        private static Type definitionType;

        public static void Install(Harmony harmony)
        {
            definitionType = GameProbe.Get("ConversationScamDefinition");
            var session = GameProbe.Get("ConversationScamSession");
            if (definitionType == null || session == null)
            {
                Mod.Log.LogError("Scam injection disabled: call session types not found.");
                return;
            }

            var arrayType = definitionType.MakeArrayType();
            foreach (var ctor in session.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length > 0 && parameters[0].ParameterType.IsAssignableFrom(arrayType))
                    Patch(harmony, ctor, prefix: nameof(SessionPrefix), postfix: nameof(SessionPostfix));
            }

            var prompt = AccessTools.Method(GameProbe.Get("CallerIdentityLibrary"), "BuildCallerSystemPrompt");
            if (prompt != null) Patch(harmony, prompt, postfix: nameof(CallerPromptPostfix));
            else Mod.Log.LogWarning("Caller personas disabled: CallerIdentityLibrary.BuildCallerSystemPrompt not found.");
        }

        private static void Patch(Harmony harmony, MethodBase target, string prefix = null, string postfix = null)
        {
            try
            {
                harmony.Patch(target,
                    prefix: prefix == null ? null : new HarmonyMethod(typeof(ScamInjector), prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(typeof(ScamInjector), postfix));
                InstalledHooks.Add($"{target.DeclaringType?.Name}.{target.Name}");
            }
            catch (Exception e)
            {
                Mod.Log.LogError($"Failed to hook {target.DeclaringType?.Name}.{target.Name}: {e}");
            }
        }

        // Harmony writes changes to __args back into the real arguments.
        private static void SessionPrefix(object[] __args)
        {
            SessionsStarted++;
            if (!(__args[0] is IEnumerable catalog)) return;
            var tier = CallContext.EnsureTier();

            var merged = new ArrayList();
            var ids = new HashSet<string>();
            bool changed = false;
            foreach (var definition in catalog)
            {
                var id = Native.GetString(definition, "id");
                if (id != null) ids.Add(id);
                bool ours = id != null && ScamRegistry.FindById(id) != null;
                if (!ours && CallContext.ShouldScaleStock && tier.RewardMultiplier != 1.0)
                {
                    merged.Add(ScaledCopy(definition, tier.RewardMultiplier));
                    changed = true;
                }
                else merged.Add(definition);
            }
            LastNativeCatalog = new ArrayList(merged);

            int added = 0;
            foreach (var pack in ScamRegistry.Active())
            {
                if (ids.Contains(pack.id)) continue;
                try
                {
                    var definition = ScamRegistry.BuildDefinition(pack, tier.RewardMultiplier);
                    if (definition == null) continue;
                    merged.Add(definition);
                    added++;
                }
                catch (Exception e)
                {
                    Mod.Log.LogError($"Could not build scam '{pack.id}': {e}");
                }
            }
            if (added == 0 && !changed) return;

            __args[0] = Native.ToArray(definitionType, merged);
            ScamsInjected += added;
            if (Mod.LogGameEvents.Value)
                Mod.Log.LogInfo($"Call started: {tier.Label} target, rewards x{tier.RewardMultiplier}, {added} modded scam(s) available.");
        }

        private static void SessionPostfix(object __instance) => CryptoPayouts.Track(__instance, CallContext.Current);

        /// <summary>A per-call copy of a stock scam with its rewards scaled; the shared original is untouched.</summary>
        private static object ScaledCopy(object definition, double multiplier)
        {
            var copy = Native.Clone(definition);
            if (Native.Get(definition, "reward") is int reward)
                Native.Set(copy, "reward", (int)Math.Round(reward * multiplier));
            if (Native.Get(definition, "rewardTiers") is int[] tiers)
            {
                var scaled = new int[tiers.Length];
                for (int i = 0; i < tiers.Length; i++) scaled[i] = (int)Math.Round(tiers[i] * multiplier);
                Native.Set(copy, "rewardTiers", scaled);
            }
            return copy;
        }

        // The prompt may be built from the game's original catalog, before our session
        // prefix runs, so personas come from the caller's rolled accounts, not the catalog.
        private static void CallerPromptPostfix(ref string __result)
        {
            if (string.IsNullOrEmpty(__result)) return;
            var lines = new List<string> { CallContext.EnsureTier().PersonaHint };
            foreach (var pack in ScamRegistry.Active())
                if (CallContext.Uses(pack) && !string.IsNullOrWhiteSpace(pack.callerPersona)) lines.Add(pack.callerPersona.Trim());
            __result += "\n" + string.Join("\n", lines);
        }
    }
}
