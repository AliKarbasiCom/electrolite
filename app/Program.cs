namespace Electrolite;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    static void Main()
    {
        // ── Single-Instance Guard ──────────────────────────────────────
        const string mutexName = @"Global\ElectroliteMutex";
        _mutex = new Mutex(initiallyOwned: true, mutexName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running — exit silently
            MessageBox.Show(
                "Electrolite is already running in the system tray.",
                "Electrolite",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            using var trayApp = new ElectroliteTrayApp();
            Application.Run(trayApp);
        }
        finally
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
