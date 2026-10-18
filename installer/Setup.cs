using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace ScamWithFriendsModded.Installer
{
    /// <summary>Install, update and uninstall steps against one game folder.</summary>
    internal sealed class Setup
    {
        public const string PluginFolderName = "ScamWithFriendsModded";
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
                if (name != PluginFolderName) result.Add(name);
            }
            return result;
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
            if (Directory.Exists(PluginDir)) Directory.Delete(PluginDir, recursive: true);
            Directory.CreateDirectory(PluginDir);
            Extract(Payload.ModZip, PluginDir);
            File.WriteAllText(Path.Combine(PluginDir, InstalledVersionFile), payload.Version);
            log($"Installed version {payload.Version}.");
        }

        /// <summary>Removes only this mod. BepInEx, other mods and your config stay.</summary>
        public void UninstallMod()
        {
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
