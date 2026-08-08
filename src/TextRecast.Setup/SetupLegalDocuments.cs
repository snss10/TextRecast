using System.IO;
using System.Reflection;

namespace TextRecast.Setup;

internal static class SetupLegalDocuments
{
    public static string License { get; } = Read("TextRecast.Legal.LICENSE");

    public static string Privacy { get; } = Read("TextRecast.Legal.PRIVACY.md");

    public static string Notice { get; } = Read("TextRecast.Legal.NOTICE");

    public static string ThirdPartyNotices { get; } =
        Read("TextRecast.Legal.THIRD-PARTY-NOTICES.md");

    private static string Read(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName) ??
            throw new InvalidDataException(
                $"Setup is missing its offline legal resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
