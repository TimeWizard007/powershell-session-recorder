using System.Text.Json;
using System.Text.RegularExpressions;

namespace PowerShellSessionRecorder.Tests;

public class LocalizationCatalogTests
{
    private static readonly string[] PolishCharacters = ["ą", "ć", "ę", "ł", "ń", "ó", "ś", "ź", "ż"];

    [Fact]
    public void Catalogs_HaveTheSameKeysAsTheCodeConstants()
    {
        var root = RepoRoot();
        var constants = ReadConstants(Path.Combine(root, "src/PowerShellSessionRecorder.App/Localization/L.cs"));
        var english = ReadCatalog(Path.Combine(root, "src/PowerShellSessionRecorder.App/Localization/en-US.json"));
        var polish = ReadCatalog(Path.Combine(root, "src/PowerShellSessionRecorder.App/Localization/pl-PL.json"));

        Assert.Equal(constants.OrderBy(key => key), english.Keys.OrderBy(key => key));
        Assert.Equal(english.Keys.OrderBy(key => key), polish.Keys.OrderBy(key => key));
        Assert.All(english, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value)));
        Assert.All(polish, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value)));
    }

    [Fact]
    public void Catalogs_AreReferencedByTheInterface()
    {
        var root = RepoRoot();
        var constants = ReadConstants(Path.Combine(root, "src/PowerShellSessionRecorder.App/Localization/L.cs"));
        var sources = Directory.EnumerateFiles(Path.Combine(root, "src/PowerShellSessionRecorder.App"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".axaml", StringComparison.Ordinal))
            .Where(path => !path.EndsWith($"{Path.DirectorySeparatorChar}L.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText);
        var combined = string.Join('\n', sources);

        foreach (var key in constants)
        {
            Assert.True(
                Regex.IsMatch(combined, $@"(?:\{{loc:Tr {Regex.Escape(key)}\}}|\bL\.{Regex.Escape(key)}\b)"),
                $"Localization key '{key}' is not used.");
        }
    }

    [Fact]
    public void PolishCatalog_UsesPolishCharactersAndTranslatesTheInterface()
    {
        var root = RepoRoot();
        var polishPath = Path.Combine(root, "src/PowerShellSessionRecorder.App/Localization/pl-PL.json");
        var polishText = File.ReadAllText(polishPath);
        var english = ReadCatalog(Path.Combine(root, "src/PowerShellSessionRecorder.App/Localization/en-US.json"));
        var polish = ReadCatalog(polishPath);

        foreach (var character in PolishCharacters)
            Assert.Contains(character, polishText, StringComparison.Ordinal);

        foreach (var key in new[] { "NavHistory", "NavSettings", "FieldSessionName", "ButtonBrowse", "ValidationCustomerTagRequired", "HistoryEmptyTitle" })
            Assert.NotEqual(english[key], polish[key]);
    }

    private static IReadOnlyList<string> ReadConstants(string path)
    {
        var matches = Regex.Matches(File.ReadAllText(path), "public const string \\w+ = \"([A-Za-z0-9]+)\";");
        return matches.Select(match => match.Groups[1].Value).ToArray();
    }

    private static Dictionary<string, string> ReadCatalog(string path)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Could not read {path}.");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PowerShellSessionRecorder.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
