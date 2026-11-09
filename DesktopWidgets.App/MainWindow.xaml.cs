using System.Windows;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using DesktopWidgets.App.API;
using DesktopWidgets.App.Core;
using DesktopWidgets.App.Win32;
using MessageBox = System.Windows.MessageBox;

namespace DesktopWidgets.App;

public partial class MainWindow : Window
{
    public ObservableCollection<WidgetInfo> Widgets { get; set; } = [];
    private readonly List<WidgetWindow>? _windows = [];
    private NotifyIcon? _notifyIcon;
    private GlobalHotkey? _showKey;

    private bool _onEditMode;
    
    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;

        LoadInstalledWidgets();

        Closed += OnClosed;
        InitTray();

        SourceInitialized += (_, _) =>
        {
            _showKey = new GlobalHotkey(
                this,
                System.Windows.Input.Key.F9,
                System.Windows.Input.ModifierKeys.Control
            );

            _showKey.Pressed += ToggleEditMode;
        };

        Loaded += MainWindow_Loaded;
    }

    private void LoadInstalledWidgets()
    {
        var runningStates = new Dictionary<string, bool>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var widget in Widgets)
        {
            if (!string.IsNullOrWhiteSpace(widget.Name))
            {
                runningStates[widget.Name] = widget.IsRunning;
            }
        }

        Widgets.Clear();

        string[] widgetNames = WidgetData.GetWidgetsNames();

        foreach (var widgetName in widgetNames)
        {
            WidgetConfig? data = WidgetData.LoadWidgetData(widgetName);

            if (data is null)
                continue;

            string displayName = string.IsNullOrWhiteSpace(data.widget_name)
                ? widgetName
                : data.widget_name;

            bool isRunning = runningStates.TryGetValue(
                displayName,
                out bool previousState
            ) && previousState;

            Widgets.Add(new WidgetInfo
            {
                Name = string.IsNullOrWhiteSpace(data.widget_name)
                    ? widgetName
                    : data.widget_name,

                Description = data.widget_description ?? "Описание отсутствует",

                Icon = WidgetData.LoadWidgetIcon(widgetName),

                IsRunning = isRunning
            });
        }
    }

    private void RefreshWidgets_Clicked(object sender, RoutedEventArgs a)
    {
        LoadInstalledWidgets();

        // Обновляем отображение с учётом текущего поиска
        if (FindResource("WidgetsView") is CollectionViewSource source)
        {
            source.View?.Refresh();
        }
    }
    
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await RestoreOpenedWidgetsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private async Task RestoreOpenedWidgetsAsync()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopWidgets"
        );
        string path = Path.Combine(folder, "opened-widgets.json");

        if (!File.Exists(path))
            return;

        List<WidgetWindowState>? savedWidgets;
        try
        {
            string json = await File.ReadAllTextAsync(path);
            savedWidgets = JsonSerializer.Deserialize<List<WidgetWindowState>>(json);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Не удалось прочитать состояние виджетов");
            return;
        }

        if (savedWidgets == null || _windows == null)
            return;

        foreach (WidgetWindowState saved in savedWidgets)
        {
            if (string.IsNullOrWhiteSpace(saved.Name))
                continue;

            try
            {
                OpenWidget(saved.Name, false);

                WidgetWindow? window = GetWidgetByName(saved.Name);
                if (window == null)
                    continue;

                await window.InitializationCompleted;

                window.Left = saved.Left;
                window.Top = saved.Top;

                var hwnd = new WindowInteropHelper(window).Handle;

                DesktopHelper.MoveToScreenPosition(
                    hwnd,
                    (int)Math.Round(saved.Left),
                    (int)Math.Round(saved.Top)
                );

                WidgetInfo? info = Widgets.FirstOrDefault(w => w.Name == saved.Name);
                if (info != null)
                    info.IsRunning = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    $"Не удалось восстановить виджет: {saved.Name}"
                );
            }
        }

        SaveOpenedWidgets();
    }
    private void InitTray()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = new Icon(Path.Combine(
                    AppContext.BaseDirectory,
                    "Resources",
                    "app.ico")),
            Visible = true,
            Text = "DesktopWidgets Handler"
        };

        var menu = new ContextMenuStrip();

        menu.Items.Add("Открыть", null, (_, _) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
        
        menu.Items.Add("Выход", null, (_, _) =>
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        });

        _notifyIcon.ContextMenuStrip = menu;
        
        _notifyIcon.DoubleClick += (_, _) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };
    }

    public void ToggleEditMode()
    {
        if (_windows == null) return;
        _onEditMode = !_onEditMode;
        foreach (var w in _windows)
        {
            w.ToggleEditMode();
        }
        WidgetBridgeManager.Instance.BroadcastEvent("onEditModeToggle", new {enabled = _onEditMode});
        SaveOpenedWidgets();
    }
    
    private void CloseToTray_CLick(object sender, RoutedEventArgs e)
    {
        Hide();
    }
    
    private void WidgetSearchBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (FindResource("WidgetsView") is CollectionViewSource source)
        {
            source.View?.Refresh();
        }
    }

    private void WidgetsView_Filter(
        object sender,
        FilterEventArgs e)
    {
        if (e.Item is not WidgetInfo widget)
        {
            e.Accepted = false;
            return;
        }

        string search = WidgetSearchBox?.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(search))
        {
            e.Accepted = true;
            return;
        }

        e.Accepted =
            (!string.IsNullOrEmpty(widget.Name) &&
             widget.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            ||
            (!string.IsNullOrEmpty(widget.Description) &&
             widget.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
    }


    private void OpenWidget(string wName, bool save = true)
    {
        if (_windows == null)
            return;

        if (_windows.Any(w => w.WidgetName == wName))
            return;

        try
        {
            var data = WidgetData.LoadWidgetData(wName);

            if (data == null)
            {
                MessageBox.Show(
                    $"Не удалось загрузить manifest.json для {wName}\n" +
                    $"Путь: {AppContext.BaseDirectory}\\Widgets\\{wName}\\manifest.json"
                );
                return;
            }

            var widgetWindow = new WidgetWindow(wName, this)
            {
                Left = 100,
                Top = 100,
                Width = data.width,
                Height = data.height
            };

            widgetWindow.Closed += (_, _) =>
            {
                _windows.Remove(widgetWindow);

                var info = Widgets.FirstOrDefault(w => w.Name == wName);
                if (info != null)
                    info.IsRunning = false;
            };

            _windows.Add(widgetWindow);
            widgetWindow.Show();
            if (save) SaveOpenedWidgets();

            var widgetInfo = Widgets.FirstOrDefault(w => w.Name == wName);
            if (widgetInfo != null)
                widgetInfo.IsRunning = true;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message);
            throw;
        }
    }
    
    private void CloseWidget(string wName, bool save = true)
    {
        if (_windows == null)
            return;

        var widgetWindow = _windows.FirstOrDefault(w => w.WidgetName == wName);

        if (widgetWindow != null)
            widgetWindow.Close();
        if (save) SaveOpenedWidgets();
    }
    
    private void WidgetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        if (button.DataContext is not WidgetInfo widget)
        {
            MessageBox.Show("DataContext кнопки не является WidgetInfo.");
            return;
        }

        if (string.IsNullOrWhiteSpace(widget.Name))
        {
            MessageBox.Show("Имя виджета пустое.");
            return;
        }

        if (widget.IsRunning)
            CloseWidget(widget.Name);
        else
            OpenWidget(widget.Name);
    }
    
    private void OnClosed(object? sender, EventArgs e)
    {
        if (_windows == null) return;

        SaveOpenedWidgets();

        var windows = _windows.ToList();

        foreach (var w in windows)
        {
            w.Close();
        }

        _windows.Clear();

        _notifyIcon?.Dispose();
        _showKey?.Dispose();

        _showKey = null;
    }

    private WidgetWindow? GetWidgetByName(string name) => _windows?.FirstOrDefault(w => w.WidgetName == name);
    
    
    private void SaveOpenedWidgets()
    {
        if (_windows == null)
            return;

        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopWidgets"
        );

        Directory.CreateDirectory(folder);

        string path = Path.Combine(folder, "opened-widgets.json");

        var openedWidgets = _windows.Select(w => new WidgetWindowState
        {
            Name = w.WidgetName,
            Left = w.Left,
            Top = w.Top
        }).ToList();

        string json = JsonSerializer.Serialize(
            openedWidgets,
            new JsonSerializerOptions { WriteIndented = true }
        );

        File.WriteAllText(path, json);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
    
    private void MinimizeWindow_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
    
    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Данная функция пока не доступана!");
    }
    
    private void AddWidget_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Данная функция пока не доступана!");
    }

    private void OpenWidgetsFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = WidgetData.GetWidgetsPath();
        Process.Start("explorer.exe", path);
    }
}
