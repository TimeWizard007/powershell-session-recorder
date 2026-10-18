using System.Diagnostics;

namespace PowerShellSessionRecorder.App.Services;

public interface IExternalLinkOpener
{
    bool TryOpen(string url);
}

public sealed class ExternalLinkOpener : IExternalLinkOpener
{
    public bool TryOpen(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
