
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace DesktopWidgets.App.Core
{
    /// <summary>
    /// Загружает данные и иконки установленных виджетов.
    /// </summary>
    public static class WidgetData
    {
        public static WidgetConfig? LoadWidgetData(string name)
        {
            string path = Path.Combine(
                AppContext.BaseDirectory,
                "Widgets",
                name,
                "manifest.json"
            );

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Manifest не найден",
                    path
                );
            }

            string json = File.ReadAllText(path);

            var res = JsonSerializer.Deserialize<WidgetConfig>(json);

            if (res != null)
                res.widget_name = name;

            return res;
        }

        /// <summary>
        /// Загружает иконку виджета по имени его папки.
        /// </summary>
        public static BitmapImage? LoadWidgetIcon(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            string widgetDirectory = Path.Combine(
                AppContext.BaseDirectory,
                "Widgets",
                name
            );

            if (!Directory.Exists(widgetDirectory))
                return null;

            // Возможные названия иконок
            string[] iconNames =
            [
                "icon.png",
                "icon.jpg",
                "icon.jpeg",
                "icon.bmp",
                "icon.gif",
                "icon.ico",

                $"{name}.png",
                $"{name}.jpg",
                $"{name}.jpeg",
                $"{name}.bmp",
                $"{name}.gif",
                $"{name}.ico"
            ];

            foreach (string iconName in iconNames.Distinct(
                         StringComparer.OrdinalIgnoreCase))
            {
                string iconPath = Path.Combine(
                    widgetDirectory,
                    iconName
                );

                if (!File.Exists(iconPath))
                    continue;

                try
                {
                    var bitmap = new BitmapImage();

                    bitmap.BeginInit();

                    bitmap.CacheOption = BitmapCacheOption.OnLoad;

                    bitmap.UriSource = new Uri(
                        iconPath,
                        UriKind.Absolute
                    );

                    bitmap.EndInit();

                    if (bitmap.CanFreeze)
                        bitmap.Freeze();

                    return bitmap;
                }
                catch (Exception)
                {
                    // Если иконка повреждена,
                    // пробуем следующий файл.
                }
            }

            return null;
        }

        public static string[] GetWidgetsNames()
        {
            var widgetsPath = GetWidgetsPath();

            if (!Directory.Exists(widgetsPath))
                return [];

            return
            [
                .. Directory.GetDirectories(widgetsPath)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
            ];
        }
        
        public static string GetWidgetsPath() => Path.Combine(
            AppContext.BaseDirectory,
            "Widgets"
        );
    }
}
