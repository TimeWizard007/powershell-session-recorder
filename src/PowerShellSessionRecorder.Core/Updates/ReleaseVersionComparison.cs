namespace PowerShellSessionRecorder.Core.Updates;

public enum ReleaseRelation
{
    NewerStable,
    SameStable,
    RemoteOlder,
    RemotePrerelease,
    InvalidRemote,
    InvalidLocal
}

/// <summary>
/// Compares the installed informational version with a GitHub release tag.
/// Only a stable remote release can be reported as newer.
/// A checkpoint label such as <c>0.1.0-checkpoint.2.1</c> is a prerelease of that core version.
/// </summary>
public static class ReleaseVersionComparison
{
    public static ReleaseRelation Relate(string? localInformationalVersion, string? remoteTag)
    {
        if (!ReleaseVersion.TryParse(localInformationalVersion, out var local))
            return ReleaseRelation.InvalidLocal;

        if (!ReleaseVersion.TryParse(remoteTag, out var remote))
            return ReleaseRelation.InvalidRemote;

        if (!remote.IsStable)
            return ReleaseRelation.RemotePrerelease;

        var order = remote.CompareTo(local);
        if (order > 0)
            return ReleaseRelation.NewerStable;

        if (order == 0)
            return ReleaseRelation.SameStable;

        return ReleaseRelation.RemoteOlder;
    }
}
