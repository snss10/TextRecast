using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TextRecast.Setup;

internal sealed class LegalDocumentWindow : Window
{
    public LegalDocumentWindow(Window owner, string title, string content)
    {
        Owner = owner;
        Title = title;
        Width = 720;
        Height = 560;
        MinWidth = 560;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = (Brush)owner.FindResource("WindowBackgroundBrush");
        Foreground = (Brush)owner.FindResource("PrimaryTextBrush");

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(24, 20, 24, 16)
        };
        grid.Children.Add(heading);

        var text = new TextBox
        {
            Text = content,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Margin = new Thickness(24, 0, 24, 16),
            Padding = new Thickness(12),
            Background = (Brush)owner.FindResource("PanelBackgroundBrush"),
            Foreground = (Brush)owner.FindResource("PrimaryTextBrush"),
            BorderBrush = (Brush)owner.FindResource("BorderBrush")
        };
        Grid.SetRow(text, 1);
        grid.Children.Add(text);

        var close = new Button
        {
            Content = "Close",
            IsDefault = true,
            IsCancel = true,
            MinWidth = 120,
            Height = 34,
            Margin = new Thickness(0, 0, 24, 18),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        close.Click += (_, _) => Close();
        Grid.SetRow(close, 2);
        grid.Children.Add(close);
        Content = grid;
    }
}
