using CodeKeys.App.UI;

namespace CodeKeys.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        // Milestone build: launch straight into the standalone test window (no global
        // hook, no tray yet) so the two audio layers and latency can be validated by ear.
        Application.Run(new TestWindow());
    }
}
