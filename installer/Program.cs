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
            try
            {
                return args.Length > 0 ? RunScripted(args) : RunInteractive();
            }
            catch (Exception e)
            {
                Console.WriteLine();
                Console.WriteLine("Error: " + e.Message);
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
                Console.WriteLine($"Hotline setup  (mod version {payload.Version})");
                Console.WriteLine(new string('-', 60));
                if (!GameLocator.LooksLikeGame(game))
                {
                    Console.WriteLine("Couldn't find Scam With Your Friends automatically.");
                    game = AskForGameFolder();
                    if (game == null) return 0;
                    continue;
                }

                var setup = new Setup(game, payload, Console.WriteLine);
                Console.WriteLine($"Game:    {game}");
                Console.WriteLine($"BepInEx: {(setup.BepInExInstalled ? "installed" : "not installed")}");
                Console.WriteLine($"Mod:     {(setup.ModInstalled ? "installed, version " + setup.InstalledVersion : "not installed")}");
                if (setup.ModInstalled)
                {
                    var health = setup.DescribeHealth();
                    Console.WriteLine($"Last run: {health[health.Count > 1 ? 1 : 0]}");
                }
                Console.WriteLine();
                Console.WriteLine(setup.ModInstalled ? "  1  Update / reinstall the mod" : "  1  Install the mod");
                Console.WriteLine("  2  Repair (reinstall BepInEx and the mod)");
                Console.WriteLine("  3  Uninstall the mod (keeps BepInEx and your settings)");
                Console.WriteLine("  4  Uninstall everything (mod and BepInEx)");
                Console.WriteLine("  5  Choose a different game folder");
                Console.WriteLine("  6  Check whether the mod loaded (reads BepInEx's log)");
                Console.WriteLine("  Q  Quit");
                Console.Write("> ");
                var choice = (Console.ReadLine() ?? "q").Trim().ToLowerInvariant();
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
                    Console.WriteLine("Close the game first, then try again.");
                    Pause();
                    continue;
                }

                switch (choice)
                {
                    case "1": setup.Install(); PrintAfterInstall(); break;
                    case "2": setup.Install(repairBepInEx: true); PrintAfterInstall(); break;
                    case "3": setup.UninstallMod(); break;
                    case "4":
                        var others = setup.OtherPlugins();
                        if (others.Count > 0)
                            Console.WriteLine($"Warning: these other mods also use BepInEx and will stop working: {string.Join(", ", others)}");
                        Console.Write("Type YES to remove BepInEx and the mod: ");
                        if ((Console.ReadLine() ?? "").Trim() == "YES") setup.UninstallEverything();
                        else Console.WriteLine("Cancelled.");
                        break;
                    default: Console.WriteLine("Pick 1-6 or Q."); break;
                }
                Pause();
            }
        }

        private static void PrintHealth(Setup setup)
        {
            foreach (var line in setup.DescribeHealth()) Console.WriteLine(line);
            Console.WriteLine();
            Console.WriteLine("Full log: " + setup.LogPath);
            Console.WriteLine("If you report a problem, send that file.");
        }

        private static void PrintAfterInstall()
        {
            Console.WriteLine();
            Console.WriteLine("Done. Start the game from Steam. You should see \"HOTLINE ... activated\" in the top-left corner;");
            Console.WriteLine("press F8 for the mod menu. If you don't see it, run this setup again and pick 6.");
            Console.WriteLine("Steam Deck / Linux (Proton): set this as the game's launch option in Steam:");
            Console.WriteLine("  " + ProtonLaunchOption);
        }

        private static string AskForGameFolder()
        {
            Console.WriteLine("Paste the game folder (the one containing \"Scam With Your Friends.exe\"),");
            Console.Write("or press Enter to quit: ");
            var input = (Console.ReadLine() ?? "").Trim().Trim('"');
            if (input.Length == 0) return null;
            if (GameLocator.LooksLikeGame(input)) return Path.GetFullPath(input);
            Console.WriteLine("That folder doesn't look like the game.");
            Pause();
            return "";
        }

        private static void Pause()
        {
            Console.Write("Press Enter to continue...");
            Console.ReadLine();
        }

        private static bool Has(string[] args, string flag) => Array.IndexOf(args, flag) >= 0;

        private static string Option(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
