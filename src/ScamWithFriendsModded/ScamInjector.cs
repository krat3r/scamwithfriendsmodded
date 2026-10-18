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
                    Patch(harmony, ctor, prefix: nameof(SessionPrefix));
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

            var merged = new ArrayList();
            var ids = new HashSet<string>();
            foreach (var definition in catalog)
            {
                merged.Add(definition);
                var id = Native.GetString(definition, "id");
                if (id != null) ids.Add(id);
            }
            LastNativeCatalog = new ArrayList(merged);

            int added = 0;
            foreach (var pack in ScamRegistry.Active())
            {
                if (ids.Contains(pack.id)) continue;
                try
                {
                    var definition = ScamRegistry.BuildDefinition(pack);
                    if (definition == null) continue;
                    merged.Add(definition);
                    added++;
                }
                catch (Exception e)
                {
                    Mod.Log.LogError($"Could not build scam '{pack.id}': {e}");
                }
            }
            if (added == 0) return;

            __args[0] = Native.ToArray(definitionType, merged);
            ScamsInjected += added;
            if (Mod.LogGameEvents.Value) Mod.Log.LogInfo($"Call started with {added} modded scam(s) available.");
        }

        // The prompt may be built from the game's original catalog, before our session
        // prefix runs, so personas for every active pack are added unconditionally.
        private static void CallerPromptPostfix(ref string __result)
        {
            if (string.IsNullOrEmpty(__result)) return;
            var personas = new List<string>();
            foreach (var pack in ScamRegistry.Active())
                if (!string.IsNullOrWhiteSpace(pack.callerPersona)) personas.Add(pack.callerPersona.Trim());
            if (personas.Count > 0) __result += "\n" + string.Join("\n", personas);
        }
    }
}
