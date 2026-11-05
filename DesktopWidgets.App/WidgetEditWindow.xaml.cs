using System;
using System.Windows;
using DesktopWidgets.App.Core;

namespace DesktopWidgets.App;

public partial class WidgetEditWindow : Window
{
    private readonly Uri _contentUri;
    private bool _initialized;

    public WidgetEditWindow(Uri contentUri)
    {
        _contentUri = contentUri ?? throw new ArgumentNullException(nameof(contentUri));
        InitializeComponent();
        Loaded += WidgetEditWindow_Loaded;
    }

    private async void WidgetEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
            return;

        try
        {
            await WebView2Runtime.InitializeAsync(Browser);
            Browser.Source = _contentUri;
            _initialized = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.ToString(),
                "Editor initialization error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            Close();
        }
    }
}
