using System.Windows;
using System.Windows.Controls;

namespace TextRecast.App.Presentation;

internal sealed partial class InformationWindow : Window
{
    public InformationWindow(InformationDialogContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        InitializeComponent();
        Title = $"{content.Title} - TextRecast";
        HeadingTextBlock.Text = content.Heading;
        DescriptionTextBlock.Text = content.Description;
        if (content.Pages.Count == 1)
        {
            PagesTabControl.Visibility = Visibility.Collapsed;
            SinglePagePanel.Visibility = Visibility.Visible;
            SinglePageTextBox.Text = content.Pages[0].Content;
        }
        else
        {
            foreach (var page in content.Pages)
            {
                PagesTabControl.Items.Add(CreateTab(page));
            }

            if (PagesTabControl.Items.Count > 0 &&
                PagesTabControl.Items[0] is TabItem firstTab)
            {
                firstTab.IsSelected = true;
            }
        }

        ApplicationTheme.Apply(this);
    }

    private static TabItem CreateTab(InformationPage page)
    {
        return new TabItem
        {
            Header = page.Header,
            Style = (Style)global::System.Windows.Application.Current.FindResource(
                "InformationTabStyle"),
            Content = new TextBox
            {
                Text = page.Content,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = (System.Windows.Media.Brush)
                    global::System.Windows.Application.Current.FindResource("SurfaceBrush"),
                Foreground = (System.Windows.Media.Brush)
                    global::System.Windows.Application.Current.FindResource("InkBrush"),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 10.5
            }
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
