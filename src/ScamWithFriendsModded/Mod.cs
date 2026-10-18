using BepInEx.Configuration;
using BepInEx.Logging;

namespace ScamWithFriendsModded
{
    /// <summary>
    /// Shared state set up by <see cref="Plugin"/>. Kept out of the plugin class so
    /// game-facing code doesn't depend on MonoBehaviour and can run in tests.
    /// </summary>
    internal static class Mod
    {
        public static ManualLogSource Log;
        public static ConfigEntry<bool> LogGameEvents;
    }
}
