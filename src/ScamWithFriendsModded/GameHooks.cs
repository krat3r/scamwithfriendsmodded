using System;
using System.Reflection;
using HarmonyLib;

namespace ScamWithFriendsModded
{
    /// <summary>
    /// Example Harmony hooks. They are installed by name and only log, so they are a
    /// safe template for real gameplay changes.
    /// </summary>
    internal static class GameHooks
    {
        public static int ScamSessionsStarted;
        public static int FieldsSubmitted;

        public static void Install(Harmony harmony)
        {
            var session = GameProbe.Get("ConversationScamSession");
            if (session == null) return;

            foreach (var ctor in session.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                TryPatch(harmony, ctor, nameof(SessionCreatedPostfix));

            var submit = AccessTools.Method(session, "SubmitField");
            if (submit != null) TryPatch(harmony, submit, nameof(SubmitFieldPostfix));
            else Plugin.Log.LogWarning("Hook skipped: ConversationScamSession.SubmitField not found.");
        }

        private static void TryPatch(Harmony harmony, MethodBase target, string postfix)
        {
            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(typeof(GameHooks), postfix));
                Plugin.Log.LogInfo($"Hooked {target.DeclaringType?.Name}.{target.Name}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to hook {target.DeclaringType?.Name}.{target.Name}: {e}");
            }
        }

        private static void SessionCreatedPostfix()
        {
            ScamSessionsStarted++;
            if (Plugin.LogGameEvents.Value) Plugin.Log.LogInfo("Scam session started.");
        }

        private static void SubmitFieldPostfix()
        {
            FieldsSubmitted++;
            // Arguments are not logged: they can contain values the player typed.
            if (Plugin.LogGameEvents.Value) Plugin.Log.LogInfo("Field submitted.");
        }
    }
}
