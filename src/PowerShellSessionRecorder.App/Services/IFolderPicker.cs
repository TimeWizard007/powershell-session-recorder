using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace PowerShellSessionRecorder.App.Services;

public readonly record struct FolderPickResult(bool Selected, string? LocalPath)
{
    public bool IsNonLocal => Selected && string.IsNullOrWhiteSpace(LocalPath);
}

public interface IFolderPicker
{
    Task<FolderPickResult> PickFolderAsync(string title, string? initialDirectory);
}

public sealed class AvaloniaFolderPicker : IFolderPicker
{
    private readonly Window _window;

    public AvaloniaFolderPicker(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
    }

    public async Task<FolderPickResult> PickFolderAsync(string title, string? initialDirectory)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            try
            {
                options.SuggestedStartLocation = await _window.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        var folders = await _window.StorageProvider.OpenFolderPickerAsync(options);
        if (folders.Count == 0)
            return new FolderPickResult(Selected: false, LocalPath: null);

        return new FolderPickResult(Selected: true, folders[0].TryGetLocalPath());
    }
}
