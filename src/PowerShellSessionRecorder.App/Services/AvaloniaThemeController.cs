using Avalonia;
using Avalonia.Styling;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.Services;

public interface IThemeController
{
    void Apply(AppTheme theme);
}

public sealed class AvaloniaThemeController : IThemeController
{
    public void Apply(AppTheme theme)
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
