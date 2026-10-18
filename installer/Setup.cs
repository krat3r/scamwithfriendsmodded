using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace Hotline.Installer
{
    /// <summary>Install, update and uninstall steps against one game folder.</summary>
    internal sealed class Setup
    {
        public const string PluginFolderName = "Hotline";
        /// <summary>Folder names earlier versions used; removed on install so two copies never load.</summary>
        private static readonly string[] LegacyPluginFolders = { "ScamWithFriendsModded" };
        private const string InstalledVersionFile = "installed-version.txt";

        // Files BepInEx 5 puts next to the game exe (the loader DLL is handled separately).
        private static readonly string[] LoaderFiles = { "doorstop_config.ini", ".doorstop_version", "changelog.txt" };

        // BepInEx's loader (UnityDoorstop 4) works under either name. Windows loads it from the
        // game folder in place of the system DLL. Not every game loads winhttp.dll; almost all load version.dll.
        public const string DefaultLoader = "winhttp.dll";
        public const string AlternateLoader = "version.dll";
        // Records which name we put the loader under, so we never delete someone else's version.dll.
        private const string LoaderMarker = "hotline-loader.txt";
        // DLL names other mod loaders use; any of these next to the game can mean a conflict.
        private static readonly string[] OtherProxyNames = { "version.dll", "winmm.dll", "dinput8.dll", "dxgi.dll" };

        public readonly string GameDir;
        private readonly Payload payload;
        private readonly Action<string> log;

        public Setup(string gameDir, Payload payload, Action<string> log)
        {
            GameDir = gameDir;
            this.payload = payload;
            this.log = log ?? (_ => { });
        }

        public string BepInExDir => Path.Combine(GameDir, "BepInEx");
        public string PluginDir => Path.Combine(BepInExDir, "plugins", PluginFolderName);
        public bool BepInExInstalled => File.Exists(Path.Combine(BepInExDir, "core", "BepInEx.dll")) && ActiveLoader != null;

        private string MarkerPath => Path.Combine(BepInExDir, LoaderMarker);

        /// <summary>The loader name we installed under, per our marker (defaults to winhttp.dll).</summary>
        private string ChosenLoader =>
            File.Exists(MarkerPath) && File.ReadAllText(MarkerPath).Trim() == AlternateLoader ? AlternateLoader : DefaultLoader;

        /// <summary>The BepInEx loader file actually next to the game, or null if it's missing (e.g. removed by antivirus).</summary>
        public string ActiveLoader => File.Exists(Path.Combine(GameDir, ChosenLoader)) ? ChosenLoader : null;

        /// <summary>
        /// Renames BepInEx's loader between winhttp.dll and version.dll. Refuses to overwrite a
        /// version.dll that isn't ours (another tool's loader).
        /// </summary>
        public void SwitchLoader(string target)
        {
            if (target != DefaultLoader && target != AlternateLoader) throw new ArgumentException("Unknown loader name: " + target);
            var current = ActiveLoader ?? throw new InvalidOperationException("BepInEx's loader file is missing. Pick Repair first (and check your antivirus).");
            if (current == target) { log($"Already using {target}."); return; }
            var targetPath = Path.Combine(GameDir, target);
            if (File.Exists(targetPath))
                throw new InvalidOperationException($"{target} already exists in the game folder and isn't from Hotline (another mod loader?). Remove it first.");
            File.Move(Path.Combine(GameDir, current), targetPath);
            File.WriteAllText(MarkerPath, target);
            log($"BepInEx's loader is now {target}. Start the game and check again.");
        }
        public bool ModInstalled => File.Exists(Path.Combine(PluginDir, PluginFolderName + ".dll"));

        public string InstalledVersion
        {
            get
            {
                var path = Path.Combine(PluginDir, InstalledVersionFile);
                return File.Exists(path) ? File.ReadAllText(path).Trim() : (ModInstalled ? "unknown" : null);
            }
        }

        /// <summary>Other mods in BepInEx/plugins, which a full uninstall would also break.</summary>
        public List<string> OtherPlugins()
        {
            var result = new List<string>();
            var plugins = Path.Combine(BepInExDir, "plugins");
            if (!Directory.Exists(plugins)) return result;
            foreach (var entry in Directory.EnumerateFileSystemEntries(plugins))
            {
                var name = Path.GetFileName(entry);
                if (name != PluginFolderName && Array.IndexOf(LegacyPluginFolders, name) < 0) result.Add(name);
            }
            return result;
        }

        private void RemoveLegacyFolders()
        {
            foreach (var name in LegacyPluginFolders)
            {
                var dir = Path.Combine(BepInExDir, "plugins", name);
                if (!Directory.Exists(dir)) continue;
                Directory.Delete(dir, recursive: true);
                log($"Removed the old \"{name}\" version of the mod.");
            }
        }

        public string LogPath => Path.Combine(BepInExDir, "LogOutput.log");

        /// <summary>What BepInEx's last log says: did BepInEx run, did Hotline load, and what went wrong.</summary>
        public sealed class Health
        {
            public bool LogFound;
            public DateTime LogTime;
            public bool BepInExRan;
            public bool HotlineLoaded;
            public string HotlineLine;
            public readonly List<string> Problems = new List<string>();
        }

        public Health CheckHealth()
        {
            var health = new Health();
            if (!File.Exists(LogPath)) return health;
            health.LogFound = true;
            health.LogTime = File.GetLastWriteTime(LogPath);

            string[] lines;
            // The game keeps the log open while running, so allow shared access.
            using (var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
                lines = reader.ReadToEnd().Split('\n');

            foreach (var raw in lines)
            {
                var line = raw.TrimEnd('\r');
                if (line.Contains("Chainloader") || line.Contains("BepInEx 5.")) health.BepInExRan = true;
                bool aboutUs = line.Contains("Hotline");
                if (aboutUs && line.Contains(" loaded")) { health.HotlineLoaded = true; health.HotlineLine = line.Trim(); }
                bool bad = line.StartsWith("[Error") || line.StartsWith("[Fatal")
                    || line.Contains("Could not load") || line.Contains("Skipping [");
                if (bad && (aboutUs || line.Contains("Could not load")) && health.Problems.Count < 8) health.Problems.Add(line.Trim());
            }
            return health;
        }

        /// <summary>One check on BepInEx's loader setup, for when BepInEx never starts.</summary>
        public sealed class LoaderCheck
        {
            public bool Ok;
            public string Message;
            public LoaderCheck(bool ok, string message) { Ok = ok; Message = message; }
        }

        /// <summary>
        /// Why BepInEx might not be starting: loader file missing, loader disabled, wrong
        /// architecture, or another mod loader in the way.
        /// </summary>
        public List<LoaderCheck> CheckLoader()
        {
            var checks = new List<LoaderCheck>();
            var loader = ActiveLoader;
            checks.Add(loader != null
                ? new LoaderCheck(true, $"Loader file present: {loader}")
                : new LoaderCheck(false, $"Loader file {ChosenLoader} is missing. Antivirus often removes it: allow it, then pick Repair."));

            var ini = Path.Combine(GameDir, "doorstop_config.ini");
            if (!File.Exists(ini)) checks.Add(new LoaderCheck(false, "doorstop_config.ini is missing. Pick Repair."));
            else
            {
                string enabled = null, target = null;
                foreach (var raw in File.ReadAllLines(ini))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    var key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    var value = line.Substring(eq + 1).Trim();
                    if (key == "enabled") enabled = value.ToLowerInvariant();
                    if (key == "target_assembly" || key == "targetassembly") target = value;
                }
                checks.Add(enabled == "false"
                    ? new LoaderCheck(false, "The loader is switched off (enabled = false in doorstop_config.ini). Pick Repair.")
                    : new LoaderCheck(true, "Loader is enabled in doorstop_config.ini."));
                if (target != null)
                {
                    var targetPath = Path.Combine(GameDir, target.Replace('\\', Path.DirectorySeparatorChar));
                    checks.Add(File.Exists(targetPath)
                        ? new LoaderCheck(true, "BepInEx preloader found.")
                        : new LoaderCheck(false, $"BepInEx preloader missing ({target}). Pick Repair."));
                }
            }

            var arch = GameArchitecture();
            if (arch == "x86") checks.Add(new LoaderCheck(false, "The game is 32-bit but this BepInEx is 64-bit. Please report this."));
            else if (arch == "x64") checks.Add(new LoaderCheck(true, "Game is 64-bit, matching BepInEx."));

            foreach (var name in OtherProxyNames)
            {
                if (name == loader) continue;
                if (File.Exists(Path.Combine(GameDir, name)))
                    checks.Add(new LoaderCheck(false, $"Found {name} next to the game, probably another mod loader. Two loaders can block each other."));
            }
            if (Directory.Exists(Path.Combine(GameDir, "MelonLoader")))
                checks.Add(new LoaderCheck(false, "MelonLoader is installed. It and BepInEx usually can't run together."));

            checks.Add(File.Exists(Path.Combine(BepInExDir, "config", "BepInEx.cfg"))
                ? new LoaderCheck(true, "BepInEx has started at least once before (its config exists).")
                : new LoaderCheck(false, "BepInEx has never started in this folder (no BepInEx.cfg yet)."));
            return checks;
        }

        /// <summary>"x64", "x86", or null if the game exe can't be read.</summary>
        public string GameArchitecture()
        {
            var exe = Path.Combine(GameDir, GameLocator.ExeName);
            if (!File.Exists(exe)) return null;
            try
            {
                using var reader = new BinaryReader(File.OpenRead(exe));
                reader.BaseStream.Seek(0x3C, SeekOrigin.Begin);
                reader.BaseStream.Seek(reader.ReadInt32() + 4, SeekOrigin.Begin);
                ushort machine = reader.ReadUInt16();
                return machine == 0x8664 ? "x64" : machine == 0x14C ? "x86" : null;
            }
            catch { return null; }
        }

        public static bool GameRunning()
        {
            try { return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(GameLocator.ExeName)).Length > 0; }
            catch { return false; }
        }

        /// <summary>Installs BepInEx if it's missing (or <paramref name="repairBepInEx"/>), then installs or replaces the mod.</summary>
        public void Install(bool repairBepInEx = false)
        {
            if (!payload.Has(Payload.ModZip)) throw new InvalidOperationException("This installer has no mod files in it. Download the release .exe again.");

            if (!BepInExInstalled || repairBepInEx)
            {
                if (!payload.Has(Payload.BepInExZip)) throw new InvalidOperationException("BepInEx isn't installed and this installer doesn't include it.");
                log("Installing BepInEx 5...");
                bool useAlternate = ChosenLoader == AlternateLoader;
                Extract(Payload.BepInExZip, GameDir);
                // The zip ships winhttp.dll; keep the loader under the name the player switched to.
                if (useAlternate && File.Exists(Path.Combine(GameDir, DefaultLoader)))
                {
                    File.Copy(Path.Combine(GameDir, DefaultLoader), Path.Combine(GameDir, AlternateLoader), overwrite: true);
                    File.Delete(Path.Combine(GameDir, DefaultLoader));
                }
            }
            else log("BepInEx is already installed.");

            log("Installing the mod...");
            RemoveLegacyFolders();
            if (Directory.Exists(PluginDir)) Directory.Delete(PluginDir, recursive: true);
            Directory.CreateDirectory(PluginDir);
            Extract(Payload.ModZip, PluginDir);
            File.WriteAllText(Path.Combine(PluginDir, InstalledVersionFile), payload.Version);
            log($"Installed version {payload.Version}.");
        }

        /// <summary>Removes only this mod. BepInEx, other mods and your config stay.</summary>
        public void UninstallMod()
        {
            RemoveLegacyFolders();
            if (Directory.Exists(PluginDir))
            {
                Directory.Delete(PluginDir, recursive: true);
                log("Removed the mod.");
            }
            else log("The mod wasn't installed.");
        }

        /// <summary>Removes the mod, BepInEx and its loader files, returning the game to stock.</summary>
        public void UninstallEverything()
        {
            UninstallMod();
            // Only the loader name we used; a version.dll from another tool stays.
            var loader = Path.Combine(GameDir, ChosenLoader);
            if (File.Exists(loader)) File.Delete(loader);
            foreach (var file in LoaderFiles)
            {
                var path = Path.Combine(GameDir, file);
                if (File.Exists(path)) File.Delete(path);
            }
            if (Directory.Exists(BepInExDir)) Directory.Delete(BepInExDir, recursive: true);
            log("Removed BepInEx. The game is back to stock.");
        }

        private void Extract(string payloadName, string destination)
        {
            var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            using var stream = payload.Open(payloadName);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in zip.Entries)
            {
                var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                // Refuse entries that would land outside the destination folder.
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Unsafe path in {payloadName}: {entry.FullName}");
                if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                entry.ExtractToFile(target, overwrite: true);
            }
        }
    }
}
