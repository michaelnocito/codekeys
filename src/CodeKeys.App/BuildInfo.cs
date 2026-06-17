namespace CodeKeys.App;

/// <summary>Visible build identity — shown in the test window now and the About dialog later.</summary>
public static class BuildInfo
{
    public const string Version = "1.3.2";

    /// <summary>Build stamp taken from the executable's last-write time (UTC).</summary>
    public static string BuildStamp
    {
        get
        {
            try
            {
                // Environment.ProcessPath points at the real on-disk exe even for a
                // single-file app (Assembly.Location is empty there).
                string path = Environment.ProcessPath ?? "";
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm");
            }
            catch { /* fall through */ }
            return "unknown";
        }
    }

    public static string Full => $"Bowl Bass Keys v{Version} · build {BuildStamp}";
}
