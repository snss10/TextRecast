namespace TextRecast.Setup;

internal enum SetupOperation
{
    Interactive,
    Install,
    Uninstall
}

internal sealed record SetupOptions(
    SetupOperation Operation,
    bool Quiet,
    string? InstallDirectory);

internal static class SetupCommandLine
{
    public static bool IsQuietRequested(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Contains("--quiet", StringComparer.OrdinalIgnoreCase);
    }

    public static SetupOptions Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0)
        {
            return new SetupOptions(SetupOperation.Interactive, false, null);
        }

        var operation = SetupOperation.Interactive;
        var quiet = false;
        string? installDirectory = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (argument.Equals("--install", StringComparison.OrdinalIgnoreCase))
            {
                operation = SelectOperation(operation, SetupOperation.Install);
            }
            else if (argument.Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
            {
                operation = SelectOperation(operation, SetupOperation.Uninstall);
            }
            else if (argument.Equals("--quiet", StringComparison.OrdinalIgnoreCase))
            {
                quiet = true;
            }
            else if (argument.Equals(
                "--install-directory",
                StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= arguments.Count ||
                    string.IsNullOrWhiteSpace(arguments[index]))
                {
                    throw new ArgumentException(
                        "--install-directory requires a directory path.",
                        nameof(arguments));
                }

                installDirectory = arguments[index];
            }
            else
            {
                throw new ArgumentException(
                    $"Unknown setup argument: {argument}",
                    nameof(arguments));
            }
        }

        if (operation is SetupOperation.Interactive)
        {
            throw new ArgumentException(
                "Select --install or --uninstall when passing setup arguments.",
                nameof(arguments));
        }

        return new SetupOptions(operation, quiet, installDirectory);
    }

    private static SetupOperation SelectOperation(
        SetupOperation current,
        SetupOperation requested)
    {
        if (current is not SetupOperation.Interactive && current != requested)
        {
            throw new ArgumentException(
                "--install and --uninstall cannot be used together.");
        }

        return requested;
    }
}
