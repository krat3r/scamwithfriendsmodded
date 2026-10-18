using System;
using System.Reflection;
using HarmonyLib;

namespace ScamWithFriendsModded.Economy
{
    /// <summary>
    /// Reads the game's current day from ScamazonUnlockSchedule.CurrentDay and raises
    /// <see cref="DayChanged"/> when it increases. Polled from the plugin's Update; the
    /// game exposes no day-change event we can rely on.
    /// </summary>
    internal static class DayWatcher
    {
        private static MethodInfo currentDayGetter;
        private static int lastDay = -1;
        private static bool available;

        public static event Action<int> DayChanged;

        public static void Install()
        {
            var type = GameProbe.Get("ScamazonUnlockSchedule");
            currentDayGetter = type == null ? null : AccessTools.PropertyGetter(type, "CurrentDay");
            available = currentDayGetter != null;
            if (!available)
                Mod.Log.LogWarning("Day watcher disabled: ScamazonUnlockSchedule.CurrentDay not found; crypto market will not advance.");
        }

        public static int CurrentDay
        {
            get
            {
                if (!available) return lastDay < 0 ? 0 : lastDay;
                try { return Convert.ToInt32(currentDayGetter.Invoke(null, null)); }
                catch { return lastDay < 0 ? 0 : lastDay; }
            }
        }

        public static void Poll()
        {
            if (!available) return;
            int day = CurrentDay;
            if (day == lastDay) return;
            lastDay = day;
            try { DayChanged?.Invoke(day); }
            catch (Exception e) { Mod.Log.LogError("Day change handler failed: " + e); }
        }
    }
}
