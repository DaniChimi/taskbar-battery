using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace TaskbarBattery
{
    static class Program
    {
        const string SingleInstanceMutex = @"Local\TaskbarBattery";

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Any(a => string.Equals(a, "--probe", StringComparison.OrdinalIgnoreCase)))
                return Probe.Run();

            bool createdNew;
            using (var mutex = new Mutex(true, SingleInstanceMutex, out createdNew))
            {
                // Another instance already owns the tray icon.
                if (!createdNew)
                    return 0;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var app = new TrayApp())
                {
                    Application.Run(app);
                }
            }
            return 0;
        }
    }
}
