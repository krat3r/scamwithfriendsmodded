using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Hotline.Installer
{
    /// <summary>Finds the game's install folder through Steam's library list.</summary>
    internal static class GameLocator
    {
        public const string AppId = "4954910";
        public const string GameFolderName = "Scam With Your Friends";
        public const string ExeName = "Scam With Your Friends.exe";
        public const string DataFolderName = "Scam With Your Friends_Data";

        public static bool LooksLikeGame(string dir) =>
            !string.IsNullOrEmpty(dir) && Directory.Exists(dir)
            && (File.Exists(Path.Combine(dir, ExeName)) || Directory.Exists(Path.Combine(dir, DataFolderName)));

        /// <summary>Returns the game folder, or null. Extra Steam roots are checked first (used by tests).</summary>
        public static string Find(params string[] extraSteamRoots)
        {
            var roots = new List<string>(extraSteamRoots);
            roots.AddRange(SteamRoots());
            foreach (var root in roots)
                foreach (var library in Libraries(root))
                {
                    var game = FromLibrary(library);
                    if (game != null) return game;
                }
            return null;
        }

        private static IEnumerable<string> SteamRoots()
        {
            if (OperatingSystem.IsWindows())
            {
                foreach (var (hive, key, value) in new[]
                {
                    (Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
                    (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                    (Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
                })
                {
                    string path = null;
                    try { path = hive.OpenSubKey(key)?.GetValue(value) as string; } catch { }
                    if (!string.IsNullOrEmpty(path)) yield return path.Replace('/', '\\');
                }
                yield return @"C:\Program Files (x86)\Steam";
            }
            else
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                yield return Path.Combine(home, ".steam", "steam");
                yield return Path.Combine(home, ".local", "share", "Steam");
            }
        }

        /// <summary>Every Steam library folder listed under a Steam root, the root itself first.</summary>
        public static IEnumerable<string> Libraries(string steamRoot)
        {
            if (string.IsNullOrEmpty(steamRoot) || !Directory.Exists(steamRoot)) yield break;
            yield return steamRoot;
            var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) yield break;
            string text;
            try { text = File.ReadAllText(vdf); } catch { yield break; }
            foreach (var path in ParseLibraryPaths(text)) yield return path;
        }

        public static IEnumerable<string> ParseLibraryPaths(string vdf)
        {
            foreach (Match match in Regex.Matches(vdf, "\"path\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\""))
                yield return match.Groups[1].Value.Replace("\\\\", "\\");
        }

        private static string FromLibrary(string library)
        {
            var steamapps = Path.Combine(library, "steamapps");
            var manifest = Path.Combine(steamapps, $"appmanifest_{AppId}.acf");
            if (File.Exists(manifest))
            {
                try
                {
                    var match = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                    if (match.Success)
                    {
                        var dir = Path.Combine(steamapps, "common", match.Groups[1].Value);
                        if (LooksLikeGame(dir)) return dir;
                    }
                }
                catch { }
            }
            var fallback = Path.Combine(steamapps, "common", GameFolderName);
            return LooksLikeGame(fallback) ? fallback : null;
        }
    }
}
