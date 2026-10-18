using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ScamWithFriendsModded
{
    /// <summary>
    /// Looks up game types by name so the mod survives game updates: if a type is
    /// renamed, we log it and skip the related feature instead of crashing on load.
    /// </summary>
    internal static class GameProbe
    {
        // Types seen in the current Early Access build (Assembly-CSharp).
        // Run tools/decompile to browse the full list.
        private static readonly string[] KnownTypes =
        {
            "ConversationScamSession",
            "ConversationScamAiDetector",
            "ConversationScamDefinition",
            "ConversationScamObjective",
            "CallerIdentityLibrary",
            "KolkataOsController",
            "KolkataOsApplicationAccess",
            "KolkataOsApplicationStoreCatalog",
            "KolkataOsApplicationStoreManifest",
        };

        public static readonly Dictionary<string, Type> Found = new Dictionary<string, Type>();
        public static readonly List<string> Missing = new List<string>();

        public static void Scan()
        {
            Found.Clear();
            Missing.Clear();
            foreach (var name in KnownTypes)
            {
                var type = AccessTools.TypeByName(name);
                if (type != null) Found[name] = type;
                else Missing.Add(name);
            }

            Mod.Log.LogInfo($"Game probe: {Found.Count}/{KnownTypes.Length} known types found.");
            foreach (var name in Missing)
                Mod.Log.LogWarning($"Game probe: type '{name}' not found; the game may have been updated.");
        }

        public static Type Get(string name) => Found.TryGetValue(name, out var type) ? type : null;
    }
}
