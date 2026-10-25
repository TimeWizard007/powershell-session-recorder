using Avalonia;
using System;

namespace PowerShellSessionRecorder.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (StartupSmoke.IsRequested(args))
            Environment.Exit(StartupSmoke.Run());

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
