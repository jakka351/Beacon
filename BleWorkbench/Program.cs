using System;
using System.Windows.Forms;
using BleWorkbench.Forms;

namespace BleWorkbench
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // High-DPI awareness is configured declaratively in App.config
            // (System.Windows.Forms.ApplicationConfigurationSection -> DpiAwareness = PerMonitorV2).
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (s, e) => ReportFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportFatal(e.ExceptionObject as Exception);

            Application.Run(new MainForm());
        }

        private static void ReportFatal(Exception ex)
        {
            if (ex == null) return;
            try
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Beacon - Unexpected Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
