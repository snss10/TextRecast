using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace TextRecast.App.Presentation;

internal static class ApplicationTheme
{
    private const int DarkTitleBarAttribute = 20;
    private static readonly DependencyProperty IsDarkProperty =
        DependencyProperty.RegisterAttached(
            "IsDark",
            typeof(bool),
            typeof(ApplicationTheme));

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

    public static void Initialize()
    {
        SetApplicationPalette(IsDarkMode());
    }

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Apply(window, IsDarkMode());
    }

    internal static void Apply(Window window, bool dark)
    {
        ArgumentNullException.ThrowIfNull(window);
        SetApplicationPalette(dark);
        window.SetValue(IsDarkProperty, dark);
        window.SourceInitialized -= Window_SourceInitialized;
        window.SourceInitialized += Window_SourceInitialized;
        window.Activated -= Window_Activated;
        window.Activated += Window_Activated;
        ApplyTitleBar(window, dark);
    }

    private static void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            ApplyTitleBar(window, (bool)window.GetValue(IsDarkProperty));
        }
    }

    private static void Window_Activated(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            var dark = IsDarkMode();
            if (dark != (bool)window.GetValue(IsDarkProperty))
            {
                Apply(window, dark);
            }
        }
    }

    private static void SetApplicationPalette(bool dark)
    {
        var resources = global::System.Windows.Application.Current.Resources;
        Set(resources, "SurfaceBrush", dark ? "#202225" : "#FFFFFF");
        Set(resources, "CanvasBrush", dark ? "#17191C" : "#F6F6F6");
        Set(resources, "InkBrush", dark ? "#F5F5F5" : "#171717");
        Set(resources, "HeaderBrush", dark ? "#17191C" : "#202225");
        Set(resources, "HeaderHoverBrush", dark ? "#363A40" : "#35383D");
        Set(resources, "FooterBrush", dark ? "#1C1F22" : "#F3F3F3");
        Set(resources, "PrimaryHoverBrush", dark ? "#60CDFF" : "#005A9E");
        Set(resources, "SecondaryTextBrush", dark ? "#C7C7C7" : "#4A4A4A");
        Set(resources, "DividerBrush", dark ? "#4A4D51" : "#C9C9C9");
        Set(resources, "DisabledSurfaceBrush", dark ? "#24282C" : "#F4F4F4");
        Set(resources, "DisabledTextBrush", dark ? "#85898E" : "#8A8A8A");
        Set(resources, "AccentBrush", dark ? "#60CDFF" : "#0067C0");
        Set(resources, "AccentOutlineBrush", dark ? "#4BA7D1" : "#60A5D8");
        Set(resources, "AccentSoftBrush", dark ? "#173B4D" : "#E5F1FB");
        Set(resources, "MenuHoverBrush", dark ? "#34373C" : "#E8E8E8");
        Set(resources, "MenuIconBrush", dark ? "#E3E3E3" : "#34373B");
        Set(resources, "LauncherBorderBrush", dark ? "#72777F" : "#5E636B");
    }

    private static void Set(ResourceDictionary resources, string key, string color)
    {
        resources[key] = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color));
    }

    private static void ApplyTitleBar(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        var enabled = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(
            handle,
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
