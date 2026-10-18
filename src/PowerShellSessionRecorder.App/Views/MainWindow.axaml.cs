using Avalonia.Controls;
using PowerShellSessionRecorder.App.Services;
using PowerShellSessionRecorder.App.ViewModels;

namespace PowerShellSessionRecorder.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.Session.AttachFolderPicker(new AvaloniaFolderPicker(this));
    }
}
