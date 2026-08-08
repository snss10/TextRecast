namespace TextRecast.Deployment.Installer;

public static class WindowsInstallerIdentity
{
    public const string ProductId = "{FF637B7C-6CB2-470E-A9A6-7366AA51A3D6}";
    public const string ProductName = "TextRecast";
    public const string Publisher = "snss10";
    public const string InstallFolderName = "TextRecast";
    public const string PerUserInstallPath = @"Programs\TextRecast";
    public const string StartMenuFolderName = "TextRecast";
    public const string StartMenuShortcutName = "TextRecast.lnk";
    public const string ExecutableName = "TextRecast.exe";
    public const string UninstallerName = "Uninstall.exe";
    public const string ApplicationRegistryKey = @"Software\TextRecast";
    public const string UninstallRegistryKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast";

    public static string GetDefaultInstallDirectory(
        string localApplicationDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationDataDirectory);
        return Path.Combine(
            Path.GetFullPath(localApplicationDataDirectory),
            PerUserInstallPath);
    }
}
