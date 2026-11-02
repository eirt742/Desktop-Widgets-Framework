using System;
using System.IO;
using System.Windows;
using System.Drawing;
using Microsoft.Web.WebView2.Core;

namespace DesktopWidgets.App;

public partial class WidgetWindow : Window
{
    private WindowInteraction? _interaction;
    private GlobalHotkey? _hotkey;
    bool IsEditMode = false;
    public WidgetWindow()
    {
        InitializeComponent();

        Loaded += WidgetWindow_Loaded;
        SourceInitialized += (_, _) =>
        {
            _interaction = new WindowInteraction(this);

            _interaction.SetClickThrough(true);

           _hotkey = new GlobalHotkey(this, System.Windows.Input.Key.F9, System.Windows.Input.ModifierKeys.Control);

        _hotkey.Pressed += ToggleEditMode;
        };
    }

    
    private void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;

        EditModeBorder.Visibility = IsEditMode
            ? Visibility.Visible
            : Visibility.Collapsed;
        _interaction?.SetClickThrough(!IsEditMode);
    }


    private async void WidgetWindow_Loaded(
    object sender,
    RoutedEventArgs e)
{
    try
    {
        await Browser.EnsureCoreWebView2Async();

        Browser.DefaultBackgroundColor = Color.Transparent;

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
}
