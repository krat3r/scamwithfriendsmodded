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
            TestCorlibSetup(check, temp);
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
        var game = MakeGame(temp, "Game");
        var payload = MakePayload(temp, "payload", "9.9.9", ("Hotline.dll", "mod"), ("ScamPacks/a.json", "{}"));

        var setup = new Setup(game, Payload.FromFolder(payload), null);
        var problems = setup.Install();
        check(problems.Count == 0 && setup.BepInExInstalled && setup.ModInstalled && setup.InstalledVersion == "9.9.9"
            && File.Exists(Path.Combine(setup.PluginDir, "ScamPacks", "a.json")),
            "install sets up BepInEx, passes its own verification, and puts the mod in BepInEx/plugins");

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
            "[Message:   BepInEx] BepInEx 5.4.23.5 - Scam With Your Friends",
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
            && !Directory.Exists(setup.OverrideDir) && Directory.Exists(Path.Combine(game, GameLocator.DataFolderName)),
            "full uninstall returns the game folder to stock");
    }

    /// <summary>The stripped-corlib fix: what the community setup adds, and how the installer keeps it current.</summary>
    private static void TestCorlibSetup(Action<bool, string> check, string temp)
    {
        var game = MakeGame(temp, "GameCorlib");
        var payload = MakePayload(temp, "payloadCorlib", "1.0.0", ("Hotline.dll", "mod"));
        var setup = new Setup(game, Payload.FromFolder(payload), null);
        setup.Install();

        var seeded = ScamWYF.Setup.SetupPayload.CorlibAssemblyNames.All(n => File.Exists(Path.Combine(setup.OverrideDir, n)));
        var ini = File.ReadAllText(Path.Combine(game, "doorstop_config.ini"));
        check(seeded && ini.Contains("dll_search_path_override=unstripped_corlib") && File.Exists(setup.SetupLogPath),
            "install seeds the 8 full .NET libraries and points the loader at them (details in hotline-setup.log)");

        // A game set up by the previous Hotline version: plain BepInEx config, no override folder.
        File.WriteAllText(Path.Combine(game, "doorstop_config.ini"), "[General]\nenabled = true\ntarget_assembly=BepInEx\\core\\BepInEx.Preloader.dll\n");
        Directory.Delete(setup.OverrideDir, true);
        var checks = setup.CheckLoader();
        check(!setup.BepInExInstalled && checks.Any(c => !c.Ok && c.Message.Contains("unstripped_corlib"))
            && checks.Any(c => !c.Ok && c.Message.Contains("isn't set to use")),
            "an install without the library override counts as not set up, and the checks say why");
        setup.Install();
        check(setup.BepInExInstalled, "install fixes an old setup that lacks the override");

        // After a game update: a patched copy older than the game's file would shadow it.
        var copy = Path.Combine(setup.OverrideDir, "GameCode.dll");
        var original = Path.Combine(setup.ManagedDir, "GameCode.dll");
        File.WriteAllText(copy, "old patched copy");
        File.WriteAllText(original, "updated game file");
        File.SetLastWriteTimeUtc(copy, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(original, DateTime.UtcNow.AddDays(-1));
        check(setup.StaleOverrides().SequenceEqual(new[] { "GameCode.dll" })
            && setup.CheckLoader().Any(c => !c.Ok && c.Message.Contains("game was updated")),
            "a patched copy older than the game's file is flagged as needing Repair");
        File.Delete(original);
        setup.Install();
        check(!File.Exists(copy), "install rebuilds the override folder from scratch, dropping stale copies");

        var notMono = MakeGame(temp, "GameNoManaged");
        File.Delete(Path.Combine(notMono, GameLocator.DataFolderName, "Managed", "mscorlib.dll"));
        string message = null;
        try { new Setup(notMono, Payload.FromFolder(payload), null).Install(); }
        catch (InvalidOperationException e) { message = e.Message; }
        check(message != null && message.Contains("newer Hotline release") && !Directory.Exists(Path.Combine(notMono, "BepInEx")),
            "install stops before touching a game without the Mono files, with a plain message");
    }

    private static void TestZipSlip(Action<bool, string> check, string temp)
    {
        var game = MakeGame(temp, "Game2");
        var payload = MakePayload(temp, "bad-payload", "1.0.0", ("../../escaped.txt", "nope"));
        bool threw = false;
        try { new Setup(game, Payload.FromFolder(payload), null).Install(); }
        catch (InvalidOperationException e) { threw = e.Message.Contains("Unsafe path"); }
        check(threw && !File.Exists(Path.Combine(temp, "escaped.txt")), "installer refuses zip entries that escape the target folder");
    }

    private static void TestLoader(Action<bool, string> check, string temp)
    {
        var game = MakeGame(temp, "Game3");
        // Minimal PE header: "MZ", e_lfanew at 0x3C pointing to "PE\0\0" + machine 0x8664 (x64).
        var exe = new byte[0x100];
        exe[0] = (byte)'M'; exe[1] = (byte)'Z';
        BitConverter.GetBytes(0x80).CopyTo(exe, 0x3C);
        exe[0x80] = (byte)'P'; exe[0x81] = (byte)'E';
        BitConverter.GetBytes((ushort)0x8664).CopyTo(exe, 0x84);
        File.WriteAllBytes(Path.Combine(game, GameLocator.ExeName), exe);

        var payload = MakePayload(temp, "payload3", "1.0.0", ("Hotline.dll", "mod"));
        var setup = new Setup(game, Payload.FromFolder(payload), null);
        setup.Install();
        check(setup.ActiveLoader == Setup.DefaultLoader && setup.GameArchitecture() == "x64", "fresh install uses winhttp.dll and reads the game's architecture");
        var checks = setup.CheckLoader();
        check(checks.Count(c => !c.Ok) == 1 && checks.Any(c => !c.Ok && c.Message.Contains("never started")),
            "loader checks pass on a clean install (only 'never started' is flagged)");

        setup.SwitchLoader(Setup.AlternateLoader);
        check(setup.ActiveLoader == Setup.AlternateLoader && !File.Exists(Path.Combine(game, "winhttp.dll")) && setup.BepInExInstalled,
            "switching renames the loader to version.dll and BepInEx still counts as installed");

        setup.Install();
        check(setup.ActiveLoader == Setup.AlternateLoader && !File.Exists(Path.Combine(game, "winhttp.dll")),
            "repair keeps the loader under version.dll instead of adding a second copy");

        File.WriteAllText(Path.Combine(game, GameLocator.DataFolderName, "Managed", "Assembly-CSharp.dll"), "game");
        check(setup.CheckLoader().Any(c => c.Ok && c.Message.Contains("Mono build")), "loader checks recognise a Mono build");
        File.WriteAllText(Path.Combine(game, "GameAssembly.dll"), "il2cpp");
        check(setup.CheckLoader().Any(c => !c.Ok && c.Blocker && c.Message.Contains("IL2CPP")), "loader checks flag an IL2CPP build as a blocker");
        File.Delete(Path.Combine(game, "GameAssembly.dll"));
        Directory.CreateDirectory(Path.Combine(game, "EasyAntiCheat"));
        check(setup.CheckLoader().Any(c => !c.Ok && c.Blocker && c.Message.Contains("Easy Anti-Cheat")), "loader checks flag anti-cheat as a blocker");
        Directory.Delete(Path.Combine(game, "EasyAntiCheat"));

        File.WriteAllText(Path.Combine(game, "winmm.dll"), "other");
        check(setup.CheckLoader().Any(c => !c.Ok && c.Message.Contains("winmm.dll")), "loader checks flag another mod loader's DLL");
        File.Delete(Path.Combine(game, "winmm.dll"));

        File.Delete(Path.Combine(game, "version.dll"));
        check(setup.ActiveLoader == null && setup.CheckLoader().Any(c => !c.Ok && c.Message.Contains("missing")),
            "a loader removed by antivirus is reported as missing");
        setup.Install();
        check(setup.ActiveLoader == Setup.AlternateLoader, "repair restores a missing loader");

        setup.SwitchLoader(Setup.DefaultLoader);
        File.WriteAllText(Path.Combine(game, "version.dll"), "someone else's");
        bool refused = false;
        try { setup.SwitchLoader(Setup.AlternateLoader); } catch (InvalidOperationException) { refused = true; }
        check(refused && File.ReadAllText(Path.Combine(game, "version.dll")) == "someone else's", "won't overwrite another tool's version.dll");

        setup.UninstallEverything();
        check(!File.Exists(Path.Combine(game, "winhttp.dll")) && File.Exists(Path.Combine(game, "version.dll")),
            "full uninstall removes our loader but leaves another tool's version.dll");
    }

    /// <summary>A fake game folder whose Managed/mscorlib.dll is a real assembly (the patchers read it).</summary>
    private static string MakeGame(string temp, string name)
    {
        var game = Path.Combine(temp, name);
        var managed = Path.Combine(game, GameLocator.DataFolderName, "Managed");
        Directory.CreateDirectory(managed);
        using (var source = ScamWYF.Setup.SetupPayload.Embedded().OpenCorlib("mscorlib.dll"))
        using (var file = File.Create(Path.Combine(managed, "mscorlib.dll")))
            source.CopyTo(file);
        return game;
    }

    private static string MakePayload(string temp, string name, string version, params (string name, string content)[] modEntries)
    {
        var payload = Path.Combine(temp, name);
        Directory.CreateDirectory(payload);
        MakeZip(Path.Combine(payload, Payload.BepInExZip),
            ("winhttp.dll", "bepinex's own loader"), ("BepInEx/core/BepInEx.dll", "core"), ("BepInEx/core/BepInEx.Preloader.dll", "pre"));
        MakeZip(Path.Combine(payload, Payload.ModZip), modEntries);
        File.WriteAllText(Path.Combine(payload, Payload.VersionFile), version);
        return payload;
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
