
using System;
using System.Runtime.InteropServices;
using System.Text;

class Program
{
    // Callback для EnumWindows
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // Перечисление всех верхнеуровневых окон
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(
        EnumWindowsProc callback,
        IntPtr lParam
    );

    // Получение заголовка окна
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(
        IntPtr hWnd,
        StringBuilder text,
        int maxCount
    );

    // Получение имени класса окна
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(
        IntPtr hWnd,
        StringBuilder className,
        int maxCount
    );

    // Получение родительского окна
    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    // Поиск окна по имени класса и заголовку
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(
        string? lpClassName,
        string? lpWindowName
    );

    // Перечисление дочерних окон
    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(
        IntPtr hWndParent,
        EnumWindowsProc callback,
        IntPtr lParam
    );

    private static bool EvaluateWindow(IntPtr hWnd, IntPtr lParam)
    {
        StringBuilder title = new StringBuilder(256);
        StringBuilder className = new StringBuilder(256);

        GetWindowText(hWnd, title, title.Capacity);
        GetClassName(hWnd, className, className.Capacity);

        IntPtr parent = GetParent(hWnd);

        Console.WriteLine("------------------------------------");
        Console.WriteLine($"HWND:      {hWnd}");
        Console.WriteLine($"Title:     {title}");
        Console.WriteLine($"Class:     {className}");
        Console.WriteLine($"Parent:    {parent}");

        return true;
    }

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("=== WINDOWS WINDOW INSPECTOR ===");
        Console.WriteLine();

        Console.WriteLine("=== TOP LEVEL WINDOWS ===");

        EnumWindowsProc callback = EvaluateWindow;
        EnumWindows(callback, IntPtr.Zero);

        Console.WriteLine();
        Console.WriteLine("=== IMPORTANT WINDOWS ===");

        string[] classes =
        {
            "Progman",
            "WorkerW",
            "SHELLDLL_DefView"
        };

        foreach (string className in classes)
        {
            IntPtr hWnd = FindWindow(className, null);

            Console.WriteLine($"{className}: {hWnd}");
        }

        Console.WriteLine();
        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
    }
}
