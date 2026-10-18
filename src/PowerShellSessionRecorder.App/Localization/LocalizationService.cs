using System.Text.Json;

namespace PowerShellSessionRecorder.App.Localization;

public sealed class LocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _catalogs = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _current = new(StringComparer.Ordinal);

    public LocalizationService(string localizationDirectory)
    {
        LoadDirectory(localizationDirectory);
        SetLanguage("en-US");
    }

    public static LocalizationService? Current { get; private set; }

    public string Language { get; private set; } = "en-US";

    public event EventHandler? LanguageChanged;

    public static void SetCurrent(LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        Current = localization;
    }

    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "";

        if (_current.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;

        return key;
    }

    public void SetLanguage(string? language)
    {
        var canonical = Core.ApplicationSettings.CanonicalLanguage(language);
        if (!_catalogs.TryGetValue(canonical, out var catalog) && !_catalogs.TryGetValue("en-US", out catalog))
            catalog = new Dictionary<string, string>(StringComparer.Ordinal);

        _current = catalog;
        Language = _catalogs.ContainsKey(canonical) ? canonical : "en-US";
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LoadDirectory(string localizationDirectory)
    {
        if (!Directory.Exists(localizationDirectory))
            return;

        foreach (var file in Directory.EnumerateFiles(localizationDirectory, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (entries is null)
                    continue;

                var code = Path.GetFileNameWithoutExtension(file);
                _catalogs[code] = new Dictionary<string, string>(entries, StringComparer.Ordinal);
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                // Skip a damaged catalog. Missing strings are shown as their key.
            }
        }
    }
}
