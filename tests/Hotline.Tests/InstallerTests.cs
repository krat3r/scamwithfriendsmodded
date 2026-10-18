using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Hotline.Installer;

/// <summary>Installer checks against temporary Steam and game folders.</summary>
internal static class InstallerTests
{
    public static void Run(Action<bool, string> check)
    {
        var temp = Path.Combine(Path.GetTempPath(), "swyf-installer-" + Guid.NewGuid().ToString("N"));
        try
        {
            TestVdf(check);
            TestLocator(check, temp);
            TestInstall(check, temp);
            TestZipSlip(check, temp);
            TestLoader(check, temp);
        }
        finally
        {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    private static void TestVdf(Action<bool, string> check)
    {
        const string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"D:\\\\SteamLibrary\"\n\t}\n}";
        var paths = GameLocator.ParseLibraryPaths(vdf).ToList();
        check(paths.Count == 2 && paths[0] == @"C:\Program Files (x86)\Steam" && paths[1] == @"D:\SteamLibrary",
            "installer parses Steam library paths from libraryfolders.vdf");
    }

    private static void TestLocator(Action<bool, string> check, string temp)
    {
        var steam = Path.Combine(temp, "Steam");
        var library = Path.Combine(temp, "Library2");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
            "\"libraryfolders\" { \"1\" { \"path\" \"" + library.Replace("\\", "\\\\") + "\" } }");
        var game = Path.Combine(library, "steamapps", "common", "SWYF EA");
        Directory.CreateDirectory(Path.Combine(game, GameLocator.DataFolderName));
        File.WriteAllText(Path.Combine(library, "steamapps", $"appmanifest_{GameLocator.AppId}.acf"),
            "\"AppState\" { \"appid\" \"4954910\" \"installdir\" \"SWYF EA\" }");

        check(GameLocator.Find(steam) == game, "installer finds the game in a second Steam library via its app manifest");
    }

    private static void TestInstall(Action<bool, string> check, string temp)
    {
        var game = Path.Combine(temp, "Game");
        Directory.CreateDirectory(Path.Combine(game, GameLocator.DataFolderName));
        var payload = Path.Combine(temp, "payload");
        Directory.CreateDirectory(payload);
        MakeZip(Path.Combine(payload, Payload.BepInExZip),
            ("winhttp.dll", "loader"), ("doorstop_config.ini", "cfg"), ("BepInEx/core/BepInEx.dll", "core"));
        MakeZip(Path.Combine(payload, Payload.ModZip),
            ("Hotline.dll", "mod"), ("ScamPacks/a.json", "{}"));
        File.WriteAllText(Path.Combine(payload, Payload.VersionFile), "9.9.9");

        var setup = new Setup(game, Payload.FromFolder(payload), null);
        setup.Install();
        check(setup.BepInExInstalled && setup.ModInstalled && setup.InstalledVersion == "9.9.9"
            && File.Exists(Path.Combine(setup.PluginDir, "ScamPacks", "a.json")),
            "install puts BepInEx next to the game and the mod in BepInEx/plugins");

        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "plugins", "SomeoneElsesMod"));
        check(setup.OtherPlugins().SequenceEqual(new[] { "SomeoneElsesMod" }), "installer notices other BepInEx mods");

        setup.UninstallMod();
        check(!setup.ModInstalled && setup.BepInExInstalled, "uninstalling the mod keeps BepInEx");

        var legacy = Path.Combine(game, "BepInEx", "plugins", "ScamWithFriendsModded");
        Directory.CreateDirectory(legacy);
        setup.Install();
        check(!Directory.Exists(legacy) && setup.ModInstalled, "install removes the old ScamWithFriendsModded folder");

        check(!setup.CheckHealth().LogFound, "health check reports no log before BepInEx has run");
        File.WriteAllLines(setup.LogPath, new[]
        {
            "[Message:   BepInEx] BepInEx 5.4.23.4 - Scam With Your Friends",
            "[Info   :   BepInEx] Chainloader started",
            "[Info   :   BepInEx] Loading [Hotline 0.4.0]",
            "[Error  :   Hotline] Scam packs failed to start: boom",
            "[Info   :   Hotline] Hotline 0.4.0 loaded with 1 problem(s): Scam packs: boom",
        });
        var health = setup.CheckHealth();
        check(health.BepInExRan && health.HotlineLoaded && health.Problems.Count == 1 && health.Problems[0].Contains("boom"),
            "health check reads BepInEx's log: ran, loaded, and the error line");
        File.WriteAllLines(setup.LogPath, new[]
        {
            "[Info   :   BepInEx] Chainloader started",
            "[Error  : BepInEx] Could not load [Hotline 0.4.0] because it has missing dependencies: netstandard",
        });
        health = setup.CheckHealth();
        check(health.BepInExRan && !health.HotlineLoaded && health.Problems.Count == 1, "health check flags a plugin that failed to load");

        setup.Install();
        setup.UninstallEverything();
        check(!Directory.Exists(Path.Combine(game, "BepInEx")) && !File.Exists(Path.Combine(game, "winhttp.dll"))
            && Directory.Exists(Path.Combine(game, GameLocator.DataFolderName)),
            "full uninstall returns the game folder to stock");
    }

    private static void TestZipSlip(Action<bool, string> check, string temp)
    {
        var game = Path.Combine(temp, "Game2");
        Directory.CreateDirectory(Path.Combine(game, GameLocator.DataFolderName));
        var payload = Path.Combine(temp, "bad-payload");
        Directory.CreateDirectory(payload);
        MakeZip(Path.Combine(payload, Payload.ModZip), ("../../escaped.txt", "nope"));
        bool threw = false;
        try { new Setup(game, Payload.FromFolder(payload), null).Install(); }
        catch (InvalidOperationException) { threw = true; }
        check(threw && !File.Exists(Path.Combine(temp, "escaped.txt")), "installer refuses zip entries that escape the target folder");
    }

    private static void TestLoader(Action<bool, string> check, string temp)
    {
        var game = Path.Combine(temp, "Game3");
        Directory.CreateDirectory(Path.Combine(game, GameLocator.DataFolderName));
        // Minimal PE header: "MZ", e_lfanew at 0x3C pointing to "PE\0\0" + machine 0x8664 (x64).
        var exe = new byte[0x100];
        exe[0] = (byte)'M'; exe[1] = (byte)'Z';
        BitConverter.GetBytes(0x80).CopyTo(exe, 0x3C);
        exe[0x80] = (byte)'P'; exe[0x81] = (byte)'E';
        BitConverter.GetBytes((ushort)0x8664).CopyTo(exe, 0x84);
        File.WriteAllBytes(Path.Combine(game, GameLocator.ExeName), exe);

        var payload = Path.Combine(temp, "payload3");
        Directory.CreateDirectory(payload);
        MakeZip(Path.Combine(payload, Payload.BepInExZip),
            ("winhttp.dll", "loader"), ("doorstop_config.ini", "[General]\nenabled = true\ntarget_assembly = BepInEx\\core\\BepInEx.Preloader.dll\n"),
            ("BepInEx/core/BepInEx.dll", "core"), ("BepInEx/core/BepInEx.Preloader.dll", "pre"));
        MakeZip(Path.Combine(payload, Payload.ModZip), ("Hotline.dll", "mod"));
        File.WriteAllText(Path.Combine(payload, Payload.VersionFile), "1.0.0");

        var setup = new Setup(game, Payload.FromFolder(payload), null);
        setup.Install();
        check(setup.ActiveLoader == Setup.DefaultLoader && setup.GameArchitecture() == "x64", "fresh install uses winhttp.dll and reads the game's architecture");
        var checks = setup.CheckLoader();
        check(checks.Count(c => !c.Ok) == 1 && checks.Any(c => !c.Ok && c.Message.Contains("never started")),
            "loader checks pass on a clean install (only 'never started' is flagged)");

        setup.SwitchLoader(Setup.AlternateLoader);
        check(setup.ActiveLoader == Setup.AlternateLoader && !File.Exists(Path.Combine(game, "winhttp.dll")) && setup.BepInExInstalled,
            "switching renames the loader to version.dll and BepInEx still counts as installed");

        setup.Install(repairBepInEx: true);
        check(setup.ActiveLoader == Setup.AlternateLoader && !File.Exists(Path.Combine(game, "winhttp.dll")),
            "repair keeps the loader under version.dll instead of adding a second copy");

        Directory.CreateDirectory(Path.Combine(game, GameLocator.DataFolderName, "Managed"));
        File.WriteAllText(Path.Combine(game, GameLocator.DataFolderName, "Managed", "Assembly-CSharp.dll"), "game");
        check(setup.CheckLoader().Any(c => c.Ok && c.Message.Contains("Mono build")), "loader checks recognise a Mono build");
        File.WriteAllText(Path.Combine(game, "GameAssembly.dll"), "il2cpp");
        check(setup.CheckLoader().Any(c => !c.Ok && c.Message.Contains("IL2CPP")), "loader checks flag an IL2CPP build");
        File.Delete(Path.Combine(game, "GameAssembly.dll"));
        Directory.CreateDirectory(Path.Combine(game, "EasyAntiCheat"));
        check(setup.CheckLoader().Any(c => !c.Ok && c.Message.Contains("Easy Anti-Cheat")), "loader checks flag anti-cheat");
        Directory.Delete(Path.Combine(game, "EasyAntiCheat"));

        File.WriteAllText(Path.Combine(game, "winmm.dll"), "other");
        check(setup.CheckLoader().Any(c => !c.Ok && c.Message.Contains("winmm.dll")), "loader checks flag another mod loader's DLL");
        File.Delete(Path.Combine(game, "winmm.dll"));

        File.Delete(Path.Combine(game, "version.dll"));
        check(setup.ActiveLoader == null && setup.CheckLoader().Any(c => !c.Ok && c.Message.Contains("missing")),
            "a loader removed by antivirus is reported as missing");
        setup.Install(repairBepInEx: true);

        setup.SwitchLoader(Setup.DefaultLoader);
        File.WriteAllText(Path.Combine(game, "version.dll"), "someone else's");
        bool refused = false;
        try { setup.SwitchLoader(Setup.AlternateLoader); } catch (InvalidOperationException) { refused = true; }
        check(refused && File.ReadAllText(Path.Combine(game, "version.dll")) == "someone else's", "won't overwrite another tool's version.dll");

        setup.UninstallEverything();
        check(!File.Exists(Path.Combine(game, "winhttp.dll")) && File.Exists(Path.Combine(game, "version.dll")),
            "full uninstall removes our loader but leaves another tool's version.dll");
    }

    private static void MakeZip(string path, params (string name, string content)[] entries)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(content);
        }
    }
}
