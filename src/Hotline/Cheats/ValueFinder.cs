using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace Hotline.Cheats
{
    /// <summary>
    /// A numeric field or property somewhere in the game, e.g. the player's money or level.
    /// Static members are read directly; instance members are read from the first live
    /// Unity object of that type (looked up again if the scene changes).
    /// </summary>
    internal sealed class ValueRef
    {
        public readonly Type Type;
        public readonly MemberInfo Member;
        public readonly bool IsStatic;
        private UnityEngine.Object cachedInstance;

        public ValueRef(Type type, MemberInfo member, bool isStatic)
        {
            Type = type;
            Member = member;
            IsStatic = isStatic;
        }

        public string Key => Type.FullName + "::" + Member.Name;
        public string Label => $"{Type.Name}.{Member.Name}{(IsStatic ? " (static)" : "")}";

        private Type ValueType => Member is FieldInfo f ? f.FieldType : ((PropertyInfo)Member).PropertyType;

        private object Target()
        {
            if (IsStatic) return null;
            if (cachedInstance == null)
            {
                var found = UnityEngine.Object.FindObjectsOfType(Type);
                cachedInstance = found != null && found.Length > 0 ? found[0] : null;
            }
            return cachedInstance;
        }

        public bool Available => IsStatic || Target() != null;

        public double? Read()
        {
            try
            {
                var target = Target();
                if (!IsStatic && target == null) return null;
                object raw = Member is FieldInfo f ? f.GetValue(target) : ((PropertyInfo)Member).GetValue(target, null);
                return raw == null ? (double?)null : Convert.ToDouble(raw);
            }
            catch { return null; }
        }

        public bool Write(double value)
        {
            try
            {
                var target = Target();
                if (!IsStatic && target == null) return false;
                object converted = Convert.ChangeType(Math.Round(value, ValueType == typeof(float) || ValueType == typeof(double) ? 2 : 0), ValueType);
                if (Member is FieldInfo f) f.SetValue(target, converted);
                else ((PropertyInfo)Member).SetValue(target, converted, null);
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.LogWarning($"Could not write {Label}: {e.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Scans the game's assemblies for numeric members whose names look like money or
    /// progression, so cheats can work before we know the exact field names.
    /// </summary>
    internal static class ValueFinder
    {
        private const int MaxResults = 300;
        private static readonly Regex Keywords = new Regex(
            "money|cash|balance|funds|wallet|bank|coin|credit|currency|level|xp|exp|experience|rank|reputation",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly HashSet<Type> Numeric = new HashSet<Type>
        {
            typeof(int), typeof(long), typeof(float), typeof(double), typeof(uint), typeof(ulong), typeof(short),
        };

        public static IEnumerable<Assembly> GameAssemblies()
        {
            var anchor = GameProbe.Get("ConversationScamSession")?.Assembly;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var name = assembly.GetName().Name;
                if (assembly == anchor || name.StartsWith("Assembly-CSharp") || name == "ScriptsAssDef")
                    yield return assembly;
            }
        }

        public static IEnumerable<Type> GameTypes()
        {
            foreach (var assembly in GameAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                foreach (var type in types)
                    if (type != null && !type.IsGenericTypeDefinition) yield return type;
            }
        }

        public static List<ValueRef> Scan(string filter = null)
        {
            var results = new List<ValueRef>();
            var unityObject = typeof(UnityEngine.Object);
            foreach (var type in GameTypes())
            {
                bool isUnity = unityObject.IsAssignableFrom(type);
                bool hasLiveInstance = false, checkedInstance = false;
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

                foreach (var member in type.GetMembers(flags))
                {
                    if (member.Name.Contains("<")) continue;
                    if (!Matches(type, member, filter)) continue;

                    bool isStatic;
                    if (member is FieldInfo field)
                    {
                        if (!Numeric.Contains(field.FieldType) || field.IsLiteral || field.IsInitOnly) continue;
                        isStatic = field.IsStatic;
                    }
                    else if (member is PropertyInfo property)
                    {
                        if (!Numeric.Contains(property.PropertyType) || !property.CanRead || !property.CanWrite) continue;
                        if (property.GetIndexParameters().Length > 0) continue;
                        isStatic = property.GetGetMethod(true)?.IsStatic ?? false;
                    }
                    else continue;

                    if (!isStatic)
                    {
                        if (!isUnity) continue; // plain objects can't be found without a path to them
                        if (!checkedInstance)
                        {
                            checkedInstance = true;
                            try { hasLiveInstance = UnityEngine.Object.FindObjectsOfType(type).Length > 0; }
                            catch { hasLiveInstance = false; }
                        }
                        if (!hasLiveInstance) continue;
                    }

                    results.Add(new ValueRef(type, member, isStatic));
                    if (results.Count >= MaxResults) return results;
                }
            }
            return results;
        }

        private static bool Matches(Type type, MemberInfo member, string filter)
        {
            if (!string.IsNullOrEmpty(filter))
                return member.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || type.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            return Keywords.IsMatch(member.Name);
        }

        /// <summary>Finds a member again from a saved key ("Full.Type::Member").</summary>
        public static ValueRef Resolve(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            int split = key.LastIndexOf("::", StringComparison.Ordinal);
            if (split < 0) return null;
            var type = AccessTools.TypeByName(key.Substring(0, split));
            if (type == null) return null;
            string name = key.Substring(split + 2);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var field = type.GetField(name, flags);
            if (field != null) return new ValueRef(type, field, field.IsStatic);
            var property = type.GetProperty(name, flags);
            if (property != null) return new ValueRef(type, property, property.GetGetMethod(true)?.IsStatic ?? false);
            return null;
        }
    }
}
