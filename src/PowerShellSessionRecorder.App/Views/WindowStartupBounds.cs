namespace PowerShellSessionRecorder.App.Views;

/// <summary>
/// Initial window size in device-independent pixels, clamped to the monitor working area.
/// </summary>
public static class WindowStartupBounds
{
    public const double PreferredWidth = 960;

    public const double PreferredHeight = 640;

    public const double FloorWidth = 640;

    public const double FloorHeight = 480;

    public const double Margin = 24;

    public readonly record struct FitResult(double Width, double Height, double MinWidth, double MinHeight);

    public static FitResult Fit(double workingWidth, double workingHeight)
    {
        if (workingWidth <= 0 || workingHeight <= 0 || double.IsNaN(workingWidth) || double.IsNaN(workingHeight))
            return new FitResult(PreferredWidth, PreferredHeight, FloorWidth, FloorHeight);

        var availableWidth = Math.Max(1, workingWidth - Margin);
        var availableHeight = Math.Max(1, workingHeight - Margin);
        return new FitResult(
            Math.Min(PreferredWidth, availableWidth),
            Math.Min(PreferredHeight, availableHeight),
            Math.Min(FloorWidth, availableWidth),
            Math.Min(FloorHeight, availableHeight));
    }

    public static (double Width, double Height) WorkingAreaDips(double pixelWidth, double pixelHeight, double scaling)
    {
        var scale = scaling <= 0 || double.IsNaN(scaling) ? 1 : scaling;
        return (pixelWidth / scale, pixelHeight / scale);
    }
}
