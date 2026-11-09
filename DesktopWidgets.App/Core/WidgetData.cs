using System;
using System.IO;
using System.Text.Json;

namespace DesktopWidgets.App.Core
{
    /// <summary>
    /// загружает данные о подключённых виджетах
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

            res?.widget_name = name;

            return res;
        }

        public static string[] GetWidgetsNames()
        {
            string widgetsPath = Path.Combine(
                AppContext.BaseDirectory,
                "Widgets"
            );

            return
            [
                .. Directory.GetDirectories(widgetsPath)
                    .Select(Path.GetFileName)
            ];
        }
    }
}