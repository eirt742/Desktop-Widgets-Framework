using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using DesktopWidgets.App.API;
using DesktopWidgets.App.Core;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace DesktopWidgets.App;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly Dictionary<string, WidgetWindow> _windows =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string _statePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopWidgets",
        "opened-widgets.json"
    );

    private Forms.NotifyIcon? _notifyIcon;
    private GlobalHotkey? _showKey;
    private bool _isShuttingDown;
    private bool _onEditMode;

    public ObservableCollection<WidgetInfo> Widgets { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        LoadWidgetCatalog();
        InitTray();

        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Closed += OnClosed;
    }

    private void LoadWidgetCatalog()
    {
        foreach (string directoryName in WidgetData.GetWidgetsNames())
        {
            try
            {
                WidgetConfig? config = WidgetData.LoadWidgetData(directoryName);
                if (config == null)
                    continue;

                Widgets.Add(new WidgetInfo
                {
                    Id = directoryName,
                    Name = string.IsNullOrWhiteSpace(config.widget_name)
                        ? directoryName
                        : config.widget_name,
                    Description = config.widget_description ?? string.Empty,
                    IsRunning = false
                });
            }
            catch (Exception ex)
            {
                LogError($"Failed to load widget manifest '{directoryName}'.", ex);
            }
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            _showKey = new GlobalHotkey(
                this,
                System.Windows.Input.Key.F9,
                System.Windows.Input.ModifierKeys.Control
            );
            _showKey.Pressed += ToggleEditMode;
        }
        catch (Exception ex)
        {
            LogError("Global hotkey registration failed.", ex);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RestoreOpenedWidgetsAsync();
    }

    private async Task RestoreOpenedWidgetsAsync()
    {
        if (!File.Exists(_statePath))
            return;

        List<WidgetWindowState>? states;
        try
        {
            string json = await File.ReadAllTextAsync(_statePath);
            states = JsonSerializer.Deserialize<List<WidgetWindowState>>(json);
        }
        catch (Exception ex)
        {
            LogError("Could not read saved widget state.", ex);
            MessageBox.Show(
                ex.Message,
                "Не удалось восстановить виджеты",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        if (states == null)
            return;

        foreach (WidgetWindowState state in states)
        {
            if (string.IsNullOrWhiteSpace(state.Name))
                continue;

            try
            {
                await StartWidgetAsync(state.Name, state.Left, state.Top, saveState: false);
            }
            catch (Exception ex)
            {
                LogError($"Could not restore widget '{state.Name}'.", ex);
            }
        }

        SaveOpenedWidgets();
    }

    private void InitTray()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "Desktop Widgets"
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Выход", null, (_, _) => ShutdownApplication());
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (_isShuttingDown)
            return;

        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void CloseToTray_CLick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private async void WidgetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button ||
            button.DataContext is not WidgetInfo widget ||
            string.IsNullOrWhiteSpace(widget.Id))
            return;

        if (_windows.ContainsKey(widget.Id))
        {
            CloseWidget(widget.Id);
            return;
        }

        try
        {
            await StartWidgetAsync(widget.Id, saveState: true);
        }
        catch (Exception ex)
        {
            LogError($"Could not start widget '{widget.Name}'.", ex);
            MessageBox.Show(
                ex.Message,
                $"Не удалось запустить виджет: {widget.Name}",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private async Task StartWidgetAsync(
        string widgetName,
        double? left = null,
        double? top = null,
        bool saveState = true)
    {
        if (_isShuttingDown)
            throw new InvalidOperationException("Application is shutting down.");

        if (_windows.ContainsKey(widgetName))
            return;

        WidgetConfig config = WidgetData.LoadWidgetData(widgetName)
            ?? throw new InvalidDataException(
                $"Manifest for widget '{widgetName}' is empty."
            );

        var window = new WidgetWindow(widgetName)
        {
            Width = config.width > 0 ? config.width : 280,
            Height = config.height > 0 ? config.height : 145,
            Left = left ?? 100,
            Top = top ?? 100
        };

        _windows.Add(widgetName, window);
        window.Closed += (_, _) => OnWidgetClosed(widgetName, window);
        SetWidgetRunning(widgetName, true);

        try
        {
            window.Show();
            await window.InitializationCompleted;

            if (saveState)
                SaveOpenedWidgets();
        }
        catch
        {
            if (_windows.TryGetValue(widgetName, out WidgetWindow? current) &&
                ReferenceEquals(current, window))
            {
                _windows.Remove(widgetName);
            }

            SetWidgetRunning(widgetName, false);

            if (window.IsVisible)
                window.Close();

            throw;
        }
    }

    private void CloseWidget(string widgetName)
    {
        if (!_windows.TryGetValue(widgetName, out WidgetWindow? window))
            return;

        window.Close();
    }

    private void OnWidgetClosed(string widgetName, WidgetWindow window)
    {
        if (_windows.TryGetValue(widgetName, out WidgetWindow? current) &&
            ReferenceEquals(current, window))
        {
            _windows.Remove(widgetName);
        }

        SetWidgetRunning(widgetName, false);

        if (!_isShuttingDown)
            SaveOpenedWidgets();
    }

    private void SetWidgetRunning(string widgetName, bool running)
    {
        WidgetInfo? info = Widgets.FirstOrDefault(
            item => string.Equals(item.Id, widgetName, StringComparison.OrdinalIgnoreCase)
        );

        if (info != null)
            info.IsRunning = running;
    }

    private void ToggleEditMode()
    {
        if (_isShuttingDown)
            return;

        _onEditMode = !_onEditMode;

        foreach (WidgetWindow window in _windows.Values.ToArray())
            window.ToggleEditMode();

        WidgetBridgeManager.Instance.BroadcastEvent(
            "onEditModeToggle",
            new { enabled = _onEditMode }
        );
    }

    private void SaveOpenedWidgets()
    {
        if (_isShuttingDown)
            return;

        try
        {
            string? directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            List<WidgetWindowState> states = _windows
                .Where(pair => pair.Value.IsVisible)
                .Select(pair => new WidgetWindowState
                {
                    Name = pair.Key,
                    Left = pair.Value.Left,
                    Top = pair.Value.Top
                })
                .ToList();

            string temporaryPath = _statePath + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(states, JsonOptions)
            );

            File.Move(temporaryPath, _statePath, overwrite: true);
        }
        catch (Exception ex)
        {
            LogError("Could not save widget state.", ex);
        }
    }

    private void ShutdownApplication()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        SaveStateForShutdown();

        foreach (WidgetWindow window in _windows.Values.ToArray())
            window.Close();

        _windows.Clear();
        _showKey?.Dispose();
        _showKey = null;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        Application.Current.Shutdown();
    }

    private void SaveStateForShutdown()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_statePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var states = _windows.Select(pair => new WidgetWindowState
            {
                Name = pair.Key,
                Left = pair.Value.Left,
                Top = pair.Value.Top
            }).ToList();

            string temporaryPath = _statePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(states, JsonOptions));
            File.Move(temporaryPath, _statePath, overwrite: true);
        }
        catch (Exception ex)
        {
            LogError("Could not save widget state during shutdown.", ex);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        ShutdownApplication();
    }

    private static void LogError(string message, Exception? exception = null)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), "DesktopWidgets_App.log");
            File.AppendAllText(
                path,
                $"[{DateTime.Now:O}] {message}{Environment.NewLine}{exception}{Environment.NewLine}"
            );
        }
        catch
        {
            // Logging must not interrupt application shutdown.
        }
    }
}
