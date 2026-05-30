using CodeKeys.App.UI;

namespace CodeKeys.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Build step 4: control panel + system-wide keyboard hook. Tray comes later.
        Application.Run(new MainWindow());
    }
}
