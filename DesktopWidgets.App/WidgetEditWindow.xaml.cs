using System;
using System.Windows;

namespace DesktopWidgets.App;

public partial class WidgetEditWindow : Window
{
    public WidgetEditWindow(Uri contentUri)
    {
        InitializeComponent();

        Browser.Source = contentUri;
    }
}