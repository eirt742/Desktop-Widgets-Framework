
using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Drawing;
using DesktopWidgets.App;
using Microsoft.Web.WebView2.Core;

namespace DesktopWidgets.App;

public partial class WidgetWindow : Window
{
    private WindowInteraction? _interaction;
    private GlobalHotkey? _hotkey;
    private WidgetEditWindow? _editWindow;

    private bool IsEditMode = false;

    public WidgetWindow()
    {
        InitializeComponent();

        Loaded += WidgetWindow_Loaded;

        SourceInitialized += (_, _) =>
        {
            _interaction = new WindowInteraction(this);
            _interaction.SetClickThrough(true);

            _hotkey = new GlobalHotkey(
                this,
                System.Windows.Input.Key.F9,
                System.Windows.Input.ModifierKeys.Control
            );

            _hotkey.Pressed += ToggleEditMode;
        };
    }

    private void ToggleEditMode()
    {
        // Если окно редактирования уже открыто — закрываем его.
        if (_editWindow != null)
        {
            _editWindow.Close();
            return;
        }

        if (Browser.Source == null)
            return;

        IsEditMode = true;

        _interaction?.SetClickThrough(false);

        // Создаём обычное окно редактирования.
        _editWindow = new WidgetEditWindow(Browser.Source)
        {
            Width = Width,
            Height = Height,
            Left = Left,
            Top = Top
        };

        _editWindow.Closed += (_, _) =>
        {
            // Сохраняем новую позицию.
            Left = _editWindow.Left;
            Top = _editWindow.Top;

            _editWindow = null;
            IsEditMode = false;

            // Возвращаем прозрачное окно.
            Show();
            Activate();

            _interaction?.SetClickThrough(true);
        };

        Hide();
        _editWindow.Show();
    }

    private async void WidgetWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();

            Browser.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            string htmlPath = Path.Combine(
                AppContext.BaseDirectory,
                "Widgets",
                "Clock",
                "index.html"
            );

            Browser.Source = new Uri(htmlPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Widget initialization error"
            );
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Закрываем окно редактирования, если оно ещё открыто.
        _editWindow?.Close();

        // Освобождаем глобальную горячую клавишу.
        _hotkey?.Dispose();

        base.OnClosed(e);
    }
}
