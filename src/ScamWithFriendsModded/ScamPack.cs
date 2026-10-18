using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ScamWithFriendsModded
{
    /// <summary>
    /// A scam type loaded from ScamPacks/*.json. Field names follow the game's own
    /// ConversationScamDefinition / ConversationScamObjective where they overlap.
    /// </summary>
    public sealed class ScamPack
    {
        public string id;
        public string brand;
        public string title;
        public string description;
        public int reward = 200;
        public ShopListing shop = new ShopListing();
        /// <summary>Appended to the AI caller's system prompt so callers can fall for this scam.</summary>
        public string callerPersona;
        public List<Objective> objectives = new List<Objective>();

        [JsonIgnore] public string SourceFile;
        [JsonIgnore] public string AppId => id;
        [JsonIgnore] public string ProductId => "scam-" + id;

        public sealed class ShopListing
        {
            public int price = 1000;
            public int unlockDay = 1;
            public string iconText;
            public string accentColor = "#4A90E2";
        }

        public sealed class Objective
        {
            public string id;
            public string text;
            public string criteria;
            public bool final;
            public bool optional;
            public string[] prerequisites;
            /// <summary>Advanced: a game sentinel tag the player must submit in the app.</summary>
            public string requiredSentinel;
        }

        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(id)) return "missing \"id\"";
            if (string.IsNullOrWhiteSpace(title)) return "missing \"title\"";
            if (objectives == null || objectives.Count == 0) return "needs at least one objective";
            if (!objectives.Exists(o => o.final)) return "needs one objective with \"final\": true";
            var ids = new HashSet<string>();
            foreach (var objective in objectives)
            {
                if (string.IsNullOrWhiteSpace(objective.id)) return "objective missing \"id\"";
                if (!ids.Add(objective.id)) return $"duplicate objective id '{objective.id}'";
            }
            foreach (var objective in objectives)
                if (objective.prerequisites != null)
                    foreach (var prerequisite in objective.prerequisites)
                        if (!ids.Contains(prerequisite)) return $"objective '{objective.id}' needs unknown objective '{prerequisite}'";
            return null;
        }
    }

    internal static class ScamPackLoader
    {
        /// <summary>Loads packs from each folder in order; later folders override earlier ones by id.</summary>
        public static List<ScamPack> Load(params string[] folders)
        {
            var byId = new Dictionary<string, ScamPack>();
            var order = new List<string>();
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;
                var files = Directory.GetFiles(folder, "*.json");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    ScamPack pack;
                    try
                    {
                        pack = JsonConvert.DeserializeObject<ScamPack>(File.ReadAllText(file));
                    }
                    catch (Exception e)
                    {
                        Mod.Log.LogError($"Scam pack {Path.GetFileName(file)}: invalid JSON: {e.Message}");
                        continue;
                    }
                    var error = pack == null ? "empty file" : pack.Validate();
                    if (error != null)
                    {
                        Mod.Log.LogError($"Scam pack {Path.GetFileName(file)}: {error}");
                        continue;
                    }
                    pack.SourceFile = file;
                    if (!byId.ContainsKey(pack.id)) order.Add(pack.id);
                    byId[pack.id] = pack;
                }
            }
            return order.ConvertAll(id => byId[id]);
        }
    }
}
