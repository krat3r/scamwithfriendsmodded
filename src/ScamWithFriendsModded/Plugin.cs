using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ScamWithFriendsModded
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("Scam With Your Friends.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.krat3r.scamwithfriendsmodded";
        public const string Name = "Scam With Friends Modded";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> LogGameEvents;

        private ConfigEntry<KeyCode> overlayKey;
        private Harmony harmony;
        private StatusOverlay overlay;

        private void Awake()
        {
            Log = Logger;

            overlayKey = Config.Bind("General", "OverlayKey", KeyCode.F8,
                "Key that toggles the mod status overlay.");
            LogGameEvents = Config.Bind("Debug", "LogGameEvents", true,
                "Write hooked game events to the BepInEx log (BepInEx/LogOutput.log).");

            GameProbe.Scan();

            harmony = new Harmony(Guid);
            GameHooks.Install(harmony);

            overlay = new StatusOverlay(overlayKey);
            Log.LogInfo($"{Name} {Version} loaded. Press {overlayKey.Value} in game for the status overlay.");
        }

        private void OnGUI() => overlay.OnGUI();

        private void OnDestroy() => harmony?.UnpatchSelf();
    }
}
