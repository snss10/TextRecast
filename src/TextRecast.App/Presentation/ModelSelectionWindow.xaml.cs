using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.App.Presentation;

public partial class ModelSelectionWindow : Window
{
    private readonly string _initialModelId;

    internal ModelSelectionWindow(
        IReadOnlyList<ModelSelectionChoice> choices,
        string initialModelId)
    {
        ArgumentNullException.ThrowIfNull(choices);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialModelId);
        Choices = choices;
        _initialModelId = initialModelId;
        InitializeComponent();
        DataContext = this;
        Loaded += ModelSelectionWindow_Loaded;
    }

    public IReadOnlyList<ModelSelectionChoice> Choices { get; }

    public SlmModelProfile? SelectedProfile { get; private set; }

    private void ModelSelectionWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ModelListBox.SelectedItem = Choices.FirstOrDefault(choice =>
                choice.IsCompatible &&
                choice.Profile.Id.Equals(_initialModelId, StringComparison.Ordinal))
            ?? Choices.FirstOrDefault(choice => choice.IsCompatible);
    }

    private void ModelListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ContinueButton.IsEnabled = ModelListBox.SelectedItem is ModelSelectionChoice
        {
            IsCompatible: true
        };
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (ModelListBox.SelectedItem is not ModelSelectionChoice choice ||
            !choice.IsCompatible)
        {
            return;
        }

        var action = choice.IsInstalled ? "Use" : "Download";
        var message =
            $"{action} {choice.Profile.DisplayName}?\n\n" +
            $"Model: {choice.Profile.Id}\n" +
            $"Download size: {choice.DownloadSizeText}\n" +
            $"Language: {choice.Profile.LanguageSupport}\n" +
            $"License: {choice.Profile.LicenseExpression}\n\n" +
            $"{choice.Profile.LimitationNotice}\n\n" +
            (choice.IsInstalled
                ? "The verified installed copy will be reused."
                : "Only this model will be downloaded. No other model will be queued.");

        var result = MessageBox.Show(
            this,
            message,
            "Confirm local model",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        SelectedProfile = choice.Profile;
        DialogResult = true;
    }
}

public sealed record ModelSelectionChoice(
    SlmModelProfile Profile,
    bool IsCompatible,
    bool IsInstalled,
    bool IsRecommended,
    string CompatibilityText)
{
    public string DownloadSizeText => FormatBytes(Profile.ExpectedFileSize);

    public string BadgeText
    {
        get
        {
            var badges = new List<string> { FormatRole(Profile.Role), Profile.LanguageSupport };
            if (IsRecommended)
            {
                badges.Add("Recommended for this PC");
            }

            if (IsInstalled)
            {
                badges.Add("Installed");
            }

            if (Profile.IsExperimental)
            {
                badges.Add("Experimental");
            }

            return string.Join(" | ", badges);
        }
    }

    private static string FormatRole(SlmModelRole role)
    {
        return role switch
        {
            SlmModelRole.Fast => "Fast / default",
            SlmModelRole.Balanced => "Balanced",
            SlmModelRole.Quality => "Best quality",
            SlmModelRole.Alternative => "Alternative",
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
    }

    private static string FormatBytes(long bytes)
    {
        const double bytesPerGibibyte = 1024d * 1024 * 1024;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{bytes / bytesPerGibibyte:F2} GiB");
    }
}
