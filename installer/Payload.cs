using System.IO;
using System.Reflection;

namespace ScamWithFriendsModded.Installer
{
    /// <summary>
    /// The files the installer puts in place: BepInEx.zip and mod.zip, plus version.txt.
    /// Release builds embed them in the .exe; otherwise they're read from a payload folder
    /// next to the .exe (handy while developing).
    /// </summary>
    internal sealed class Payload
    {
        public const string BepInExZip = "BepInEx.zip";
        public const string ModZip = "mod.zip";
        public const string VersionFile = "version.txt";

        private readonly string folder;

        private Payload(string folder) => this.folder = folder;

        public static Payload Embedded() => new Payload(null);
        public static Payload FromFolder(string folder) => new Payload(folder);

        /// <summary>Embedded payload if the .exe has one, else the payload folder beside it.</summary>
        public static Payload Default()
        {
            var embedded = Embedded();
            if (embedded.Has(ModZip)) return embedded;
            return FromFolder(Path.Combine(System.AppContext.BaseDirectory, "payload"));
        }

        public bool Has(string name)
        {
            using var stream = Open(name);
            return stream != null;
        }

        public Stream Open(string name)
        {
            if (folder != null)
            {
                var path = Path.Combine(folder, name);
                return File.Exists(path) ? File.OpenRead(path) : null;
            }
            return Assembly.GetExecutingAssembly().GetManifestResourceStream("payload/" + name);
        }

        public string Version
        {
            get
            {
                using var stream = Open(VersionFile);
                if (stream == null) return "unknown";
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd().Trim();
            }
        }
    }
}
