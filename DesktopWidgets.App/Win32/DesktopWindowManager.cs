
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace DesktopWidgets.App.Win32;

public static class DesktopWindowManager
{
    private const uint WorkerWMessage = 0x052C;
    private const uint SendTimeout = 0x0002;

    private const int GW_HWNDNEXT = 2;

    private const int GWL_STYLE = -16;

    private const long WS_CHILD = 0x40000000L;
    private const long WS_POPUP = unchecked((int)0x80000000);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private static readonly IntPtr HWND_BOTTOM = new(1);

    private static IntPtr _workerW;

    public static bool Attach(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;

        if (hwnd == IntPtr.Zero)
            return false;

        IntPtr worker = FindWorkerW();

        if (worker == IntPtr.Zero)
            return false;

        // Сохраняем текущие стили окна.
        long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();

        // Превращаем обычное popup-окно в дочернее окно WorkerW.
        style &= ~WS_POPUP;
        style |= WS_CHILD;

        SetWindowLongPtr(
            hwnd,
            GWL_STYLE,
            new IntPtr(style)
        );

        SetParent(hwnd, worker);

        SetWindowPos(
            hwnd,
            HWND_BOTTOM,
            0, 0, 0, 0,
            SWP_NOMOVE |
            SWP_NOSIZE |
            SWP_NOZORDER |
            SWP_FRAMECHANGED
        );

        _workerW = worker;

        return true;
    }

    
    private static IntPtr FindWorkerW()
    {
        IntPtr progman = FindWindow("Progman", null);

        if (progman == IntPtr.Zero)
            return IntPtr.Zero;

        // Windows 11: просим Explorer создать фоновый WorkerW.
        SendMessageTimeout(
            progman,
            0x052C,
            new IntPtr(0xD),
            new IntPtr(0x1),
            0,
            1000,
            out _
        );

        // Ищем WorkerW непосредственно внутри Progman.
        IntPtr worker = FindWindowEx(
            progman,
            IntPtr.Zero,
            "WorkerW",
            null
        );

        if (worker != IntPtr.Zero)
            return worker;

        // Дополнительная проверка: ищем WorkerW среди окон,
        // связанных с окном рабочего стола.
        IntPtr shellView = FindWindowEx(
            progman,
            IntPtr.Zero,
            "SHELLDLL_DefView",
            null
        );

        if (shellView != IntPtr.Zero)
        {
            worker = FindWindowEx(
                progman,
                shellView,
                "WorkerW",
                null
            );

            if (worker != IntPtr.Zero)
                return worker;
        }

        return IntPtr.Zero;
    }


    private delegate bool EnumWindowsProc(
        IntPtr hwnd,
        IntPtr lParam
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(
        string? className,
        string? windowName
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(
        IntPtr parent,
        IntPtr childAfter,
        string? className,
        string? windowName
    );

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(
        EnumWindowsProc callback,
        IntPtr lParam
    );

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(
        IntPtr hwnd,
        int command
    );

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(
        IntPtr hwnd,
        StringBuilder className,
        int maxCount
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hwnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(
        IntPtr child,
        IntPtr newParent
    );

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(
        IntPtr hwnd,
        int index
    );

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(
        IntPtr hwnd,
        int index,
        IntPtr value
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags
    );
}
