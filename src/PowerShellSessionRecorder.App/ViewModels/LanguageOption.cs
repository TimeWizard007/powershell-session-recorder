using CommunityToolkit.Mvvm.ComponentModel;
using PowerShellSessionRecorder.App.Localization;

namespace PowerShellSessionRecorder.App.ViewModels;

public sealed class LanguageOption : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly string _nameKey;

    public LanguageOption(string code, string nameKey, LocalizationService localization)
    {
        Code = code;
        _nameKey = nameKey;
        _localization = localization;
    }

    public string Code { get; }

    public string DisplayName => _localization.Get(_nameKey);

    public void Refresh() => OnPropertyChanged(nameof(DisplayName));
}
