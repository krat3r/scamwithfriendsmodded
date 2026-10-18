using System;
using System.IO;

namespace Hotline.Installer
{
    /// <summary>
    /// Double-click: a numbered menu. Scripted:
    ///   Hotline-Setup --install [--game "path"] [--repair]
    ///   Hotline-Setup --uninstall [--all] [--game "path"] [--yes]
    ///   Hotline-Setup --check [--game "path"]     (reads BepInEx's log; exit 0 if Hotline loaded)
    /// </summary>
    internal static class Program
    {
        private const string ProtonLaunchOption = "WINEDLLOVERRIDES=\"winhttp=n,b\" %command%";

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
                setup.Install(repairBepInEx: Has(args, "--repair"));
                PrintAfterInstall();
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
            Console.WriteLine("Use --install, --uninstall or --check.");
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
                Ui.Field("BepInEx", setup.BepInExInstalled ? "installed" : "not installed",
                    setup.BepInExInstalled ? Ui.StatusGood : Ui.StatusWarn);
                Ui.Field("Mod", setup.ModInstalled ? "installed · v" + setup.InstalledVersion : "not installed",
                    setup.ModInstalled ? Ui.StatusGood : Ui.StatusWarn);
                if (setup.ModInstalled)
                {
                    var health = setup.CheckHealth();
                    if (health.LogFound)
                        Ui.Field("Last run", health.HotlineLoaded ? "Hotline loaded" : "Hotline did NOT load",
                            health.HotlineLoaded ? Ui.StatusGood : Ui.StatusBad);
                }
                Console.WriteLine();
                Ui.Option("1", setup.ModInstalled ? "Update / reinstall the mod" : "Install the mod");
                Ui.Option("2", "Repair (reinstall BepInEx and the mod)");
                Ui.Option("3", "Uninstall the mod " + Dim("(keeps BepInEx and your settings)"));
                Ui.Option("4", "Uninstall everything " + Dim("(mod and BepInEx)"));
                Ui.Option("5", "Choose a different game folder");
                Ui.Option("6", "Check whether the mod loaded " + Dim("(reads BepInEx's log)"));
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
                if (choice is "1" or "2" or "3" or "4" && Setup.GameRunning())
                {
                    Ui.Warn("Close the game first, then try again.");
                    Pause();
                    continue;
                }

                switch (choice)
                {
                    case "1": setup.Install(); Ui.Ok("Installed."); PrintAfterInstall(); break;
                    case "2": setup.Install(repairBepInEx: true); Ui.Ok("Repaired."); PrintAfterInstall(); break;
                    case "3": setup.UninstallMod(); Ui.Ok("Done."); break;
                    case "4":
                        var others = setup.OtherPlugins();
                        if (others.Count > 0)
                            Ui.Warn($"These other mods also use BepInEx and will stop working: {string.Join(", ", others)}");
                        if (Ui.Prompt("Type YES to remove BepInEx and the mod:") == "YES") { setup.UninstallEverything(); Ui.Ok("Done."); }
                        else Ui.Info("Cancelled.");
                        break;
                    default: Ui.Warn("Pick 1-6 or Q."); break;
                }
                Pause();
            }
        }

        private static void PrintHealth(Setup setup)
        {
            Console.WriteLine();
            Ui.Heading("Mod health");
            var health = setup.CheckHealth();
            if (!health.LogFound) Ui.Warn(setup.DescribeHealth()[0]);
            else if (health.HotlineLoaded) Ui.Ok("Hotline loaded in the last run.");
            else Ui.Fail("Hotline did NOT load in the last run.");
            foreach (var line in setup.DescribeHealth()) Ui.Muted(line);
            Console.WriteLine();
            Ui.Muted("Full log: " + setup.LogPath);
            Ui.Muted("If you report a problem, send that file.");
        }

        private static void PrintAfterInstall()
        {
            Console.WriteLine();
            Ui.Info("Start the game from Steam. You should see \"HOTLINE activated\" in the top-left corner;");
            Ui.Info("press F8 for the mod menu. If you don't see it, run this setup again and pick 6.");
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
