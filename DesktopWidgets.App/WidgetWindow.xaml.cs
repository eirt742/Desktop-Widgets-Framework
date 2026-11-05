
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DesktopWidgets.App.API;
using Microsoft.Web.WebView2.Core;
using DesktopWidgets.App.Win32;
using MessageBox = System.Windows.MessageBox;

namespace DesktopWidgets.App;

public partial class WidgetWindow : Window
{
    private WidgetEditWindow? _editWindow;

    private readonly string _widgetName;

    public string WidgetName => _widgetName;

    private static readonly SemaphoreSlim EnvironmentLock = new(1, 1);
    private static CoreWebView2Environment? SharedEnvironment;

    private Task? _initializationTask;
    
    private readonly TaskCompletionSource<bool> _initializationCompleted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task InitializationCompleted => _initializationCompleted.Task;

    private bool _desktopAttached;

    private WidgetBridgeConnection? _bridgeConnection;

    public WidgetWindow(string widgetName)
    {
        _widgetName = widgetName;

        InitializeComponent();

        Loaded += WidgetWindow_Loaded;
    }

    private async void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowVisibilityHelper.HideFromAltTab(
            new WindowInteropHelper(this).Handle
        );

        try
        {
            _initializationTask ??= InitializeBrowserAsync();

            await _initializationTask;

            _initializationCompleted.TrySetResult(true);
        }
        catch (Exception ex)
        {
            _initializationCompleted.TrySetException(ex);

            LogError(ex);

            MessageBox.Show(
                ex.ToString(),
                "Widget initialization error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private static async Task<CoreWebView2Environment> GetSharedEnvironmentAsync()
    {
        await EnvironmentLock.WaitAsync();

        try
        {
            if (SharedEnvironment == null)
            {
                SharedEnvironment =
                    await CoreWebView2Environment.CreateAsync();
            }

            return SharedEnvironment;
        }
        finally
        {
            EnvironmentLock.Release();
        }
    }

    private async Task InitializeBrowserAsync()
    {
        var environment = await GetSharedEnvironmentAsync();

        await Browser.EnsureCoreWebView2Async(environment);
        
        SetupBridge();

        Browser.DefaultBackgroundColor =
            System.Drawing.Color.Transparent;

        string htmlPath = Path.Combine(
            AppContext.BaseDirectory,
            "Widgets",
            _widgetName,
            "web",
            "index.html"
        );

        if (!File.Exists(htmlPath))
        {
            throw new FileNotFoundException(
                "Widget HTML file not found.",
                htmlPath
            );
        }

        Browser.Source = new Uri(htmlPath);

        await Dispatcher.InvokeAsync(
            () => { },
            DispatcherPriority.ApplicationIdle
        );

        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        _desktopAttached = DesktopHelper.AttachToDesktop(hwnd);

        System.Diagnostics.Debug.WriteLine(
            $"Desktop attachment: {_desktopAttached}"
        );
    }

    private void SetupBridge()
    {
        _bridgeConnection = WidgetBridgeManager.Instance.Connect(
            Browser.CoreWebView2,
            this
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

            Left = newLeft;
            Top = newTop;

            Show();

            if (_desktopAttached)
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;

                DesktopHelper.MoveToScreenPosition(
                    hwnd,
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
                Path.GetTempPath(),
                "DesktopWidgets_WebView2.log"
            );

            File.AppendAllText(
                path,
                $"[{DateTime.Now:O}]\n{ex}\n\n"
            );
        }
        catch
        {
            // Ошибка логирования не должна ломать виджет.
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _editWindow?.Close();
        
        _bridgeConnection?.Dispose();
        _bridgeConnection = null;

        base.OnClosed(e);
    }
}
