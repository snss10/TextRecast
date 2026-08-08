using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace TextRecast.Setup;

internal static class SetupTheme
{
    private const int DarkTitleBarAttribute = 20;
    private static readonly DependencyProperty IsDarkTitleBarProperty =
        DependencyProperty.RegisterAttached(
            "IsDarkTitleBar",
            typeof(bool),
            typeof(SetupTheme));

    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception exception) when (
            exception is SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void Apply(FrameworkElement element)
    {
        Apply(element, IsDarkMode());
    }

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Apply(window, IsDarkMode());
    }

    internal static void Apply(Window window, bool dark)
    {
        ArgumentNullException.ThrowIfNull(window);
        Apply((FrameworkElement)window, dark);
        window.SetValue(IsDarkTitleBarProperty, dark);
        window.SourceInitialized -= Window_SourceInitialized;
        window.SourceInitialized += Window_SourceInitialized;
        window.Loaded -= Window_Loaded;
        window.Loaded += Window_Loaded;
        if (new WindowInteropHelper(window).Handle != nint.Zero)
        {
            ApplyTitleBar(window, dark);
        }
    }

    private static void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            ApplyTitleBar(window, (bool)window.GetValue(IsDarkTitleBarProperty));
        }
    }

    private static void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            ApplyTitleBar(window, (bool)window.GetValue(IsDarkTitleBarProperty));
        }
    }

    internal static void Apply(FrameworkElement element, bool dark)
    {
        ArgumentNullException.ThrowIfNull(element);
        Set(element, "WindowBackgroundBrush", dark ? "#17191C" : "#FFFFFF");
        Set(element, "PanelBackgroundBrush", dark ? "#202327" : "#F6F6F6");
        Set(element, "SubtleBackgroundBrush", dark ? "#292D32" : "#F0F0F0");
        Set(element, "ControlHoverBrush", dark ? "#33383E" : "#E7E7E7");
        Set(element, "ControlPressedBrush", dark ? "#3B4148" : "#DADADA");
        Set(element, "DisabledBackgroundBrush", dark ? "#24282C" : "#F4F4F4");
        Set(element, "DisabledBorderBrush", dark ? "#3B3F44" : "#D6D6D6");
        Set(element, "FooterBackgroundBrush", dark ? "#1C1F22" : "#F3F3F3");
        Set(element, "PrimaryTextBrush", dark ? "#F5F5F5" : "#171717");
        Set(element, "SecondaryTextBrush", dark ? "#C7C7C7" : "#4A4A4A");
        Set(element, "BorderBrush", dark ? "#4A4D51" : "#C9C9C9");
        Set(element, "AccentBrush", dark ? "#60CDFF" : "#0067C0");
        Set(element, "ProgressBrush", "#16A34A");
        Set(element, "BrandPanelBrush", dark ? "#101214" : "#202225");
        Set(element, "BrandTextBrush", "#FFFFFF");
    }

    private static void Set(FrameworkElement element, string key, string color)
    {
        element.Resources[key] = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color));
    }

    private static void ApplyTitleBar(Window window, bool dark)
    {
        var enabled = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(
            new WindowInteropHelper(window).Handle,
            DarkTitleBarAttribute,
            ref enabled,
            sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
