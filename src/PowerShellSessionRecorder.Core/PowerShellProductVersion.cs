using System.Globalization;

namespace PowerShellSessionRecorder.Core;

/// <summary>
/// A version printed by $PSVersionTable.PSVersion. Three or four numeric components are accepted.
/// Prerelease labels are not an installed product version.
/// </summary>
public readonly record struct PowerShellProductVersion
{
    private PowerShellProductVersion(int major, int minor, int patch, int? revision, string text)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Revision = revision;
        Text = text;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public int? Revision { get; }

    public string Text { get; }

    public static bool TryParse(string? text, out PowerShellProductVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var line = text.Trim();
        var newline = line.IndexOfAny(['\r', '\n']);
        if (newline >= 0)
            line = line[..newline].Trim();

        if (line.Length == 0 || line.Contains('-') || line.Contains('+'))
            return false;

        var parts = line.Split('.');
        if (parts.Length is not (3 or 4))
            return false;

        if (!TryComponent(parts[0], out var major)
            || !TryComponent(parts[1], out var minor)
            || !TryComponent(parts[2], out var patch))
            return false;

        int? revision = null;
        if (parts.Length == 4)
        {
            if (!TryComponent(parts[3], out var parsedRevision))
                return false;

            revision = parsedRevision;
        }

        version = new PowerShellProductVersion(major, minor, patch, revision, line);
        return true;
    }

    private static bool TryComponent(string text, out int number)
    {
        number = 0;
        if (text.Length is 0 or > 9)
            return false;

        foreach (var character in text)
        {
            if (!char.IsDigit(character))
                return false;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }
}
