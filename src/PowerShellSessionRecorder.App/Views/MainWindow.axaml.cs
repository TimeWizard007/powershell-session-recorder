using Avalonia;
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
        FitToWorkingArea();
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.Session.AttachFolderPicker(new AvaloniaFolderPicker(this));
    }

    private void FitToWorkingArea()
    {
        var screen = Screens?.ScreenFromWindow(this) ?? Screens?.Primary;
        if (screen is null)
            return;

        var working = WindowStartupBounds.WorkingAreaDips(screen.WorkingArea.Width, screen.WorkingArea.Height, screen.Scaling);
        var fit = WindowStartupBounds.Fit(working.Width, working.Height);
        MinWidth = fit.MinWidth;
        MinHeight = fit.MinHeight;
        Width = fit.Width;
        Height = fit.Height;

        var scale = screen.Scaling <= 0 ? 1 : screen.Scaling;
        var widthPixels = (int)Math.Round(fit.Width * scale);
        var heightPixels = (int)Math.Round(fit.Height * scale);
        var area = screen.WorkingArea;
        Position = new PixelPoint(
            area.X + Math.Max(0, (area.Width - widthPixels) / 2),
            area.Y + Math.Max(0, (area.Height - heightPixels) / 2));
    }
}
