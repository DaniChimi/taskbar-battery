using System;
using System.Diagnostics;
using System.IO;

namespace TaskbarBattery
{
    // Diagnostic log in %LOCALAPPDATA%\TaskbarBattery\log.txt. It records changes and errors only, so it stays small;
    // past MaxBytes the file is moved to log.old.txt and a new one starts. Logging never throws.
    static class Log
    {
        const long MaxBytes = 256 * 1024;

        static readonly object Gate = new object();

        public static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarBattery", "log.txt");

        public static void Write(string message)
        {
            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    var file = new FileInfo(FilePath);
                    if (file.Exists && file.Length > MaxBytes)
                    {
                        string old = Path.Combine(file.DirectoryName, "log.old.txt");
                        File.Delete(old);
                        File.Move(FilePath, old);
                    }
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Log write failed: " + ex.Message);
                }
            }
        }

        // Opens the log in the default text editor, creating it first if nothing was logged yet.
        public static void Open()
        {
            if (!File.Exists(FilePath))
                Write("Log opened.");
            try
            {
                Process.Start(new ProcessStartInfo(FilePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Write("Could not open the log: " + ex.Message);
            }
        }
    }
}
