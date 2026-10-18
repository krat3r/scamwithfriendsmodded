using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Hotline.Cheats;
using Hotline.Economy;
using Hotline.UI;
using UnityEngine;

namespace Hotline
{
    // No [BepInProcess]: a renamed or relaunched exe would otherwise make BepInEx skip us silently.
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.krat3r.hotline";
        public const string Name = "Hotline";
        public const string Version = "0.4.0";

        /// <summary>Subsystems that failed to start, shown on the badge and the Diagnostics page.</summary>
        internal static readonly List<string> StartupErrors = new List<string>();

        private ConfigEntry<KeyCode> menuKey;
        private ConfigEntry<bool> showBadge;
        private Harmony harmony;
        private ModMenu menu;
        private KeyFallback keyFallback;
        private float nextTick;
        private string status = "";

        private void Awake()
        {
            Mod.Log = Logger;
            Mod.Log.LogInfo($"{Name} {Version} starting...");

            menuKey = Config.Bind("General", "MenuKey", KeyCode.F8, "Key that opens the mod menu.");
            showBadge = Config.Bind("General", "ShowBadge", true, "Show the small \"HOTLINE activated\" tag in the top-left corner.");
            Mod.LogGameEvents = Config.Bind("Debug", "LogGameEvents", true,
                "Write hooked game events to the BepInEx log (BepInEx/LogOutput.log).");

            // The menu comes first so it opens even if a game-facing feature fails below.
            menu = new ModMenu(menuKey);
            keyFallback = new KeyFallback(() => menuKey.Value);
            ModMenu.AddPage("Scams", DrawScamsPage);
            ModMenu.AddPage("Caller", CallerPage.Draw);
            ModMenu.AddPage("Heat", HeatPage.Draw);
            ModMenu.AddPage("Cheats", CheatsPage.Draw);
            ModMenu.AddPage("Diagnostics", DrawDiagnosticsPage);
            ModMenu.AddPage("Settings", DrawSettingsPage);
            Step("Mod runner", StartRunner);

            Step("Game probe", GameProbe.Scan);
            Step("Scam packs", () => ScamRegistry.Init(Config, Path.GetDirectoryName(Info.Location)));
            Step("Caller settings", () => CallContext.Bind(Config));
            Step("Heat and wallet", () =>
            {
                CryptoBank.Init();
                DayWatcher.Install();
                DayWatcher.DayChanged += CryptoBank.Instance.OnDay;
                BustedScreen.Hook(CryptoBank.Instance);
            });
            Step("Harmony", () => harmony = new Harmony(Guid));
            if (harmony != null)
            {
                Step("Scam injection", () => ScamInjector.Install(harmony));
                Step("Scamazon apps", () => ScamazonHooks.Install(harmony));
                Step("Call blocker", () => CallBlocker.Install(Config, harmony));
            }
            Step("Cheats", () => GameCheats.Bind(Config));

            Mod.Log.LogInfo(StartupErrors.Count == 0
                ? $"{Name} {Version} loaded. Press {menuKey.Value} in game for the mod menu (key input: IMGUI + {keyFallback.Source})."
                : $"{Name} {Version} loaded with {StartupErrors.Count} problem(s): {string.Join("; ", StartupErrors)}");
        }

        private static void Step(string name, Action action)
        {
            try { action(); }
            catch (Exception e)
            {
                StartupErrors.Add($"{name}: {e.GetType().Name}: {e.Message}");
                Mod.Log.LogError($"{name} failed to start: {e}");
            }
        }

        /// <summary>
        /// Update/OnGUI run on our own hidden, persistent object. Some games destroy
        /// BepInEx's manager object on scene load, which would silently stop this plugin.
        /// </summary>
        private void StartRunner()
        {
            var host = new GameObject("Hotline");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            host.AddComponent<Runner>().Owner = this;
        }

        internal sealed class Runner : MonoBehaviour
        {
            public Plugin Owner;
            private void Update() => Owner?.Tick();
            private void OnGUI() => Owner?.DrawGui();
        }

        private void Tick()
        {
            if (keyFallback.PressedThisFrame()) menu.Toggle();
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 0.5f;
            Guard(DayWatcher.Poll);
            Guard(CryptoPayouts.Poll);
            Guard(GameCheats.Tick);
        }

        private void DrawGui()
        {
            if (showBadge.Value) Badge.Draw(menuKey.Value.ToString(), StartupErrors.Count);
            BustedScreen.OnGUI();
            menu.OnGUI();
        }

        // Errors in per-frame work are logged once each instead of every frame.
        private static readonly HashSet<string> reported = new HashSet<string>();
        private static void Guard(Action action)
        {
            try { action(); }
            catch (Exception e)
            {
                if (reported.Add(action.Method.Name + e.GetType().Name)) Mod.Log.LogError($"{action.Method.Name} failed: {e}");
            }
        }

        // No OnDestroy unpatching: if the game destroys BepInEx's manager object mid-session,
        // that would silently remove every hook while the Runner keeps the menu alive.

        private void DrawScamsPage()
        {
            GUILayout.Label("Buy a scam's app in Scamazon, then pitch it to callers. Changes apply to the next call; new apps appear when the computer is rebuilt (next day or relaunch).");
            foreach (var pack in ScamRegistry.Packs)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                var on = GUILayout.Toggle(ScamRegistry.IsEnabled(pack), $" {pack.title}");
                if (on != ScamRegistry.IsEnabled(pack)) ScamRegistry.SetEnabled(pack, on);
                if (!string.IsNullOrEmpty(pack.description)) GUILayout.Label(pack.description);
                GUILayout.Label($"Reward {pack.reward}{(string.IsNullOrEmpty(pack.payoutCoin) ? "" : $" in {pack.payoutCoin} (adds heat)")}  ·  App price {pack.shop.price}  ·  Unlocks day {pack.shop.unlockDay}");
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
            GUILayout.Label($"{Name} {Version} · key input: IMGUI + {keyFallback.Source}");
            if (StartupErrors.Count > 0)
            {
                GUILayout.Label("Startup problems:");
                foreach (var error in StartupErrors) GUILayout.Label("  " + error);
            }
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
                catch (Exception e) { status = "Dump failed: " + e.Message; Mod.Log.LogError(e); }
            }
            if (status.Length > 0) GUILayout.Label(status);
        }

        private void DrawSettingsPage()
        {
            showBadge.Value = GUILayout.Toggle(showBadge.Value, " Show the HOTLINE badge");
            Mod.LogGameEvents.Value = GUILayout.Toggle(Mod.LogGameEvents.Value, " Log game events");
            if (CallContext.ScaleStockRewards != null)
                CallContext.ScaleStockRewards.Value = GUILayout.Toggle(CallContext.ScaleStockRewards.Value,
                    " Scale the game's own scams by caller wealth");
            if (GUILayout.Button("Preview the \"crime doesn't pay\" screen", GUILayout.Width(300)))
                BustedScreen.Show("This is a preview. Nothing was seized.");
            GUILayout.Label($"Menu key: {menuKey.Value} (change MenuKey in BepInEx/config/{Guid}.cfg)");
        }
    }
}
