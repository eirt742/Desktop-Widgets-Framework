
using System.Windows;

namespace DesktopWidgets.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenClock_Click(
        object sender,
        RoutedEventArgs e)
    {
        var widget = new WidgetWindow();

        widget.Left = 100;
        widget.Top = 100;

        widget.Show();
    }
}
