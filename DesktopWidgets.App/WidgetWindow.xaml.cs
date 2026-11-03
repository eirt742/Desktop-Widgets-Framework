using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace DesktopWidgets.App;

public partial class WidgetWindow : Window
{
    private WidgetEditWindow? _editWindow;
    private readonly string _widgetName;
    public string WidgetName => _widgetName;


    public WidgetWindow(string widgetName)
    {
        _widgetName = widgetName;
        InitializeComponent();

        Loaded += WidgetWindow_Loaded;
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
            Left = _editWindow.Left;
            Top = _editWindow.Top;
        
            _editWindow = null;
        
            Show();
            Activate();
        };
        
        Hide();
        _editWindow.Show();
    }

    private async void WidgetWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await Browser.EnsureCoreWebView2Async();

            Browser.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            string htmlPath = Path.Combine(
                AppContext.BaseDirectory,
                "Widgets",
                _widgetName,
                "web",
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
        _editWindow?.Close();
        base.OnClosed(e);
    }
}
