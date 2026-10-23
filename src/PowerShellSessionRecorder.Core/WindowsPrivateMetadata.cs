using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PowerShellSessionRecorder.Core;

/// <summary>
/// Owner-only access checks for Windows. Unix modes are not applied here.
/// </summary>
public static class WindowsPrivateMetadata
{
    public static EngineFileInspection Inspect(string path)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path))
            return new EngineFileInspection(false, false);

        try
        {
            var attributes = File.GetAttributes(path);
            return new EngineFileInspection(true, (attributes & FileAttributes.ReparsePoint) != 0);
        }
        catch (Exception exception) when (exception is FileNotFoundException
                     or DirectoryNotFoundException
                     or IOException
                     or UnauthorizedAccessException
                     or ArgumentException
                     or NotSupportedException)
        {
            return new EngineFileInspection(false, false);
        }
    }

    public static bool IsReparsePoint(string path) => Inspect(path).ReparsePoint;

    [SupportedOSPlatform("windows")]
    public static void PrepareOwnerDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (IsReparsePoint(directory))
            throw new IOException("Refusing to use a reparse point as the configuration directory.");

        Directory.CreateDirectory(directory);
        if (IsReparsePoint(directory))
            throw new IOException("Refusing to use a reparse point as the configuration directory.");

        TryRestrictDirectory(directory);
    }

    [SupportedOSPlatform("windows")]
    public static void TryRestrictDirectory(string directory)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(directory) || IsReparsePoint(directory))
            return;

        TryRestrict(new DirectoryInfo(directory), directoryAcl: true);
    }

    [SupportedOSPlatform("windows")]
    public static void TryRestrictFile(string path)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path) || IsReparsePoint(path) || !File.Exists(path))
            return;

        TryRestrict(new FileInfo(path), directoryAcl: false);
    }

    [SupportedOSPlatform("windows")]
    private static void TryRestrict(FileSystemInfo info, bool directoryAcl)
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent().User;
            if (identity is null)
                return;

            FileSystemSecurity security = directoryAcl
                ? new DirectorySecurity(info.FullName, AccessControlSections.Access)
                : new FileSecurity(info.FullName, AccessControlSections.Access);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            foreach (FileSystemAccessRule rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier)))
                security.RemoveAccessRule(rule);

            security.AddAccessRule(directoryAcl
                ? new FileSystemAccessRule(
                    identity,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow)
                : new FileSystemAccessRule(identity, FileSystemRights.FullControl, AccessControlType.Allow));

            if (info is DirectoryInfo directory)
                directory.SetAccessControl((DirectorySecurity)security);
            else if (info is FileInfo file)
                file.SetAccessControl((FileSecurity)security);
        }
        catch (Exception exception) when (exception is IOException
                     or UnauthorizedAccessException
                     or PlatformNotSupportedException
                     or IdentityNotMappedException
                     or InvalidOperationException
                     or System.Security.SecurityException
                     or PrivilegeNotHeldException)
        {
        }
    }
}
