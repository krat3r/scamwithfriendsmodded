using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ScamWithFriendsModded.Cheats;
using ScamWithFriendsModded.Economy;
using UnityEngine;

namespace ScamWithFriendsModded
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("Scam With Your Friends.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.krat3r.scamwithfriendsmodded";
        public const string Name = "Scam With Friends Modded";
        public const string Version = "0.2.0";

        private ConfigEntry<KeyCode> menuKey;
        private Harmony harmony;
        private ModMenu menu;
        private string status = "";

        private void Awake()
        {
            Mod.Log = Logger;

            menuKey = Config.Bind("General", "MenuKey", KeyCode.F8, "Key that opens the mod menu.");
            Mod.LogGameEvents = Config.Bind("Debug", "LogGameEvents", true,
                "Write hooked game events to the BepInEx log (BepInEx/LogOutput.log).");

            GameProbe.Scan();
            ScamRegistry.Init(Config, Path.GetDirectoryName(Info.Location));
            CallContext.Bind(Config);
            CryptoBank.Init();
            DayWatcher.Install();
            DayWatcher.DayChanged += CryptoBank.Instance.OnDay;

            harmony = new Harmony(Guid);
            ScamInjector.Install(harmony);
            ScamazonHooks.Install(harmony);
            GameCheats.Bind(Config);
            CallBlocker.Install(Config, harmony);

            menu = new ModMenu(menuKey);
            ModMenu.AddPage("Scams", DrawScamsPage);
            ModMenu.AddPage("Diagnostics", DrawDiagnosticsPage);
            ModMenu.AddPage("Settings", DrawSettingsPage);

            Mod.Log.LogInfo($"{Name} {Version} loaded. Press {menuKey.Value} in game for the mod menu.");
        }

        private void OnGUI() => menu.OnGUI();

        private float nextTick;

        private void Update()
        {
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 0.5f;
            DayWatcher.Poll();
            CryptoPayouts.Poll();
            GameCheats.Tick();
        }

        private void OnDestroy() => harmony?.UnpatchSelf();

        private void DrawScamsPage()
        {
            GUILayout.Label("Buy a scam's app in Scamazon, then pitch it to callers. Changes apply to the next call; new apps appear when the computer is rebuilt (next day or relaunch).");
            foreach (var pack in ScamRegistry.Packs)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                var on = GUILayout.Toggle(ScamRegistry.IsEnabled(pack), $" {pack.title}");
                if (on != ScamRegistry.IsEnabled(pack)) ScamRegistry.SetEnabled(pack, on);
                if (!string.IsNullOrEmpty(pack.description)) GUILayout.Label(pack.description);
                GUILayout.Label($"Reward {pack.reward}  ·  App price {pack.shop.price}  ·  Unlocks day {pack.shop.unlockDay}");
                foreach (var objective in pack.objectives)
                    GUILayout.Label($"  {(objective.final ? "★" : objective.optional ? "○" : "•")} {objective.text}");
                GUILayout.EndVertical();
            }
            GUILayout.Space(8);
            GUILayout.Label($"Add your own packs as .json files in:\n{ScamRegistry.UserFolder}");
            if (GUILayout.Button("Reload scam packs")) ScamRegistry.Reload();
        }

        private void DrawDiagnosticsPage()
        {
            GUILayout.Label($"Game types: {GameProbe.Found.Count} found, {GameProbe.Missing.Count} missing");
            foreach (var name in GameProbe.Missing) GUILayout.Label($"  missing: {name}");
            GUILayout.Label("Hooks installed:");
            foreach (var hook in ScamInjector.InstalledHooks) GUILayout.Label($"  {hook}");
            foreach (var hook in ScamazonHooks.InstalledHooks) GUILayout.Label($"  {hook}");
            GUILayout.Label($"Calls started: {ScamInjector.SessionsStarted}");
            GUILayout.Label($"Modded scams added to calls: {ScamInjector.ScamsInjected}");
            GUILayout.Space(8);
            GUILayout.Label("Dump the game's scams, Scamazon products and computer apps to JSON (start a call and open the computer first so there's data).");
            if (GUILayout.Button("Dump game catalog"))
            {
                try { status = "Written to " + CatalogDump.Write(); }
                catch (System.Exception e) { status = "Dump failed: " + e.Message; Mod.Log.LogError(e); }
            }
            if (status.Length > 0) GUILayout.Label(status);
        }

        private void DrawSettingsPage()
        {
            Mod.LogGameEvents.Value = GUILayout.Toggle(Mod.LogGameEvents.Value, " Log game events");
            GUILayout.Label($"Menu key: {menuKey.Value} (change MenuKey in BepInEx/config/{Guid}.cfg)");
        }
    }
}
