using CommunityToolkit.Mvvm.ComponentModel;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.ViewModels;

public sealed class ThemeOption : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly string _nameKey;

    public ThemeOption(AppTheme theme, string nameKey, LocalizationService localization)
    {
        Theme = theme;
        _nameKey = nameKey;
        _localization = localization;
    }

    public AppTheme Theme { get; }

    public string DisplayName => _localization.Get(_nameKey);

    public void Refresh() => OnPropertyChanged(nameof(DisplayName));
}
