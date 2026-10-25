using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Avalonia;
using Avalonia.Native;

namespace PowerShellSessionRecorder.App;

static class StartupSmoke
{
    internal const string Argument = "--smoke";
    internal const string Marker = "SMOKE_OK";

    static readonly string[] NativeLibraries =
    [
        "libcoreclr.dylib",
        "libhostfxr.dylib",
        "libhostpolicy.dylib",
        "libAvaloniaNative.dylib",
        "libSkiaSharp.dylib",
        "libHarfBuzzSharp.dylib",
    ];

    internal static bool IsActive { get; private set; }

    internal static bool IsRequested(string[] args)
        => args.Length == 1 && string.Equals(args[0], Argument, StringComparison.Ordinal);

    internal static int Run()
    {
        IsActive = true;
        try
        {
            var entry = Assembly.GetEntryAssembly()?.Location;
            var expected = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "PowerShellSessionRecorder.dll"));
            if (string.IsNullOrEmpty(entry)
                || !string.Equals(Path.GetFullPath(entry), expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The host did not resolve PowerShellSessionRecorder.dll beside the runtime configuration.");
            }

            RequireBundleFiles(AppContext.BaseDirectory);
            var native = LoadNativeLibraries(AppContext.BaseDirectory);
            var builder = HideDockIcon(Program.BuildAvaloniaApp()).SetupWithoutStarting();
            if (builder.Instance is null || builder.Instance.Styles.Count == 0)
                throw new InvalidOperationException("Avalonia initialized without application styles.");
            RequireSubsystem(builder.RenderingSubsystemName, "Skia", "rendering");
            RequireSubsystem(builder.TextShapingSubsystemName, "HarfBuzz", "text shaping");
            if (OperatingSystem.IsMacOS())
                RequireSubsystem(builder.WindowingSubsystemName, "Native", "windowing");

            Console.WriteLine(
                $"{Marker} dll=PowerShellSessionRecorder.dll runtimeconfig=net10.0 native={string.Join(',', native)} rendering={builder.RenderingSubsystemName} shaping={builder.TextShapingSubsystemName} windowing={builder.WindowingSubsystemName}");
            Console.Out.Flush();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"SMOKE_FAIL {exception.GetType().Name}: {exception.Message}");
            Console.Error.Flush();
            return 1;
        }
    }

    internal static void RequireBundleFiles(string baseDirectory)
    {
        var resources = Path.GetFullPath(baseDirectory);
        if (!string.Equals(Path.GetFileName(resources.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "Resources", StringComparison.Ordinal))
            throw new InvalidOperationException($"The application base directory is not Contents/Resources: {resources}");

        var macos = Path.GetFullPath(Path.Combine(resources, "..", "MacOS"));
        RequireFile(Path.Combine(macos, "PowerShellSessionRecorder"));
        RequireFile(Path.Combine(resources, "PowerShellSessionRecorder.dll"));
        var runtimeConfig = RequireFile(Path.Combine(resources, "PowerShellSessionRecorder.runtimeconfig.json"));
        RequireFile(Path.Combine(resources, "PowerShellSessionRecorder.deps.json"));
        RequireFile(Path.Combine(resources, "Localization", "en-US.json"));
        RequireFile(Path.Combine(resources, "Localization", "pl-PL.json"));
        var config = File.ReadAllText(runtimeConfig);
        if (!config.Contains("\"tfm\": \"net10.0\"", StringComparison.Ordinal))
            throw new InvalidOperationException("PowerShellSessionRecorder.runtimeconfig.json is not a net10.0 runtime configuration.");
    }

    static IReadOnlyList<string> LoadNativeLibraries(string baseDirectory)
    {
        var resolver = new AssemblyDependencyResolver(Path.Combine(baseDirectory, "PowerShellSessionRecorder.dll"));
        var loaded = new List<string>(NativeLibraries.Length);
        foreach (var library in NativeLibraries)
        {
            var path = resolver.ResolveUnmanagedDllToPath(library)
                ?? resolver.ResolveUnmanagedDllToPath(Path.GetFileNameWithoutExtension(library));
            if (string.IsNullOrEmpty(path))
                throw new DllNotFoundException($"The dependency manifest did not resolve {library}.");
            var fullPath = Path.GetFullPath(path);
            var expected = Path.GetFullPath(Path.Combine(baseDirectory, "..", "MacOS", library));
            if (!string.Equals(fullPath, expected, StringComparison.Ordinal))
                throw new InvalidOperationException($"{library} resolved to {fullPath} instead of Contents/MacOS.");
            NativeLibrary.Load(fullPath);
            loaded.Add(library);
        }

        return loaded;
    }

    static AppBuilder HideDockIcon(AppBuilder builder)
    {
        if (!OperatingSystem.IsMacOS())
            return builder;

        return builder.With(new MacOSPlatformOptions { ShowInDock = false });
    }

    static void RequireSubsystem(string? name, string expected, string kind)
    {
        if (name is null || name.IndexOf(expected, StringComparison.OrdinalIgnoreCase) < 0)
            throw new InvalidOperationException($"Avalonia {kind} subsystem was '{name ?? ""}', expected {expected}.");
    }

    static string RequireFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"The bundle is missing {path}.", path);
        return path;
    }
}
