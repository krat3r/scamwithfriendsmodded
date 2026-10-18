using System;
using System.IO;

namespace Hotline.Installer
{
    /// <summary>
    /// Double-click: a numbered menu. Scripted:
    ///   Hotline-Setup --install [--game "path"] [--repair]
    ///   Hotline-Setup --uninstall [--all] [--game "path"] [--yes]
    ///   Hotline-Setup --check [--game "path"]     (reads BepInEx's log; exit 0 if Hotline loaded)
    ///   Hotline-Setup --loader version|winhttp [--game "path"]   (rename BepInEx's loader DLL)
    /// </summary>
    internal static class Program
    {
        // From the community setup: n,b (not n alone) avoids a Unity window crash on Proton;
        // -whisper-cpu runs the game's speech recognition on the CPU there.
        private const string ProtonLaunchOption = ScamWYF.Setup.GameInstall.ProtonLaunchOption;

        private static int Main(string[] args)
        {
            Ui.Init();
            try
            {
                return args.Length > 0 ? RunScripted(args) : RunInteractive();
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Ui.Fail("Error: " + e.Message);
                if (args.Length == 0) Pause();
                return 1;
            }
        }

        private static int RunScripted(string[] args)
        {
            string game = Option(args, "--game") ?? GameLocator.Find();
            if (!GameLocator.LooksLikeGame(game))
            {
                Console.WriteLine("Game not found. Pass --game \"path to the game folder\".");
                return 2;
            }
            var setup = new Setup(game, Payload.Default(), Console.WriteLine);
            if (Has(args, "--check"))
            {
                PrintHealth(setup);
                return setup.CheckHealth().HotlineLoaded ? 0 : 5;
            }
            if (Setup.GameRunning()) { Console.WriteLine("Close the game first."); return 3; }

            if (Has(args, "--install"))
            {
                var installProblems = setup.Install();
                PrintAfterInstall(setup, installProblems);
                if (installProblems.Count > 0) return 6;
                return 0;
            }
            var loader = Option(args, "--loader");
            if (loader != null)
            {
                setup.SwitchLoader(loader.EndsWith(".dll") ? loader : loader + ".dll");
                return 0;
            }
            if (Has(args, "--uninstall"))
            {
                if (Has(args, "--all"))
                {
                    if (setup.OtherPlugins().Count > 0 && !Has(args, "--yes"))
                    {
                        Console.WriteLine("Other BepInEx mods are installed and would stop working. Add --yes to remove BepInEx anyway.");
                        return 4;
                    }
                    setup.UninstallEverything();
                }
                else setup.UninstallMod();
                return 0;
            }
            Console.WriteLine("Use --install, --uninstall, --check or --loader.");
            return 2;
        }

        private static int RunInteractive()
        {
            var payload = Payload.Default();
            string game = GameLocator.Find();
            while (true)
            {
                Console.Clear();
                Ui.Banner(payload.Version);
                if (!GameLocator.LooksLikeGame(game))
                {
                    Ui.Warn("Couldn't find Scam With Your Friends automatically.");
                    game = AskForGameFolder();
                    if (game == null) return 0;
                    continue;
                }

                var setup = new Setup(game, payload, Ui.Info);
                Ui.Field("Game", game);
                Ui.Field("BepInEx", setup.BepInExInstalled ? $"installed · loader {setup.ActiveLoader}" : "not installed",
                    setup.BepInExInstalled ? Ui.StatusGood : Ui.StatusWarn);
                Ui.Field("Mod", setup.ModInstalled ? "installed · v" + setup.InstalledVersion : "not installed",
                    setup.ModInstalled ? Ui.StatusGood : Ui.StatusWarn);
                if (setup.ModInstalled)
                {
                    var health = setup.CheckHealth();
                    if (health.LogFound)
                        Ui.Field("Last run", health.HotlineLoaded ? "Hotline loaded" : "Hotline did NOT load",
                            health.HotlineLoaded ? Ui.StatusGood : Ui.StatusBad);
                    else
                        Ui.Field("Last run", "BepInEx hasn't started yet", Ui.StatusWarn);
                }
                string otherLoader = setup.ActiveLoader == Setup.AlternateLoader ? Setup.DefaultLoader : Setup.AlternateLoader;
                Console.WriteLine();
                Ui.Option("1", setup.ModInstalled ? "Update / reinstall Hotline" : "Install Hotline");
                Ui.Option("2", "Repair " + Dim("(redo the full setup; use after game updates)"));
                Ui.Option("3", "Uninstall the mod " + Dim("(keeps BepInEx and your settings)"));
                Ui.Option("4", "Uninstall everything " + Dim("(mod and BepInEx)"));
                Ui.Option("5", "Choose a different game folder");
                Ui.Option("6", "Check whether the mod loaded " + Dim("(reads BepInEx's log)"));
                if (setup.BepInExInstalled)
                    Ui.Option("7", $"Switch BepInEx's loader to {otherLoader} " + Dim("(if BepInEx never starts)"));
                Ui.Option("Q", "Quit");
                var choice = Ui.Prompt("Choose an option:").ToLowerInvariant();
                Console.WriteLine();

                if (choice == "q" || choice == "") return 0;
                if (choice == "5") { game = AskForGameFolder() ?? game; continue; }
                if (choice == "6")
                {
                    PrintHealth(setup);
                    Pause();
                    continue;
                }
                if (choice is "1" or "2" or "3" or "4" or "7" && Setup.GameRunning())
                {
                    Ui.Warn("Close the game first, then try again.");
                    Pause();
                    continue;
                }

                switch (choice)
                {
                    case "1":
                    case "2": PrintAfterInstall(setup, setup.Install()); break;
                    case "3": setup.UninstallMod(); Ui.Ok("Done."); break;
                    case "4":
                        var others = setup.OtherPlugins();
                        if (others.Count > 0)
                            Ui.Warn($"These other mods also use BepInEx and will stop working: {string.Join(", ", others)}");
                        if (Ui.Prompt("Type YES to remove BepInEx and the mod:") == "YES") { setup.UninstallEverything(); Ui.Ok("Done."); }
                        else Ui.Info("Cancelled.");
                        break;
                    case "7" when setup.BepInExInstalled: setup.SwitchLoader(otherLoader); Ui.Ok("Done."); break;
                    default: Ui.Warn("Pick one of the numbers shown, or Q."); break;
                }
                Pause();
            }
        }

        private static void PrintHealth(Setup setup)
        {
            Console.WriteLine();
            Ui.Heading("Mod health");
            var health = setup.CheckHealth();
            if (health.LogFound)
            {
                Ui.Muted($"BepInEx last ran {health.LogTime:g}.");
                if (health.HotlineLoaded) Ui.Ok("Hotline loaded: " + health.HotlineLine);
                else Ui.Fail("Hotline did NOT load in that run.");
                foreach (var problem in health.Problems) Ui.Fail(problem);
                if (!health.HotlineLoaded && health.Problems.Count == 0)
                    Ui.Muted("No error mentions Hotline. If you installed after that run, start the game again and recheck.");
                Console.WriteLine();
                Ui.Muted("Full log: " + setup.LogPath);
                if (!health.HotlineLoaded)
                    Ui.Info("Try 1 (reinstall the mod), start the game, then check again. If it still fails, check for a newer Hotline release.");
                return;
            }

            if (!setup.BepInExInstalled && setup.ActiveLoader == null && !File.Exists(Path.Combine(setup.BepInExDir, "core", "BepInEx.dll")))
            {
                Ui.Warn("BepInEx isn't installed yet. Pick 1 to install.");
                return;
            }
            Ui.Warn("BepInEx hasn't written a log, so it never started.");
            Ui.Muted("If you haven't launched the game since installing, start it from Steam, reach the main menu, then check again.");
            if (!OperatingSystem.IsWindows())
                Ui.Muted("On Steam Deck / Linux, BepInEx only runs with the launch option shown after installing.");
            Console.WriteLine();
            Ui.Heading("Loader checks");
            bool anyFailed = false, blocked = false;
            foreach (var check in setup.CheckLoader())
            {
                if (check.Ok) Ui.Ok(check.Message);
                else
                {
                    Ui.Fail(check.Message);
                    anyFailed |= !check.Message.StartsWith("BepInEx has never started");
                    blocked |= check.Blocker;
                }
            }
            bool unityChanged = false;
            var unity = setup.ReadUnityLog();
            if (unity != null)
            {
                Console.WriteLine();
                Ui.Heading("Game build (from Unity's Player.log)");
                Ui.Muted("Unity " + (unity.EngineVersion ?? "version unknown") + (unity.Backend != null ? " · " + unity.Backend : ""));
                if (unity.Backend == "IL2CPP")
                {
                    Ui.Fail("This build runs on IL2CPP, which this Hotline version can't load into.");
                    blocked = true;
                }
                if (unity.EngineVersion != null && !unity.EngineVersion.StartsWith(Setup.BundledUnityVersion))
                {
                    Ui.Warn($"The game now runs a different Unity version than this Hotline release was built for ({Setup.BundledUnityVersion}).");
                    unityChanged = true;
                }
                Ui.Muted(unity.LogPath);
            }

            Console.WriteLine();
            Ui.Heading("What to do");
            if (blocked)
            {
                Ui.Info("Hotline can't run on this version of the game (see × above). Nothing on your PC needs fixing.");
                Ui.Info("Pick 4 to return the game to stock, and check for a newer Hotline release that supports it.");
            }
            else if (anyFailed) Ui.Info("Fix the items marked × above (most need just 2, Repair), then start the game again and pick 6.");
            else if (unityChanged)
            {
                Ui.Info("A game update changed its Unity version, which is the likely reason BepInEx can't start.");
                Ui.Info("Check for a newer Hotline release. Until then, pick 4 to return the game to stock.");
            }
            else if (setup.ActiveLoader == Setup.DefaultLoader)
            {
                Ui.Info("Everything is in place, so the game probably isn't loading winhttp.dll.");
                Ui.Info("Pick 7 to switch BepInEx's loader to version.dll, then start the game again and pick 6.");
            }
            else
            {
                Ui.Info("Every check passed but BepInEx still didn't start. Try these in order, starting the game after each:");
                Ui.Info("  a) Pick 2 (Repair).");
                Ui.Info("  b) Make sure your antivirus didn't quarantine the loader DLL; allow the game folder if it did.");
                Ui.Info("  c) Pick 7 to switch the loader back to winhttp.dll.");
                Ui.Info("If none of that works, this game version probably isn't supported yet: pick 4 to return the game");
                Ui.Info("to stock, and check for a newer Hotline release.");
            }
        }

        private static void PrintAfterInstall(Setup setup, System.Collections.Generic.List<string> problems)
        {
            Console.WriteLine();
            if (problems.Count == 0) Ui.Ok("BepInEx and Hotline are set up.");
            else
            {
                Ui.Warn("Setup finished, but its final check found problems:");
                foreach (var problem in problems) Ui.Fail(problem);
                Ui.Info("Pick 2 (Repair) once. If they're still there, this game version may need a newer Hotline release.");
                Ui.Muted("Details: " + setup.SetupLogPath);
            }
            Ui.Info("Start the game from Steam. You should see \"HOTLINE activated\" in the top-left corner;");
            Ui.Info("press F8 for the mod menu. If you don't see it, run this setup again and pick 6.");
            Ui.Muted("Run this setup again (pick 2) after the game updates.");
            Ui.Muted("Steam Deck / Linux (Proton): set this as the game's launch option in Steam:");
            Ui.Muted("  " + ProtonLaunchOption);
        }

        private static string AskForGameFolder()
        {
            Ui.Info("Paste the game folder (the one containing \"Scam With Your Friends.exe\").");
            var input = Ui.Prompt("Folder (or Enter to quit):").Trim('"');
            if (input.Length == 0) return null;
            if (GameLocator.LooksLikeGame(input)) return Path.GetFullPath(input);
            Ui.Fail("That folder doesn't look like the game.");
            Pause();
            return "";
        }

        private static string Dim(string text) => Ui.Color ? "\x1b[90m" + text + "\x1b[0m" : text;

        private static void Pause() => Ui.Prompt("Press Enter to continue...");

        private static bool Has(string[] args, string flag) => Array.IndexOf(args, flag) >= 0;

        private static string Option(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
