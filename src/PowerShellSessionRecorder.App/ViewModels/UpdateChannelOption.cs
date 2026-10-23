using CommunityToolkit.Mvvm.ComponentModel;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.App.ViewModels;

public sealed class UpdateChannelOption : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly string _nameKey;

    public UpdateChannelOption(UpdateChannel channel, string nameKey, LocalizationService localization)
    {
        Channel = channel;
        _nameKey = nameKey;
        _localization = localization;
    }

    public UpdateChannel Channel { get; }

    public string DisplayName => _localization.Get(_nameKey);

    public void Refresh() => OnPropertyChanged(nameof(DisplayName));
}
