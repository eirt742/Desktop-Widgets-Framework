namespace DesktopWidgets.App.Core;

/// <summary>
/// класс, хранящий настройки виджета, необходимые для его открытия, загружаются из manifest.json
/// </summary>
public class WidgetConfig
{
    public string? widget_name { get; set; }
    public string? widget_description { get; set; }
    public int width { get; set; }
    public int height { get; set; }
}