using Avalonia.Threading;
using PowerShellSessionRecorder.Core.Updates;

namespace PowerShellSessionRecorder.App.Services.Updates;

public interface IUiInvoker
{
    Task InvokeAsync(Action action);
}

public sealed class InlineUiInvoker : IUiInvoker
{
    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}

public sealed class AvaloniaUiInvoker : IUiInvoker
{
    public async Task InvokeAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }
}

internal sealed class UnavailableReleaseSource : IGitHubReleaseSource
{
    public Task<GitHubFetchResult> GetLatestAsync(UpdateChannel channel, CancellationToken cancellationToken)
        => Task.FromResult(new GitHubFetchResult(GitHubFetchStatus.Offline, null, null));
}
