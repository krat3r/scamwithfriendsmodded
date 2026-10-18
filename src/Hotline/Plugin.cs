using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Hotline.Cheats;
using Hotline.Economy;
using Hotline.UI;
using Hotline.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hotline
{
    // No [BepInProcess]: a renamed or relaunched exe would otherwise make BepInEx skip us silently.
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.krat3r.hotline";
        public const string Name = "Hotline";
        public const string Version = "0.5.0";

        /// <summary>Subsystems that failed to start, shown on the badge and the Diagnostics page.</summary>
        internal static readonly List<string> StartupErrors = new List<string>();

        private ConfigEntry<KeyCode> menuKey;
        private ConfigEntry<bool> showBadge;
        private Harmony harmony;
        private ModMenu menu;
        private KeyFallback keyFallback;
        private MouseFallback mouseFallback;
        private int nextTickMs;
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
            mouseFallback = new MouseFallback();
            ModMenu.AddPage("Scams", BuildScamsPage);
            ModMenu.AddPage("Caller", CallerPage.Build);
            ModMenu.AddPage("Heat", HeatPage.Build);
            ModMenu.AddPage("Cheats", CheatsPage.Build);
            ModMenu.AddPage("Diagnostics", BuildDiagnosticsPage);
            ModMenu.AddPage("Settings", BuildSettingsPage);
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
                ? $"{Name} {Version} loaded. Press {menuKey.Value} or click the HOTLINE tag for the menu (key input: {keyFallback.Source}; mouse: {(mouseFallback.Available ? "Input System" : "none")})."
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
            var host = new GameObject("Hotline Runner");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            host.AddComponent<Runner>().Owner = this;
        }

        internal sealed class Runner : MonoBehaviour
        {
            public Plugin Owner;
            private void Update() => Owner?.Tick();
            private void OnGUI() => Owner?.DrawBadge();
        }

        private void Tick()
        {
            // One toggle per frame, from the Input System only (IMGUI gets no usable input here).
            bool key = keyFallback.PressedThisFrame();
            bool badgeClick = showBadge.Value && mouseFallback.LeftPressedThisFrame(out var click) && Badge.LastRect.Contains(click);
            if (key) menu.Toggle("key (" + keyFallback.Source + ")");
            else if (badgeClick) menu.Toggle("badge click");

            // Wall clock, not Time.unscaledTime: this game's Unity time didn't advance in the runner.
            int now = Environment.TickCount;
            if (unchecked(now - nextTickMs) < 0) return;
            nextTickMs = now + 500;
            Guard(DayWatcher.Poll);
            Guard(CryptoPayouts.Poll);
            Guard(GameCheats.Tick);
        }

        // Badge display stays on IMGUI: it renders fine (players see it) and only needs to be drawn,
        // not clicked - clicks are detected through the Input System in Tick.
        private void DrawBadge()
        {
            if (showBadge.Value) Badge.Draw(menuKey.Value.ToString(), StartupErrors.Count);
            BustedScreen.OnGUI();
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

        private void BuildScamsPage(VisualElement page)
        {
            W.Label(page, "Buy a scam's app in Scamazon, then pitch it to callers. Changes apply to the next call; new apps appear when the computer is rebuilt (next day or relaunch).");
            foreach (var pack in ScamRegistry.Packs)
            {
                var box = W.Box(page);
                W.Toggle(box, " " + pack.title, ScamRegistry.IsEnabled(pack), on => ScamRegistry.SetEnabled(pack, on));
                if (!string.IsNullOrEmpty(pack.description)) W.Note(box, pack.description);
                W.Note(box, $"Reward {pack.reward}{(string.IsNullOrEmpty(pack.payoutCoin) ? "" : $" in {pack.payoutCoin} (adds heat)")}  ·  App price {pack.shop.price}  ·  Unlocks day {pack.shop.unlockDay}");
                foreach (var objective in pack.objectives)
                    W.Note(box, $"  {(objective.final ? "★" : objective.optional ? "○" : "•")} {objective.text}");
            }
            W.Space(page);
            W.Note(page, "Add your own packs as .json files in:\n" + ScamRegistry.UserFolder);
            W.Button(page, "Reload scam packs", () => { ScamRegistry.Reload(); ModMenu.Refresh(); });
        }

        private void BuildDiagnosticsPage(VisualElement page)
        {
            W.Label(page, $"{Name} {Version} · key input: {keyFallback.Source}");
            if (StartupErrors.Count > 0)
            {
                W.Heading(page, "Startup problems");
                foreach (var error in StartupErrors) W.Note(page, "  " + error);
            }
            W.FieldRow(page, "Game types", $"{GameProbe.Found.Count} found, {GameProbe.Missing.Count} missing");
            foreach (var name in GameProbe.Missing) W.Note(page, "  missing: " + name);
            W.Heading(page, "Hooks installed");
            foreach (var hook in ScamInjector.InstalledHooks) W.Note(page, "  " + hook);
            foreach (var hook in ScamazonHooks.InstalledHooks) W.Note(page, "  " + hook);
            W.FieldRow(page, "Calls started", ScamInjector.SessionsStarted.ToString());
            W.FieldRow(page, "Modded scams added", ScamInjector.ScamsInjected.ToString());
            W.Space(page);
            W.Note(page, "Dump the game's scams, Scamazon products and computer apps to JSON (start a call and open the computer first so there's data).");
            W.Button(page, "Dump game catalog", () =>
            {
                try { status = "Written to " + CatalogDump.Write(); }
                catch (Exception e) { status = "Dump failed: " + e.Message; Mod.Log.LogError(e); }
                ModMenu.Refresh();
            });
            if (status.Length > 0) W.Note(page, status);
        }

        private void BuildSettingsPage(VisualElement page)
        {
            W.Toggle(page, " Show the HOTLINE badge", showBadge.Value, v => showBadge.Value = v);
            W.Toggle(page, " Log game events", Mod.LogGameEvents.Value, v => Mod.LogGameEvents.Value = v);
            if (CallContext.ScaleStockRewards != null)
                W.Toggle(page, " Scale the game's own scams by caller wealth", CallContext.ScaleStockRewards.Value,
                    v => CallContext.ScaleStockRewards.Value = v);
            W.Button(page, "Preview the \"crime doesn't pay\" screen", () => BustedScreen.Show("This is a preview. Nothing was seized."));
            W.Note(page, $"Menu key: {menuKey.Value} (change MenuKey in BepInEx/config/{Guid}.cfg)");
        }
    }
}
