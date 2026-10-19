using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace PowerShellSessionRecorder.App.Services;

public interface ITextClipboard
{
    Task<bool> TrySetTextAsync(string text);
}

public sealed class AvaloniaTextClipboard : ITextClipboard
{
    public async Task<bool> TrySetTextAsync(string text)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return false;

            var clipboard = TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard;
            if (clipboard is null)
                return false;

            await clipboard.SetTextAsync(text);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or PlatformNotSupportedException)
        {
            return false;
        }
    }
}
