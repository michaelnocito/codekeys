using System.Diagnostics;
using System.Runtime.InteropServices;
using CodeKeys.App.UI;

namespace CodeKeys.App;

internal static class Program
{
    // A machine-wide named Mutex is the standard way to enforce single-instance for a Windows app.
    // We keep a reference so it lives for the process lifetime (releasing the mutex = letting
    // another instance through, which is exactly what we want when this one exits).
    private static Mutex? _instanceMutex;

    [STAThread]
    private static void Main()
    {
        _instanceMutex = new Mutex(initiallyOwned: true, "CodeKeys.SingleInstance.v1", out bool createdNew);
        if (!createdNew)
        {
            // Another CodeKeys is already running — bring its window forward and exit quietly.
            BringExistingInstanceToFront();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainWindow());
    }

    private static void BringExistingInstanceToFront()
    {
        var current = Process.GetCurrentProcess();
        foreach (var other in Process.GetProcessesByName(current.ProcessName))
        {
            if (other.Id == current.Id) continue;
            var handle = other.MainWindowHandle;
            if (handle == IntPtr.Zero) continue;
            ShowWindow(handle, SW_RESTORE);
            SetForegroundWindow(handle);
            return;
        }
    }

    private const int SW_RESTORE = 9;
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
}
