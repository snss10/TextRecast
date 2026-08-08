using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.App.Presentation;

internal sealed record InformationPage(string Header, string Content);

internal sealed record InformationDialogContent(
    string Title,
    string Heading,
    string Description,
    IReadOnlyList<InformationPage> Pages);

internal static class ApplicationInformation
{
    private static readonly (string Header, string FileName)[] LegalFiles =
    [
        ("Privacy", "PRIVACY.md"),
        ("License", "LICENSE"),
        ("Notice", "NOTICE"),
        ("Third-party notices", "THIRD-PARTY-NOTICES.md")
    ];

    public static InformationDialogContent CreateModel(SlmModelProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var profileName = profile.Role switch
        {
            SlmModelRole.Fast => "Fast/default",
            SlmModelRole.Balanced => "Balanced",
            SlmModelRole.Quality => "Best quality",
            SlmModelRole.Alternative => "Alternative",
            _ => profile.Role.ToString()
        };
        var content = string.Join(
            Environment.NewLine + Environment.NewLine,
            $"Profile{Environment.NewLine}{profileName}",
            $"Download size{Environment.NewLine}{FormatGiB(profile.ExpectedFileSize)}",
            $"Language{Environment.NewLine}{profile.LanguageSupport}",
            $"Source{Environment.NewLine}{profile.SourceRepository}",
            $"License{Environment.NewLine}{profile.LicenseExpression}",
            $"Review guidance{Environment.NewLine}{profile.LimitationNotice}",
            "TextRecast runs this model locally. Always review generated text before using it.");

        return new InformationDialogContent(
            "Model information",
            profile.DisplayName,
            "The active local language model used by TextRecast.",
            [new InformationPage("Details", content)]);
    }

    public static InformationDialogContent CreateLegalAndPrivacy()
    {
        var pages = LegalFiles
            .Select(item => new InformationPage(
                item.Header,
                ReadPackagedDocument(item.FileName)))
            .ToArray();
        return new InformationDialogContent(
            "Legal & Privacy",
            "Legal & Privacy",
            "These documents are packaged with TextRecast and remain available offline.",
            pages);
    }

    public static InformationDialogContent CreateAbout()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString(3) ?? "Unknown";
        var content = string.Join(
            Environment.NewLine + Environment.NewLine,
            $"Version{Environment.NewLine}{version}",
            "TextRecast is a Windows writing assistant that formats selected text with a local language model.",
            "Text processing and model inference stay on this computer. Model downloads occur only during setup or recovery.",
            "Licensed under the Apache License 2.0.",
            "Copyright 2026 snss10");
        return new InformationDialogContent(
            "About TextRecast",
            "TextRecast",
            "Local writing assistance for selected text.",
            [new InformationPage("About", content)]);
    }

    private static string ReadPackagedDocument(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        try
        {
            var content = File.ReadAllText(path);
            return fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? FormatMarkdownForDisplay(content)
                : content;
        }
        catch (Exception exception) when (exception is
            FileNotFoundException or
            DirectoryNotFoundException or
            IOException or
            UnauthorizedAccessException)
        {
            return $"The packaged {fileName} document could not be opened. " +
                "Repair TextRecast with the official setup and try again.";
        }
    }

    private static string FormatMarkdownForDisplay(string markdown)
    {
        var lines = markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => Regex.Replace(line, @"^#{1,6}\s+", string.Empty))
            .Select(line => line.StartsWith("- ", StringComparison.Ordinal)
                ? $"• {line[2..]}"
                : line)
            .Select(line => Regex.Replace(line, @"\[([^\]]+)\]\(([^)]+)\)", "$1 ($2)"))
            .Select(line => line.Replace("`", string.Empty, StringComparison.Ordinal));
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatGiB(long bytes)
    {
        const double gibibyte = 1024d * 1024 * 1024;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{bytes / gibibyte:F2} GiB");
    }
}
