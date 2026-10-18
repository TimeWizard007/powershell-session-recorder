using System.ComponentModel;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace PowerShellSessionRecorder.App.Localization;

public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var localization = LocalizationService.Current;
        if (localization is null || string.IsNullOrEmpty(Key))
            return Key;

        return new Binding(nameof(LocalizedValue.Text))
        {
            Source = new LocalizedValue(localization, Key),
            Mode = BindingMode.OneWay
        };
    }
}

public sealed class LocalizedValue : INotifyPropertyChanged
{
    private readonly LocalizationService _localization;
    private readonly string _key;

    public LocalizedValue(LocalizationService localization, string key)
    {
        _localization = localization;
        _key = key;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public string Text => _localization.Get(_key);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnLanguageChanged(object? sender, EventArgs e)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
}
