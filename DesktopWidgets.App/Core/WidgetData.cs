using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DesktopWidgets.App.Core;

/// <summary>
/// Loads installed widget manifests from their directory.
/// </summary>
public static class WidgetData
{
    private static string WidgetsRoot => Path.Combine(
        AppContext.BaseDirectory,
        "Widgets"
    );

    public static WidgetConfig? LoadWidgetData(string name)
    {
        ValidateWidgetId(name);

        string widgetDirectory = Path.GetFullPath(Path.Combine(WidgetsRoot, name));
        string root = Path.GetFullPath(WidgetsRoot) + Path.DirectorySeparatorChar;

        if (!widgetDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid widget directory.", nameof(name));

        string path = Path.Combine(widgetDirectory, "manifest.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Widget manifest was not found.", path);

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<WidgetConfig>(json)
            ?? throw new InvalidDataException($"Widget manifest '{name}' is empty or invalid.");
    }

    public static string[] GetWidgetsNames()
    {
        if (!Directory.Exists(WidgetsRoot))
            return Array.Empty<string>();

        return Directory.GetDirectories(WidgetsRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
    }

    private static void ValidateWidgetId(string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains(Path.DirectorySeparatorChar) ||
            name.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Invalid widget identifier.", nameof(name));
        }
    }
}
