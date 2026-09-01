using System.Text.Json;

namespace LuajitDecompilerGui.Services;

public sealed class AppSettings
{
    public string OutputFolder { get; set; } = string.Empty;
    public bool PreserveDirectories { get; set; } = true;
    public bool ForceOverwrite { get; set; }
    public bool SilentAssertions { get; set; } = true;
    public bool IgnoreDebugInfo { get; set; }
    public bool MinimizeDiffs { get; set; }
    public bool UnrestrictedAscii { get; set; }
    public string ExtensionFilter { get; set; } = string.Empty;
    public int WindowWidth { get; set; } = 1360;
    public int WindowHeight { get; set; } = 860;
    public bool WindowMaximized { get; set; }
}

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public AppSettingsService()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsPath = Path.Combine(root, "Luajit-Decompiler-v2-GUI", "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppSettings();

            string json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string? directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
