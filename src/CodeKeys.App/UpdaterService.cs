using System.Net.Http;
using System.Text.Json;

namespace CodeKeys.App;

public static class UpdaterService
{
    const string ApiUrl = "https://api.github.com/repos/michaelnocito/codekeys/releases/latest";
    const string AssetName = "CodeKeys.exe";

    public record ReleaseInfo(string Version, string DownloadUrl, string Notes);

    public static async Task<ReleaseInfo?> CheckAsync()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "CodeKeys-Updater");
        http.Timeout = TimeSpan.FromSeconds(10);

        var json = await http.GetStringAsync(ApiUrl);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var version = tag.TrimStart('v');

        if (!IsNewer(version, BuildInfo.Version))
            return null;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.Equals(AssetName, StringComparison.OrdinalIgnoreCase))
            {
                var url = asset.GetProperty("browser_download_url").GetString() ?? "";
                var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
                return new ReleaseInfo(version, url, notes);
            }
        }
        return null;
    }

    public static async Task DownloadAndApplyAsync(ReleaseInfo info, IProgress<int> progress)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("User-Agent", "CodeKeys-Updater");

        var response = await http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        var total = response.Content.Headers.ContentLength ?? -1L;

        var tmpPath = Path.Combine(Path.GetTempPath(), "CodeKeys_update.exe");
        using (var stream = await response.Content.ReadAsStreamAsync())
        using (var file = File.Create(tmpPath))
        {
            var buf = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await stream.ReadAsync(buf)) > 0)
            {
                await file.WriteAsync(buf.AsMemory(0, read));
                downloaded += read;
                if (total > 0) progress.Report((int)(downloaded * 100 / total));
            }
        }
        progress.Report(100);

        // Can't overwrite a running exe directly — write a small batch that waits, copies, relaunches.
        var exePath = Environment.ProcessPath!;
        var bat = Path.Combine(Path.GetTempPath(), "codekeys_update.bat");
        File.WriteAllText(bat, $"""
            @echo off
            timeout /t 2 /nobreak >nul
            copy /y "{tmpPath}" "{exePath}"
            start "" "{exePath}"
            del "%~f0"
            """);

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{bat}\"",
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        });

        Application.Exit();
    }

    // Compares semver strings; strips pre-release suffix (e.g. "-dev") from current.
    static bool IsNewer(string available, string current)
    {
        current = current.Split('-')[0];
        return Version.TryParse(available, out var a)
            && Version.TryParse(current, out var c)
            && a > c;
    }
}
