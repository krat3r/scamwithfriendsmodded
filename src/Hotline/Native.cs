using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;

namespace Hotline
{
    /// <summary>
    /// Reflection helpers for game objects. Everything goes through names so a game
    /// update that renames a member disables one feature instead of the whole mod.
    /// </summary>
    internal static class Native
    {
        private static readonly MethodInfo memberwiseClone = AccessTools.Method(typeof(object), "MemberwiseClone");

        public static Type Type(string name) => AccessTools.TypeByName(name);

        public static object New(Type type) => type == null ? null : Activator.CreateInstance(type, true);

        public static object Clone(object source) => source == null ? null : memberwiseClone.Invoke(source, null);

        public static object Get(object target, string member)
        {
            if (target == null) return null;
            var field = AccessTools.Field(target.GetType(), member);
            if (field != null) return field.GetValue(target);
            var property = AccessTools.Property(target.GetType(), member);
            return property != null && property.CanRead ? property.GetValue(target, null) : null;
        }

        public static string GetString(object target, string member) => Get(target, member) as string;

        /// <summary>Sets a field or property, converting simple values. Returns false if the member doesn't exist.</summary>
        public static bool Set(object target, string member, object value)
        {
            if (target == null) return false;
            var field = AccessTools.Field(target.GetType(), member);
            if (field != null)
            {
                field.SetValue(target, Convert(value, field.FieldType));
                return true;
            }
            var property = AccessTools.Property(target.GetType(), member);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, Convert(value, property.PropertyType), null);
                return true;
            }
            return false;
        }

        public static Type MemberType(Type owner, string member)
        {
            var field = AccessTools.Field(owner, member);
            if (field != null) return field.FieldType;
            return AccessTools.Property(owner, member)?.PropertyType;
        }

        /// <summary>Copies items into a typed array, e.g. object list -> ConversationScamDefinition[].</summary>
        public static Array ToArray(Type elementType, IList items)
        {
            var array = Array.CreateInstance(elementType, items.Count);
            for (int i = 0; i < items.Count; i++) array.SetValue(items[i], i);
            return array;
        }

        private static object Convert(object value, Type type)
        {
            if (value == null || type.IsInstanceOfType(value)) return value;
            if (type.IsEnum) return value is string s ? Enum.Parse(type, s, true) : Enum.ToObject(type, value);
            return System.Convert.ChangeType(value, type);
        }
    }
}
