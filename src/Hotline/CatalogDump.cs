using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Hotline
{
    /// <summary>
    /// Writes the game's live scam catalog, Scamazon products and computer apps to
    /// JSON. Use it to see how the stock scams are set up when writing scam packs.
    /// </summary>
    internal static class CatalogDump
    {
        private const int MaxDepth = 5;

        public static string FilePath => Path.Combine(Paths.BepInExRootPath, "Hotline-catalog.json");

        public static string Write()
        {
            var root = new JObject
            {
                ["scams"] = Describe(ScamInjector.LastNativeCatalog, 0),
                ["scamazonProducts"] = Describe(ScamazonHooks.LastProducts, 0),
                ["computerApps"] = Describe(ScamazonHooks.LastApps, 0),
            };
            File.WriteAllText(FilePath, root.ToString(Formatting.Indented));
            Mod.Log.LogInfo($"Catalog written to {FilePath}");
            return FilePath;
        }

        // Checked by name so this class never loads UnityEngine.Object (keeps it testable).
        private static bool IsUnityObject(Type type)
        {
            for (; type != null; type = type.BaseType)
                if (type.FullName == "UnityEngine.Object") return true;
            return false;
        }

        private static JToken Describe(object value, int depth)
        {
            if (value == null) return JValue.CreateNull();
            var type = value.GetType();
            if (value is string || type.IsPrimitive || type.IsEnum || value is decimal) return new JValue(value is Enum ? value.ToString() : value);
            if (IsUnityObject(type)) return $"<{type.Name}>";
            if (depth >= MaxDepth) return $"<{type.Name}>";

            if (value is IDictionary dictionary)
            {
                var obj = new JObject();
                foreach (DictionaryEntry entry in dictionary) obj[entry.Key?.ToString() ?? "null"] = Describe(entry.Value, depth + 1);
                return obj;
            }
            if (value is IEnumerable list)
            {
                var array = new JArray();
                foreach (var item in list) array.Add(Describe(item, depth + 1));
                return array;
            }

            var result = new JObject { ["$type"] = type.Name };
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.Name.Contains("<")) continue; // compiler-generated backing fields
                try { result[field.Name] = Describe(field.GetValue(value), depth + 1); }
                catch (Exception e) { result[field.Name] = $"<error: {e.GetType().Name}>"; }
            }
            return result;
        }
    }
}
