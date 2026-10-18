using Avalonia.Controls;
using Avalonia.Interactivity;
using PowerShellSessionRecorder.App.ViewModels;

namespace PowerShellSessionRecorder.App.Views;

public partial class SessionSetupView : UserControl
{
    public SessionSetupView()
    {
        InitializeComponent();
    }

    private void OnCustomerTagLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SessionSetupViewModel viewModel)
            viewModel.RememberCustomerTag();
    }
}
