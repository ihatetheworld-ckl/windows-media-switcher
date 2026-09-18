using System.Text.Json;
using WindowsMediaSwitcher.Models;

namespace WindowsMediaSwitcher.Services;

/// <summary>
/// Persists settings to %LocalAppData%/WindowsMediaSwitcher/settings.json
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath;
    private AppSettings _settings = new();

    public event EventHandler? SettingsChanged;

    public AppSettings Current => _settings;

    public SettingsService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsMediaSwitcher");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            else
            {
                _settings = new AppSettings();
                Save();
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(_filePath, json);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // ignore disk errors; keep in-memory values
        }
    }

    public void Update(Action<AppSettings> mutator)
    {
        mutator(_settings);
        Save();
    }

    public void Replace(AppSettings settings)
    {
        _settings = settings;
        Save();
    }
}
