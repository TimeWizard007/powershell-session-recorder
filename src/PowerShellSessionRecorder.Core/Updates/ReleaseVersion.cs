using System.Globalization;

namespace PowerShellSessionRecorder.Core.Updates;

/// <summary>
/// A Semantic Version 2.0.0 core and prerelease label.
/// Build metadata after <c>+</c> is ignored. A leading <c>v</c> is accepted.
/// </summary>
public readonly record struct ReleaseVersion
{
    private ReleaseVersion(int major, int minor, int patch, IReadOnlyList<PrereleaseId> prerelease, string original)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Prerelease = prerelease;
        Original = original;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public IReadOnlyList<PrereleaseId> Prerelease { get; }

    public string Original { get; }

    public bool IsStable => Prerelease.Count == 0;

    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var original = text.Trim();
        var value = original;
        if (value.Length > 1 && (value[0] is 'v' or 'V') && char.IsDigit(value[1]))
            value = value[1..];

        var plus = value.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
            value = value[..plus];

        if (value.Length == 0)
            return false;

        var dash = value.IndexOf('-', StringComparison.Ordinal);
        var core = dash >= 0 ? value[..dash] : value;
        var prereleaseText = dash >= 0 ? value[(dash + 1)..] : null;
        if (prereleaseText is { Length: 0 })
            return false;

        var parts = core.Split('.');
        if (parts.Length != 3
            || !TryComponent(parts[0], out var major)
            || !TryComponent(parts[1], out var minor)
            || !TryComponent(parts[2], out var patch))
            return false;

        var identifiers = new List<PrereleaseId>();
        if (prereleaseText is not null)
        {
            foreach (var token in prereleaseText.Split('.'))
            {
                if (!PrereleaseId.TryParse(token, out var identifier))
                    return false;

                identifiers.Add(identifier);
            }
        }

        version = new ReleaseVersion(major, minor, patch, identifiers, original);
        return true;
    }

    /// <summary>
    /// Semantic Version precedence. A stable release is newer than the same core with a prerelease label.
    /// </summary>
    public int CompareTo(ReleaseVersion other)
    {
        var compared = Major.CompareTo(other.Major);
        if (compared != 0)
            return compared;

        compared = Minor.CompareTo(other.Minor);
        if (compared != 0)
            return compared;

        compared = Patch.CompareTo(other.Patch);
        if (compared != 0)
            return compared;

        if (IsStable && other.IsStable)
            return 0;

        if (IsStable)
            return 1;

        if (other.IsStable)
            return -1;

        var shared = Math.Min(Prerelease.Count, other.Prerelease.Count);
        for (var index = 0; index < shared; index++)
        {
            compared = Prerelease[index].CompareTo(other.Prerelease[index]);
            if (compared != 0)
                return compared;
        }

        return Prerelease.Count.CompareTo(other.Prerelease.Count);
    }

    private static bool TryComponent(string text, out int number)
    {
        number = 0;
        if (text.Length is 0 or > 9)
            return false;

        if (text.Length > 1 && text[0] == '0')
            return false;

        foreach (var character in text)
        {
            if (!char.IsDigit(character))
                return false;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }
}

public readonly record struct PrereleaseId
{
    private PrereleaseId(bool numeric, int number, string text)
    {
        IsNumeric = numeric;
        Number = number;
        Text = text;
    }

    public bool IsNumeric { get; }

    public int Number { get; }

    public string Text { get; }

    public static bool TryParse(string text, out PrereleaseId identifier)
    {
        identifier = default;
        if (text.Length == 0)
            return false;

        foreach (var character in text)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '-')
                return false;
        }

        var numeric = true;
        foreach (var character in text)
        {
            if (!char.IsDigit(character))
            {
                numeric = false;
                break;
            }
        }

        if (!numeric)
        {
            identifier = new PrereleaseId(false, 0, text);
            return true;
        }

        if (text.Length > 1 && text[0] == '0')
            return false;

        if (text.Length > 9 || !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            return false;

        identifier = new PrereleaseId(true, number, text);
        return true;
    }

    public int CompareTo(PrereleaseId other)
    {
        if (IsNumeric && other.IsNumeric)
            return Number.CompareTo(other.Number);

        if (IsNumeric)
            return -1;

        if (other.IsNumeric)
            return 1;

        return string.CompareOrdinal(Text, other.Text);
    }
}
