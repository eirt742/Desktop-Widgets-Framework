using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using DesktopWidgets.App.API;
using DesktopWidgets.App.Core;
using DesktopWidgets.App.Win32;
using Microsoft.Web.WebView2.Core;
using MessageBox = System.Windows.MessageBox;

namespace DesktopWidgets.App;

public partial class WidgetWindow : Window
{
    private WidgetEditWindow? _editWindow;
    private readonly string _widgetName;
    private Task? _initializationTask;
    private readonly TaskCompletionSource<bool> _initializationCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WidgetBridgeConnection? _bridgeConnection;
    private bool _desktopAttached;
    private bool _isClosed;

    public string WidgetName => _widgetName;
    public Task InitializationCompleted => _initializationCompleted.Task;

    public WidgetWindow(string widgetName, MainWindow? mw)
    {
        if (string.IsNullOrWhiteSpace(widgetName))
            throw new ArgumentException("Widget name cannot be empty.", nameof(widgetName));

        _widgetName = widgetName;
        InitializeComponent();
        Loaded += WidgetWindow_Loaded;
    }

    private async void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _initializationTask ??= InitializeBrowserAsync();

        try
        {
            await _initializationTask;
            _initializationCompleted.TrySetResult(true);
        }
        catch (Exception ex)
        {
            _initializationCompleted.TrySetException(ex);
            LogError(ex);

            if (!_isClosed)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Widget initialization error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }

    private async Task InitializeBrowserAsync()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        WindowVisibilityHelper.HideFromAltTab(hwnd);

        CoreWebView2 core = await WebView2Runtime.InitializeAsync(Browser);
        if (_isClosed)
            return;

        _bridgeConnection = WidgetBridgeManager.Instance.Connect(core, this);
        Browser.DefaultBackgroundColor = System.Drawing.Color.Transparent;

        string htmlPath = Path.Combine(
            AppContext.BaseDirectory, "Widgets", _widgetName, "web", "index.html"
        );

        if (!File.Exists(htmlPath))
            throw new FileNotFoundException("Widget HTML file not found.", htmlPath);

        Browser.Source = new Uri(htmlPath);

        await Dispatcher.InvokeAsync(
            () => { },
            System.Windows.Threading.DispatcherPriority.ApplicationIdle
        );

        if (_isClosed)
            return;

        _desktopAttached = DesktopHelper.AttachToDesktop(hwnd);
        System.Diagnostics.Debug.WriteLine($"Desktop attachment: {_desktopAttached}");
    }

    private void SetupBridge()
    {
        if (Browser.CoreWebView2 == null)
            throw new InvalidOperationException("WebView2 is not initialized.");

        _bridgeConnection = WidgetBridgeManager.Instance.Connect(
            Browser.CoreWebView2, this
        );
    }

    public void ToggleEditMode()
    {
        if (_editWindow != null)
        {
            _editWindow.Close();
            return;
        }

        if (Browser.Source == null)
            return;

        _editWindow = new WidgetEditWindow(Browser.Source)
        {
            Width = Width,
            Height = Height,
            Left = Left,
            Top = Top
        };

        _editWindow.Closed += (_, _) =>
        {
            if (_editWindow == null)
                return;

            double newLeft = _editWindow.Left;
            double newTop = _editWindow.Top;
            _editWindow = null;

            if (_isClosed)
                return;

            Left = newLeft;
            Top = newTop;
            Show();

            if (_desktopAttached)
            {
                DesktopHelper.MoveToScreenPosition(
                    new WindowInteropHelper(this).Handle,
                    (int)Math.Round(newLeft),
                    (int)Math.Round(newTop)
                );
            }

            Activate();
        };

        Hide();
        _editWindow.Show();
    }

    private static void LogError(Exception ex)
    {
        try
        {
            string path = Path.Combine(
                Path.GetTempPath(), "DesktopWidgets_WebView2.log"
            );
            File.AppendAllText(path, $"[{DateTime.Now:O}]\n{ex}\n\n");
        }
        catch
        {
            // Logging must never break widget lifetime.
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _editWindow?.Close();
        _editWindow = null;

        _bridgeConnection?.Dispose();
        _bridgeConnection = null;

        base.OnClosed(e);
    }
}
