using System;
using System.Drawing;
using System.Reflection;

namespace BleWorkbench.Core
{
    /// <summary>Central product branding for Beacon.</summary>
    public static class Branding
    {
        public const string AppName = "Beacon";
        public const string Tagline = "Bluetooth Low Energy Utility";
        public const string AppTitle = "Beacon  —  Bluetooth Low Energy Utility";
        public const string Author = "Jakka351";
        public const string GitHubUrl = "https://github.com/jakka351/beacon";

        public static string Version
        {
            get
            {
                try { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
                catch { return "1.0.0.0"; }
            }
        }

        /// <summary>Loads the embedded multi-resolution application icon (or null).</summary>
        public static Icon LoadIcon()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("BleWorkbench.beacon.ico"))
                    if (s != null) return new Icon(s);
            }
            catch { }
            return null;
        }

        /// <summary>Loads the icon at a specific size for crisp display in the About box.</summary>
        public static Icon LoadIcon(int size)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("BleWorkbench.beacon.ico"))
                    if (s != null) return new Icon(s, new Size(size, size));
            }
            catch { }
            return LoadIcon();
        }
    }
}
