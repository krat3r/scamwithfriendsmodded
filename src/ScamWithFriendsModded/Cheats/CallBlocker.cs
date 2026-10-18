using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using HarmonyLib;

namespace ScamWithFriendsModded.Cheats
{
    /// <summary>
    /// Lets the player turn off automatic incoming calls. We don't know which game method
    /// rings the phone yet, so this lists likely candidates and the player picks which to
    /// block. Blocked methods are skipped while "Automatic calls" is off.
    /// </summary>
    internal static class CallBlocker
    {
        private static readonly Regex Likely = new Regex(
            "(incoming|ring|spawn|schedule|queue|next|start|begin|trigger|dial).*call|call.*(incoming|spawn|schedule|queue|timer|ring)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static ConfigEntry<bool> AutomaticCalls;
        private static ConfigEntry<string> blockedKeys;
        private static Harmony harmony;
        private static readonly Dictionary<string, MethodInfo> blocked = new Dictionary<string, MethodInfo>();
        private static readonly MethodInfo prefix = AccessTools.Method(typeof(CallBlocker), nameof(SkipWhileDisabled));

        public static IReadOnlyCollection<string> BlockedKeys => blocked.Keys;

        public static void Install(ConfigFile config, Harmony patcher)
        {
            harmony = patcher;
            AutomaticCalls = config.Bind("Calls", "AutomaticCalls", true,
                "When off, the methods picked in the Cheats tab are skipped so callers stop ringing on their own.");
            blockedKeys = config.Bind("Calls", "BlockedMethods", "",
                "Methods skipped while AutomaticCalls is off (Full.Type::Method, separated by ';').");
            foreach (var key in blockedKeys.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var method = Resolve(key.Trim());
                if (method != null) Block(method, save: false);
                else Mod.Log.LogWarning($"Call blocker: '{key}' no longer exists in the game.");
            }
        }

        public static List<MethodInfo> Candidates()
        {
            var results = new List<MethodInfo>();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var type in ValueFinder.GameTypes())
            {
                foreach (var method in type.GetMethods(flags))
                {
                    // Only void methods: skipping them leaves no bogus return value behind.
                    if (method.ReturnType != typeof(void) || method.IsAbstract || method.IsGenericMethodDefinition || method.IsSpecialName) continue;
                    if (method.Name.Contains("<") || !Likely.IsMatch(method.Name)) continue;
                    results.Add(method);
                }
            }
            return results;
        }

        public static string KeyOf(MethodInfo method) => method.DeclaringType?.FullName + "::" + method.Name;
        public static bool IsBlocked(MethodInfo method) => blocked.ContainsKey(KeyOf(method));

        public static void Block(MethodInfo method, bool save = true)
        {
            var key = KeyOf(method);
            if (blocked.ContainsKey(key)) return;
            try
            {
                harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                blocked[key] = method;
                if (save) Save();
            }
            catch (Exception e) { Mod.Log.LogError($"Call blocker: could not hook {key}: {e.Message}"); }
        }

        public static void Unblock(MethodInfo method)
        {
            var key = KeyOf(method);
            if (!blocked.ContainsKey(key)) return;
            harmony.Unpatch(method, prefix);
            blocked.Remove(key);
            Save();
        }

        private static void Save() => blockedKeys.Value = string.Join(";", blocked.Keys);

        private static MethodInfo Resolve(string key)
        {
            int split = key.LastIndexOf("::", StringComparison.Ordinal);
            if (split < 0) return null;
            var type = AccessTools.TypeByName(key.Substring(0, split));
            if (type == null) return null;
            string name = key.Substring(split + 2);
            // Overloads share a key; block the first void overload, same as the candidate list shows.
            foreach (var method in AccessTools.GetDeclaredMethods(type))
                if (method.Name == name && method.ReturnType == typeof(void)) return method;
            return null;
        }

        // Returning false skips the original method.
        private static bool SkipWhileDisabled() => AutomaticCalls == null || AutomaticCalls.Value;
    }
}
