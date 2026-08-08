using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using TextRecast.Deployment.Installer;

namespace TextRecast.Setup;

internal sealed record PackageOperationResult(int ExitCode, string Message)
{
    public bool Succeeded => ExitCode == 0;
}

internal interface IInstallerPackageEngine
{
    string DefaultInstallDirectory { get; }

    string ResolveInstalledDirectory();

    bool IsInstalled(string installDirectory);

    Task<PackageOperationResult> InstallAsync(string installDirectory);

    Task<PackageOperationResult> UninstallAsync(string installDirectory);
}

internal sealed class EmbeddedPackageEngine : IInstallerPackageEngine
{
    private const string ApplicationDirectoryName = "Application";
    private const string PayloadMarkerFileName = ".textrecast-payload-complete";
    private const string PackageResourceName = "TextRecast.Package.exe";
    private const string InstallLocationValueName = "InstallLocation";

    public EmbeddedPackageEngine()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "The local application-data directory is unavailable.");
        }

        LocalApplicationDataDirectory = Path.GetFullPath(localApplicationData);
        DefaultInstallDirectory = WindowsInstallerIdentity.GetDefaultInstallDirectory(
            LocalApplicationDataDirectory);
    }

    internal string LocalApplicationDataDirectory { get; }

    public string DefaultInstallDirectory { get; }

    public string ResolveInstalledDirectory()
    {
        using var applicationKey = Registry.CurrentUser.OpenSubKey(
            WindowsInstallerIdentity.ApplicationRegistryKey,
            writable: false);
        var registeredPath = applicationKey?.GetValue(
            InstallLocationValueName,
            defaultValue: null,
            RegistryValueOptions.DoNotExpandEnvironmentNames) as string;

        if (!string.IsNullOrWhiteSpace(registeredPath))
        {
            try
            {
                return ValidateInstallDirectory(registeredPath);
            }
            catch (ArgumentException)
            {
            }
        }

        return DefaultInstallDirectory;
    }

    public bool IsInstalled(string installDirectory)
    {
        var validatedDirectory = ValidateInstallDirectory(installDirectory);
        var applicationDirectory = GetApplicationDirectory(validatedDirectory);
        return HasValidInstallationMarker(validatedDirectory) &&
            File.Exists(Path.Combine(
                applicationDirectory,
                WindowsInstallerIdentity.ExecutableName)) &&
            HasValidPayloadMarker(applicationDirectory) &&
            File.Exists(Path.Combine(
                validatedDirectory,
                WindowsInstallerIdentity.UninstallerName));
    }

    public async Task<PackageOperationResult> InstallAsync(string installDirectory)
    {
        var validatedDirectory = ValidateInstallDirectory(installDirectory);
        EnsureInstallTargetIsOwnedOrEmpty(validatedDirectory);
        var extractionDirectory = CreateExtractionDirectory();
        try
        {
            var packagePath = await ExtractPackageAsync(extractionDirectory)
                .ConfigureAwait(false);
            var exitCode = await RunProcessAsync(
                    packagePath,
                    ["/S", $"/D={validatedDirectory}"])
                .ConfigureAwait(false);
            if (exitCode != 0)
            {
                return new PackageOperationResult(
                    exitCode,
                    $"The Windows package engine returned exit code {exitCode}.");
            }

            VerifyInstalledPayload(validatedDirectory);
            return new PackageOperationResult(0, "TextRecast was installed successfully.");
        }
        finally
        {
            TryDeleteDirectory(extractionDirectory);
        }
    }

    public async Task<PackageOperationResult> UninstallAsync(string installDirectory)
    {
        var validatedDirectory = ValidateInstallDirectory(installDirectory);
        if (!Directory.Exists(validatedDirectory))
        {
            return new PackageOperationResult(0, "TextRecast is not installed.");
        }

        if (!HasValidInstallationMarker(validatedDirectory))
        {
            throw new InvalidDataException(
                "The installation marker is missing or invalid. Application files were not removed.");
        }

        var uninstallerPath = Path.Combine(
            validatedDirectory,
            WindowsInstallerIdentity.UninstallerName);
        if (!File.Exists(uninstallerPath))
        {
            return new PackageOperationResult(
                1,
                "The installation is incomplete and has no uninstaller. Run Setup again to repair it.");
        }

        var exitCode = await RunProcessAsync(uninstallerPath, ["/S"])
            .ConfigureAwait(false);
        if (exitCode != 0)
        {
            return new PackageOperationResult(
                exitCode,
                $"The Windows uninstaller returned exit code {exitCode}.");
        }

        if (!await WaitForDirectoryRemovalAsync(validatedDirectory)
                .ConfigureAwait(false))
        {
            return new PackageOperationResult(
                1,
                "The uninstaller completed, but the installation directory remains.");
        }

        return new PackageOperationResult(0, "TextRecast was removed successfully.");
    }

    internal string ValidateInstallDirectory(string installDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);
        var fullPath = Path.GetFullPath(installDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var localPath = LocalApplicationDataDirectory
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var relativePath = Path.GetRelativePath(localPath, fullPath);
        if (relativePath.Equals(".", StringComparison.Ordinal) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException(
                "TextRecast must be installed inside the current user's local application-data directory.",
                nameof(installDirectory));
        }

        var userDataDirectory = Path.Combine(localPath, "TextRecast");
        if (IsSameOrDescendant(fullPath, userDataDirectory))
        {
            throw new ArgumentException(
                "The application cannot be installed inside TextRecast's model and settings directory.",
                nameof(installDirectory));
        }

        if (!Path.GetFileName(fullPath).Equals(
                WindowsInstallerIdentity.InstallFolderName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The installation directory must end with '{WindowsInstallerIdentity.InstallFolderName}'.",
                nameof(installDirectory));
        }

        return fullPath;
    }

    internal void EnsureInstallTargetIsOwnedOrEmpty(string installDirectory)
    {
        var validatedDirectory = ValidateInstallDirectory(installDirectory);
        if (!Directory.Exists(validatedDirectory) ||
            !Directory.EnumerateFileSystemEntries(validatedDirectory).Any() ||
            HasValidInstallationMarker(validatedDirectory))
        {
            return;
        }

        throw new InvalidDataException(
            "The selected installation directory contains files that are not owned by TextRecast.");
    }

    private static bool HasValidInstallationMarker(string installDirectory)
    {
        var markerPath = Path.Combine(installDirectory, ".textrecast-install");
        try
        {
            return File.Exists(markerPath) &&
                File.ReadAllText(markerPath).Equals(
                    WindowsInstallerIdentity.ProductId,
                    StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasValidPayloadMarker(string applicationDirectory)
    {
        var markerPath = Path.Combine(applicationDirectory, PayloadMarkerFileName);
        try
        {
            var value = File.ReadAllText(markerPath);
            return !string.IsNullOrWhiteSpace(value) &&
                value.Equals(value.Trim(), StringComparison.Ordinal) &&
                Version.TryParse(value, out var version) &&
                version.Build >= 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsSameOrDescendant(string path, string parent)
    {
        var normalizedPath = path.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var normalizedParent = parent.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return normalizedPath.Equals(normalizedParent, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(
                $"{normalizedParent}{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateExtractionDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "TextRecast.Setup",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<string> ExtractPackageAsync(string extractionDirectory)
    {
        await using var packageStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(PackageResourceName) ??
            throw new InvalidDataException(
                "The setup executable does not contain the internal application package.");
        var packagePath = Path.Combine(extractionDirectory, "TextRecast.Package.exe");
        await using var output = new FileStream(
            packagePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await packageStream.CopyToAsync(output).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
        return packagePath;
    }

    private static async Task<int> RunProcessAsync(
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ??
                AppContext.BaseDirectory
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException(
                "The Windows package engine could not be started.");
        await process.WaitForExitAsync().ConfigureAwait(false);
        return process.ExitCode;
    }

    private static async Task<bool> WaitForDirectoryRemovalAsync(string path)
    {
        const int attemptCount = 40;
        for (var attempt = 0; attempt < attemptCount; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        }

        return !Directory.Exists(path);
    }

    private static void VerifyInstalledPayload(string installDirectory)
    {
        if (!HasValidInstallationMarker(installDirectory))
        {
            throw new InvalidDataException(
                "The installed application has a missing or invalid ownership marker.");
        }

        var applicationDirectory = GetApplicationDirectory(installDirectory);
        var requiredApplicationFiles = new[]
        {
            WindowsInstallerIdentity.ExecutableName,
            "LICENSE",
            "NOTICE",
            "PRIVACY.md",
            "THIRD-PARTY-NOTICES.md",
            "NSIS-LICENSE.txt",
            "DOTNET-LICENSE.txt",
            "DOTNET-THIRD-PARTY-NOTICES.txt",
            "WPF-LICENSE.txt",
            PayloadMarkerFileName
        };
        var missingFile = requiredApplicationFiles.FirstOrDefault(file =>
            !File.Exists(Path.Combine(applicationDirectory, file)));
        if (missingFile is not null)
        {
            throw new InvalidDataException(
                $"The installed application is missing required file: {missingFile}");
        }

        if (!HasValidPayloadMarker(applicationDirectory))
        {
            throw new InvalidDataException(
                "The installed application has a missing or invalid payload marker.");
        }

        if (!File.Exists(Path.Combine(
                installDirectory,
                WindowsInstallerIdentity.UninstallerName)))
        {
            throw new InvalidDataException(
                $"The installed application is missing required file: {WindowsInstallerIdentity.UninstallerName}");
        }

        var excludedPayload = Directory.EnumerateFiles(
                installDirectory,
                "*",
                SearchOption.AllDirectories)
            .FirstOrDefault(path =>
                path.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".partial", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(
                    ".partial.metadata.json",
                    StringComparison.OrdinalIgnoreCase));
        if (excludedPayload is not null)
        {
            throw new InvalidDataException(
                "The model-free installation unexpectedly contains model data.");
        }
    }

    private static string GetApplicationDirectory(string installDirectory) =>
        Path.Combine(installDirectory, ApplicationDirectoryName);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
