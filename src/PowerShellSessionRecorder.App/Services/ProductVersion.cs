using System.Reflection;

namespace PowerShellSessionRecorder.App.Services;

public static class ProductVersion
{
    public static string Current { get; } = Read();

    private static string Read()
    {
        var assembly = typeof(ProductVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "";
    }
}
