using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using TextRecast.Core.Application;
using TextRecast.Core.Models;
using TextRecast.Infrastructure.SLM;
using TextRecast.Infrastructure.Windows.Native;

namespace TextRecast.App.Presentation;

public partial class MainWindow : Window
{
    private readonly FormatTextWorkflow _formatTextWorkflow;
    private readonly SlmModelProfile _activeModelProfile;
    private ResultWindow? _resultWindow;
    private IntPtr _windowHandle;
    private Point? _dragStartScreen;
    private Point? _windowStart;
    private IntPtr _captureTargetWindow;
    private bool _didDrag;
    private int _captureInProgress;

    internal MainWindow(
        FormatTextWorkflow formatTextWorkflow,
        SlmModelProfile activeModelProfile)
    {
        InitializeComponent();
        _formatTextWorkflow = formatTextWorkflow ??
            throw new ArgumentNullException(nameof(formatTextWorkflow));
        _activeModelProfile = activeModelProfile ??
            throw new ArgumentNullException(nameof(activeModelProfile));
        ApplicationTheme.Apply(this);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _windowHandle = new WindowInteropHelper(this).Handle;
        NativeMethods.EnableNoActivateToolWindow(_windowHandle);
        KeepLauncherOnTop();
    }

    private void LauncherSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _captureTargetWindow = NativeMethods.GetForegroundWindowHandle();
        _dragStartScreen = PointToScreen(e.GetPosition(this));
        _windowStart = new Point(Left, Top);
        _didDrag = false;
        LauncherSurface.CaptureMouse();
    }

    private void LauncherSurface_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStartScreen is null || _windowStart is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = PointToScreen(e.GetPosition(this));
        var deltaX = current.X - _dragStartScreen.Value.X;
        var deltaY = current.Y - _dragStartScreen.Value.Y;

        if (Math.Abs(deltaX) + Math.Abs(deltaY) > 4)
        {
            _didDrag = true;
        }

        MoveLauncherTo(_windowStart.Value.X + deltaX, _windowStart.Value.Y + deltaY);
    }

    private async void LauncherSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        LauncherSurface.ReleaseMouseCapture();
        _dragStartScreen = null;
        _windowStart = null;

        if (_didDrag)
        {
            KeepLauncherOnTop();
        }
        else
        {
            await FormatSelectedTextAsync();
        }
    }

    private async void FormatSelectedText_Click(object sender, RoutedEventArgs e)
    {
        _captureTargetWindow = NativeMethods.GetForegroundWindowHandle();
        await FormatSelectedTextAsync();
    }

    private async void LauncherSurface_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            _captureTargetWindow = NativeMethods.GetForegroundWindowHandle();
            e.Handled = true;
            await FormatSelectedTextAsync();
        }
    }

    private async Task FormatSelectedTextAsync()
    {
        var targetWindow = _captureTargetWindow;
        _captureTargetWindow = IntPtr.Zero;

        if (Interlocked.CompareExchange(ref _captureInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var capture = await _formatTextWorkflow.CaptureAsync(targetWindow);
            if (!capture.Success)
            {
                ShowResult(capture.Message, selection: null);
                return;
            }

            var displayText = capture.Selection!.Text;
            if (!string.IsNullOrWhiteSpace(capture.Warning))
            {
                displayText += $"{Environment.NewLine}{Environment.NewLine}Note: {capture.Warning}";
            }

            ShowResult(displayText, capture.Selection);
        }
        finally
        {
            Volatile.Write(ref _captureInProgress, 0);
        }
    }

    private void ShowResult(string displayText, SelectionContext? selection)
    {
        if (_resultWindow is not null && _resultWindow.IsVisible)
        {
            _resultWindow.UpdateResult(displayText, selection);
            _resultWindow.Activate();
            return;
        }

        _resultWindow = new ResultWindow(
            displayText,
            selection,
            _activeModelProfile.DisplayName,
            _formatTextWorkflow.GenerateAsync,
            _formatTextWorkflow.ReplaceAsync)
        {
            Owner = this
        };

        _resultWindow.Left = Math.Min(
            Left + Width + 10,
            SystemParameters.WorkArea.Right - _resultWindow.Width - 8);
        _resultWindow.Top = Math.Min(
            Top,
            SystemParameters.WorkArea.Bottom - _resultWindow.Height - 8);

        _resultWindow.Left = Math.Max(SystemParameters.WorkArea.Left + 8, _resultWindow.Left);
        _resultWindow.Top = Math.Max(SystemParameters.WorkArea.Top + 8, _resultWindow.Top);
        _resultWindow.Closed += (_, _) => _resultWindow = null;
        _resultWindow.Show();
        KeepLauncherOnTop();
    }

    private void MoveLauncherTo(double left, double top)
    {
        var maxLeft = SystemParameters.WorkArea.Right - Width;
        var maxTop = SystemParameters.WorkArea.Bottom - Height;

        Left = Math.Max(SystemParameters.WorkArea.Left, Math.Min(left, maxLeft));
        Top = Math.Max(SystemParameters.WorkArea.Top, Math.Min(top, maxTop));
    }

    private void KeepLauncherOnTop()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        Topmost = true;
        NativeMethods.KeepWindowTopMostNoActivate(_windowHandle);
    }

    private void Quit_Click(object sender, RoutedEventArgs e)
    {
        global::System.Windows.Application.Current.Shutdown();
    }

    private void ModelInformation_Click(object sender, RoutedEventArgs e)
    {
        ShowInformationDialog(ApplicationInformation.CreateModel(_activeModelProfile));
    }

    private void LegalPrivacy_Click(object sender, RoutedEventArgs e)
    {
        ShowInformationDialog(ApplicationInformation.CreateLegalAndPrivacy());
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        ShowInformationDialog(ApplicationInformation.CreateAbout());
    }

    private void LauncherMenu_Opened(object sender, RoutedEventArgs e)
    {
        ApplicationTheme.Apply(this);
    }

    private void ShowInformationDialog(InformationDialogContent content)
    {
        var dialog = new InformationWindow(content)
        {
            Owner = this
        };
        dialog.ShowDialog();
        KeepLauncherOnTop();
    }
}
