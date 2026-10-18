using CommunityToolkit.Mvvm.ComponentModel;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.ViewModels;

public sealed class PowerShellChoice : ObservableObject
{
    private readonly LocalizationService _localization;

    public PowerShellChoice(PowerShellVersion version, LocalizationService localization)
    {
        Version = version;
        _localization = localization;
    }

    public PowerShellVersion Version { get; }

    public string DisplayName => _localization.Get(
        Version == PowerShellVersion.WindowsPowerShell51 ? L.WindowsPowerShell51 : L.PowerShell7);

    public void Refresh() => OnPropertyChanged(nameof(DisplayName));
}
