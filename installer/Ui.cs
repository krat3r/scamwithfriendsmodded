using System;
using System.Runtime.InteropServices;

namespace Hotline.Installer
{
    /// <summary>
    /// Console styling: ANSI colour (enabled on modern Windows terminals and on Linux),
    /// a banner, and simple status lines. Falls back to plain text when colour is off.
    /// </summary>
    internal static class Ui
    {
        private const string Reset = "\x1b[0m";
        private const string Dim = "\x1b[90m";
        private const string Flame = "\x1b[38;5;208m";
        private const string FlameBold = "\x1b[1;38;5;208m";
        private const string Green = "\x1b[32m";
        private const string Red = "\x1b[31m";
        private const string Yellow = "\x1b[33m";
        private const string Cyan = "\x1b[36m";

        public static bool Color { get; private set; } = true;

        public static void Init()
        {
            try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
            Color = Environment.GetEnvironmentVariable("NO_COLOR") == null && !Console.IsOutputRedirected && EnableVirtualTerminal();
        }

        private static string C(string code, string text) => Color ? code + text + Reset : text;

        public static void Banner(string version)
        {
            // Only characters in Consolas, so the classic Windows 10 console shows no boxes.
            Console.WriteLine();
            Console.WriteLine("  " + C(FlameBold, "HOTLINE") + "  " + C(Dim, "a mod for Scam With Your Friends"));
            Console.WriteLine("  " + C(Dim, "setup · v" + version));
            Rule();
        }

        public static void Rule() => Console.WriteLine(C(Dim, new string('─', 56)));

        public static void Heading(string text) => Console.WriteLine(C(Cyan, text));

        public static void Field(string label, string value, string color = null)
        {
            string padded = (label + ":").PadRight(10);
            Console.WriteLine("  " + C(Dim, padded) + " " + (color != null ? C(color, value) : value));
        }

        public static void Ok(string text) => Console.WriteLine("  " + C(Green, "√ ") + text);
        public static void Warn(string text) => Console.WriteLine("  " + C(Yellow, "! ") + text);
        public static void Fail(string text) => Console.WriteLine("  " + C(Red, "× ") + text);
        public static void Info(string text) => Console.WriteLine("  " + text);
        public static void Muted(string text) => Console.WriteLine(C(Dim, "  " + text));

        public static void Option(string key, string text) =>
            Console.WriteLine("   " + C(FlameBold, key) + "  " + text);

        public static string StatusGood => Green;
        public static string StatusBad => Red;
        public static string StatusWarn => Yellow;

        public static string Prompt(string text)
        {
            Console.Write(Environment.NewLine + C(Flame, "» ") + text + " ");
            return (Console.ReadLine() ?? "").Trim();
        }

        private static bool EnableVirtualTerminal()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return true;
            try
            {
                var handle = GetStdHandle(-11); // STD_OUTPUT_HANDLE
                if (!GetConsoleMode(handle, out uint mode)) return false;
                return SetConsoleMode(handle, mode | 0x0004); // ENABLE_VIRTUAL_TERMINAL_PROCESSING
            }
            catch { return false; }
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll")] private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
        [DllImport("kernel32.dll")] private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}
