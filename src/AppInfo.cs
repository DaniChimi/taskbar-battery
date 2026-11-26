using System;
using System.Diagnostics;
using System.Reflection;

[assembly: AssemblyTitle("TaskbarBattery")]
[assembly: AssemblyProduct("TaskbarBattery")]
[assembly: AssemblyDescription("Battery level of wireless devices in the Windows notification area")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Daniele Chiminelli")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0")]

namespace TaskbarBattery
{
    // Version, author and project links, shown in the About section of the settings.
    static class AppInfo
    {
        public const string Author = "Daniele Chiminelli";
        public const string RepositoryUrl = "https://github.com/DaniChimi/TaskbarBattery";

        public static string IssuesUrl
        {
            get { return RepositoryUrl + "/issues"; }
        }

        // The version as people read it, for example "0.1.0".
        public static string Version
        {
            get
            {
                var attributes = (AssemblyInformationalVersionAttribute[])typeof(AppInfo).Assembly
                    .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
                return attributes.Length > 0 ? attributes[0].InformationalVersion : typeof(AppInfo).Assembly.GetName().Version.ToString(3);
            }
        }

        // Opens a link in the default browser.
        public static void Open(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Write("Could not open " + url + ": " + ex.Message);
            }
        }
    }
}
