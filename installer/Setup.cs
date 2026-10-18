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

        // Files BepInEx 5 puts next to the game exe.
        private static readonly string[] LoaderFiles = { "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt" };

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
        public bool BepInExInstalled => File.Exists(Path.Combine(BepInExDir, "core", "BepInEx.dll")) && File.Exists(Path.Combine(GameDir, "winhttp.dll"));
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

        /// <summary>Plain-language summary of <see cref="CheckHealth"/> for the installer screen.</summary>
        public List<string> DescribeHealth()
        {
            var health = CheckHealth();
            var lines = new List<string>();
            if (!health.LogFound)
            {
                lines.Add(BepInExInstalled
                    ? "BepInEx hasn't run yet. Start the game once from Steam, then check again."
                    : "BepInEx isn't installed yet.");
                if (!OperatingSystem.IsWindows())
                    lines.Add("On Steam Deck / Linux, BepInEx only runs with the launch option shown after installing.");
                return lines;
            }
            lines.Add($"BepInEx last ran {health.LogTime:g}.");
            lines.Add(health.HotlineLoaded ? "Hotline loaded: " + health.HotlineLine : "Hotline did NOT load in that run.");
            foreach (var problem in health.Problems) lines.Add("  " + problem);
            if (!health.HotlineLoaded && health.Problems.Count == 0)
                lines.Add("No error mentions Hotline. If you installed after that run, start the game again and recheck.");
            return lines;
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
                Extract(Payload.BepInExZip, GameDir);
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
