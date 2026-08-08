using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using TextRecast.Deployment.Installer;
using TextRecast.Deployment.Setup;

namespace TextRecast.App;

internal static class SetupRecoveryLauncher
{
    internal static bool IsRecoveryRequired(
        ModelSetupState state,
        string? activeModelId,
        string? verifiedModelPath)
    {
        ArgumentNullException.ThrowIfNull(state);
        return !state.IsComplete ||
            string.IsNullOrWhiteSpace(activeModelId) ||
            string.IsNullOrWhiteSpace(verifiedModelPath);
    }

    public static bool TryLaunch(out string errorMessage)
    {
        var setupPath = FindSetupHost();
        if (setupPath is null)
        {
            errorMessage =
                "TextRecast setup is incomplete, but the recovery copy of Setup is missing. " +
                "Run the official TextRecast setup again to repair the installation.";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(setupPath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(setupPath)
            });
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is
            Win32Exception or
            IOException or
            InvalidOperationException)
        {
            errorMessage =
                "TextRecast could not open Setup to complete model installation. " +
                $"Run {WindowsInstallerIdentity.SetupHostName} manually. {exception.Message}";
            return false;
        }
    }

    internal static string? FindSetupHost(string? applicationDirectory = null)
    {
        var baseDirectory = Path.GetFullPath(
            applicationDirectory ?? AppContext.BaseDirectory);
        var candidates = new[]
        {
            Path.Combine(baseDirectory, WindowsInstallerIdentity.SetupHostName),
            Path.Combine(
                Directory.GetParent(baseDirectory)?.FullName ?? baseDirectory,
                WindowsInstallerIdentity.SetupHostName)
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
