using System.Windows;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using DesktopWidgets.App.Core;
using MessageBox = System.Windows.MessageBox;

namespace DesktopWidgets.App;

public partial class MainWindow : Window
{
    public ObservableCollection<WidgetInfo> Widgets { get; set; } = new();
    private List<WidgetWindow>? _windows = new();
    private NotifyIcon? _notifyIcon;
    private GlobalHotkey? _showKey;
    
    public MainWindow()
    {
        InitializeComponent();

        string[] widgetsNames = WidgetData.GetWidgetsNames();

        for (int i = 0; i < widgetsNames.Length; i++)
        {
            WidgetConfig? data = WidgetData.LoadWidgetData(widgetsNames[i]);
            Widgets.Add(new WidgetInfo{
                Name = data.widget_name,
                Description = data.widget_description,
                IsRunning = false
                 });
        }
        
        DataContext = this;
        
        Closed += OnClosed;

        InitTray();
        
        SourceInitialized += (_, _) =>
        {
            var hotkey = new GlobalHotkey(
                this,
                System.Windows.Input.Key.F9,
                System.Windows.Input.ModifierKeys.Control
            );

            hotkey.Pressed += ToggleEditMode;
        };
        LoadOpenedWidgets();
    }

    private void InitTray()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "DesktopWidgets Handler"
        };
        
        _notifyIcon.DoubleClick += (s, e) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };
    }

    private void ToggleEditMode()
    {
        if (_windows == null) return;
        foreach (var w in _windows)
        {
            w.ToggleEditMode();
        }
        SaveOpenedWidgets();
    }
    
    private void CloseToTray_CLick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OpenWidget(object sender, RoutedEventArgs e, string wName)
    {
        if (_windows == null)
            return;

        if (_windows.Any(w => w.WidgetName == wName))
            return;

        var data = WidgetData.LoadWidgetData(wName);

        if (data == null)
        {
            MessageBox.Show(
                $"Не удалось загрузить manifest.json для {wName}\n" +
                $"Путь: {AppContext.BaseDirectory}\\Widgets\\{wName}\\manifest.json"
            );
            return;
        }

        var widgetWindow = new WidgetWindow(wName)
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
        SaveOpenedWidgets();

        var widgetInfo = Widgets.FirstOrDefault(w => w.Name == wName);
        if (widgetInfo != null)
            widgetInfo.IsRunning = true;
    }
    
    private void CloseWidget(object sender, RoutedEventArgs e, string wName)
    {
        if (_windows == null)
            return;

        var widgetWindow = _windows.FirstOrDefault(w => w.WidgetName == wName);

        if (widgetWindow != null)
            widgetWindow.Close();
        SaveOpenedWidgets();
    }
    
    private void WidgetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
            return;

        if (button.DataContext is not WidgetInfo widget)
            return;

        if (widget.IsRunning)
            CloseWidget(sender, e, widget.Name);
        else
            OpenWidget(sender, e, widget.Name);
        
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
     
    private void LoadOpenedWidgets()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopWidgets"
        );

        string path = Path.Combine(folder, "opened-widgets.json");

        if (!File.Exists(path))
            return;

        try
        {
            string json = File.ReadAllText(path);

            var savedWidgets =
                JsonSerializer.Deserialize<List<WidgetWindowState>>(json);

            if (savedWidgets == null)
                return;

            foreach (var saved in savedWidgets)
            {
                var widgetInfo = Widgets.FirstOrDefault(
                    w => w.Name == saved.Name
                );

                
                if (widgetInfo == null)
                    continue;

                WidgetConfig? data = WidgetData.LoadWidgetData(saved.Name);

                if (data != null)
                {
                    var widgetWindow = new WidgetWindow(saved.Name)
                    {
                        Left = saved.Left,
                        Top = saved.Top,
                        Width = data.width,
                        Height = data.height
                    };

                    widgetWindow.Closed += (_, _) =>
                    {
                        _windows?.Remove(widgetWindow);

                        widgetInfo.IsRunning = false;

                        SaveOpenedWidgets();
                    };

                    _windows!.Add(widgetWindow);

                    widgetWindow.Show();
                }

                widgetInfo.IsRunning = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Ошибка загрузки виджетов"
            );
        }
    }
}
