using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace DesktopWidgets.App.Core;

public class WidgetInfo : INotifyPropertyChanged
{
    private bool _isRunning;

    public string? Name { get; set; } = "";
    
    public BitmapImage? Icon { get; set; }

    public string? Description { get; set; } = "";

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (_isRunning == value)
                return;

            _isRunning = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}