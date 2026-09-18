using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WindowsMediaSwitcher.Services;

/// <summary>
/// Checks GitHub Releases for a newer build and applies WindowsMediaSwitcher-win-x64.zip
/// beside the running EXE via a short-lived updater script (unpackaged app).
/// </summary>
public sealed class UpdateService
{
    public const string RepoOwner = "ihatetheworld-ckl";
    public const string RepoName = "windows-media-switcher";
    public const string AssetName = "WindowsMediaSwitcher-win-x64.zip";
    public const string ApiLatest =
        "https://api.github.com/repos/ihatetheworld-ckl/windows-media-switcher/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    public string CurrentVersion { get; } = ReadCurrentVersion();

    public record UpdateCheckResult(
        bool UpdateAvailable,
        string CurrentVersion,
        string? LatestVersion,
        string? DownloadUrl,
        string? ReleaseNotes,
        string? Error);

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, ApiLatest);
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(false, CurrentVersion, null, null, null,
                    $"GitHub API {(int)resp.StatusCode}: {resp.ReasonPhrase}");
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var latest = NormalizeVersion(tag);
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;

            string? zipUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.Equals(name, AssetName, StringComparison.OrdinalIgnoreCase))
                    {
                        zipUrl = asset.TryGetProperty("browser_download_url", out var u)
                            ? u.GetString()
                            : null;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(latest))
                return new UpdateCheckResult(false, CurrentVersion, null, null, notes, "无法解析最新版本号");

            var available = IsNewer(latest, CurrentVersion);
            if (available && string.IsNullOrEmpty(zipUrl))
                return new UpdateCheckResult(true, CurrentVersion, latest, null, notes,
                    $"发布中未找到资源 {AssetName}");

            return new UpdateCheckResult(available, CurrentVersion, latest, zipUrl, notes, null);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, CurrentVersion, null, null, null, ex.Message);
        }
    }

    /// <summary>
    /// Downloads the zip, writes update.cmd next to the EXE, starts it, then the caller should Exit.
    /// </summary>
    public async Task ApplyUpdateAsync(string downloadUrl, CancellationToken ct = default)
    {
        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var tempRoot = Path.Combine(Path.GetTempPath(), "WindowsMediaSwitcher-update-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempRoot);
        var zipPath = Path.Combine(tempRoot, AssetName);
        var extractDir = Path.Combine(tempRoot, "extracted");

        using (var req = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
        using (var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            resp.EnsureSuccessStatusCode();
            await using var fs = File.Create(zipPath);
            await resp.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
        }

        Directory.CreateDirectory(extractDir);
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

        var exeName = "WindowsMediaSwitcher.exe";
        var pid = Environment.ProcessId;
        var scriptPath = Path.Combine(baseDir, "apply-update.cmd");

        // Wait for this process to exit, copy files, relaunch.
        var script = $"""
            @echo off
            setlocal
            echo Waiting for process {pid} to exit...
            :wait
            tasklist /FI "PID eq {pid}" 2>NUL | find "{pid}" >NUL
            if not errorlevel 1 (
              timeout /t 1 /nobreak >NUL
              goto wait
            )
            timeout /t 1 /nobreak >NUL
            echo Copying update files...
            xcopy /E /Y /Q "{extractDir}\*" "{baseDir}\"
            echo Cleaning temp...
            rmdir /S /Q "{tempRoot}" 2>NUL
            start "" "{baseDir}\{exeName}"
            del "%~f0"
            """;

        await File.WriteAllTextAsync(scriptPath, script, ct).ConfigureAwait(false);

        Process.Start(new ProcessStartInfo
        {
            FileName = scriptPath,
            WorkingDirectory = baseDir,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        // GitHub API requires a User-Agent
        c.DefaultRequestHeaders.UserAgent.ParseAdd("WindowsMediaSwitcher/0.2 (+https://github.com/ihatetheworld-ckl/windows-media-switcher)");
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    private static string ReadCurrentVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
            return NormalizeVersion(info.Split('+')[0]);

        var file = asm.GetName().Version;
        if (file is not null)
            return $"{file.Major}.{file.Minor}.{file.Build}";

        return "0.0.0";
    }

    public static string NormalizeVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "0.0.0";
        var s = raw.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
        var m = Regex.Match(s, @"\d+(?:\.\d+){0,3}");
        return m.Success ? m.Value : "0.0.0";
    }

    public static bool IsNewer(string latest, string current)
    {
        try
        {
            var l = Parse(latest);
            var c = Parse(current);
            return l > c;
        }
        catch
        {
            return false;
        }
    }

    private static Version Parse(string v)
    {
        var parts = NormalizeVersion(v).Split('.');
        int major = parts.Length > 0 && int.TryParse(parts[0], out var a) ? a : 0;
        int minor = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : 0;
        int build = parts.Length > 2 && int.TryParse(parts[2], out var c) ? c : 0;
        int rev = parts.Length > 3 && int.TryParse(parts[3], out var d) ? d : 0;
        return new Version(major, minor, build, rev);
    }
}
