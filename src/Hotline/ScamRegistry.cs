using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace Hotline
{
    /// <summary>Loaded scam packs, their on/off config, and their game-side definitions.</summary>
    internal static class ScamRegistry
    {
        public static readonly List<ScamPack> Packs = new List<ScamPack>();
        private static readonly Dictionary<string, ConfigEntry<bool>> enabled = new Dictionary<string, ConfigEntry<bool>>();
        private static ConfigFile config;

        public static string BundledFolder;
        public static string UserFolder => Path.Combine(Paths.ConfigPath, "Hotline", "ScamPacks");

        public static void Init(ConfigFile file, string pluginFolder)
        {
            config = file;
            BundledFolder = Path.Combine(pluginFolder, "ScamPacks");
            Directory.CreateDirectory(UserFolder);
            Reload();
        }

        /// <summary>Re-reads every pack. New calls and newly built computers pick up the change.</summary>
        public static void Reload()
        {
            Packs.Clear();
            Packs.AddRange(ScamPackLoader.Load(BundledFolder, UserFolder));
            foreach (var pack in Packs)
            {
                if (!enabled.ContainsKey(pack.id))
                    enabled[pack.id] = config.Bind("Scams", pack.id, true, $"Enable the '{pack.title}' scam.");
            }
            Mod.Log.LogInfo($"Loaded {Packs.Count} scam pack(s): {string.Join(", ", Packs.ConvertAll(p => p.id))}");
        }

        public static bool IsEnabled(ScamPack pack) => enabled.TryGetValue(pack.id, out var entry) && entry.Value;

        public static void SetEnabled(ScamPack pack, bool value)
        {
            if (enabled.TryGetValue(pack.id, out var entry)) entry.Value = value;
        }

        public static IEnumerable<ScamPack> Active()
        {
            foreach (var pack in Packs) if (IsEnabled(pack)) yield return pack;
        }

        public static ScamPack FindById(string id) => Packs.Find(p => p.id == id);

        /// <summary>
        /// Builds a ConversationScamDefinition for the game's call session. Crypto scams get
        /// a game reward of 0 because <see cref="CryptoPayouts"/> pays them in coins instead.
        /// </summary>
        public static object BuildDefinition(ScamPack pack, double rewardMultiplier = 1.0)
        {
            var definitionType = Native.Type("ConversationScamDefinition");
            var objectiveType = Native.Type("ConversationScamObjective");
            if (definitionType == null || objectiveType == null) return null;

            var definition = Native.New(definitionType);
            Native.Set(definition, "id", pack.id);
            Native.Set(definition, "appId", pack.AppId);
            Native.Set(definition, "productId", pack.ProductId);
            int reward = string.IsNullOrEmpty(pack.payoutCoin) ? (int)Math.Round(pack.reward * rewardMultiplier) : 0;
            Native.Set(definition, "reward", reward);
            // Not every build has display fields; set them if present.
            Native.Set(definition, "title", pack.title);
            Native.Set(definition, "description", pack.description);

            var objectives = new ArrayList();
            foreach (var source in pack.objectives)
            {
                var objective = Native.New(objectiveType);
                Native.Set(objective, "id", source.id);
                Native.Set(objective, "text", source.text);
                Native.Set(objective, "criteria", source.criteria);
                Native.Set(objective, "final", source.final);
                Native.Set(objective, "optional", source.optional);
                Native.Set(objective, "requiresSubmission", !string.IsNullOrEmpty(source.requiredSentinel));
                Native.Set(objective, "requiredSentinel", source.requiredSentinel);
                Native.Set(objective, "prerequisites", source.prerequisites ?? new string[0]);
                objectives.Add(objective);
            }
            Native.Set(definition, "objectives", Native.ToArray(objectiveType, objectives));
            return definition;
        }
    }
}
