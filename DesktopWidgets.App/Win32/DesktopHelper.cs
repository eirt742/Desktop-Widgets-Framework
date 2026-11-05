
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopWidgets.App.Win32;

internal static class DesktopHelper
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;

    private const long WS_CHILD = 0x40000000L;
    private const long WS_POPUP = unchecked((int)0x80000000);
    private const long WS_EX_TRANSPARENT = 0x00000020L;

    private const int GW_HWNDNEXT = 2;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private const uint SMTO_NORMAL = 0;
    private const uint WM_SPAWN_WORKER = 0x052C;

    private static readonly string LogPath =
        Path.Combine(
            Path.GetTempPath(),
            "DesktopWidgets_WorkerW.log"
        );

    private delegate bool EnumWindowsProc(
        IntPtr hwnd,
        IntPtr lParam
    );

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // =========================
    // Win32 imports
    // =========================

    [DllImport("user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern IntPtr FindWindow(
        string? className,
        string? windowName
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hwnd,
        uint msg,
        UIntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result
    );

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(
        EnumWindowsProc callback,
        IntPtr lParam
    );

    [DllImport("user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern IntPtr FindWindowEx(
        IntPtr parent,
        IntPtr childAfter,
        string? className,
        string? windowName
    );

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(
        IntPtr hwnd,
        int command
    );

    [DllImport("user32.dll",
        CharSet = CharSet.Unicode)]
    private static extern int GetClassName(
        IntPtr hwnd,
        StringBuilder name,
        int maxCount
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(
        IntPtr hwnd,
        out RECT rect
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClientToScreen(
        IntPtr hwnd,
        ref POINT point
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ScreenToClient(
        IntPtr hwnd,
        ref POINT point
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(
        IntPtr child,
        IntPtr newParent
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags
    );

    [DllImport("user32.dll",
        EntryPoint = "GetWindowLongPtrW",
        SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(
        IntPtr hwnd,
        int index
    );

    [DllImport("user32.dll",
        EntryPoint = "SetWindowLongPtrW",
        SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(
        IntPtr hwnd,
        int index,
        IntPtr value
    );

    [DllImport("user32.dll",
        EntryPoint = "GetWindowLongW",
        SetLastError = true)]
    private static extern int GetWindowLong32(
        IntPtr hwnd,
        int index
    );

    [DllImport("user32.dll",
        EntryPoint = "SetWindowLongW",
        SetLastError = true)]
    private static extern int SetWindowLong32(
        IntPtr hwnd,
        int index,
        int value
    );

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(
        IntPtr hwnd
    );

    // =========================
    // Style helpers
    // =========================

    private static IntPtr GetWindowStyle(
        IntPtr hwnd,
        int index)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, index)
            : new IntPtr(GetWindowLong32(hwnd, index));
    }

    private static void SetWindowStyle(
        IntPtr hwnd,
        int index,
        IntPtr style)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hwnd, index, style);
        else
            SetWindowLong32(hwnd, index, style.ToInt32());
    }

    // =========================
    // Logging
    // =========================

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(
                LogPath,
                $"[{DateTime.Now:O}] {message}{Environment.NewLine}"
            );
        }
        catch
        {
        }
    }

    // =========================
    // Attach widget to desktop
    // =========================

    public static bool AttachToDesktop(IntPtr hwnd)
    {
        Log("---- AttachToDesktop started ----");

        if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
        {
            Log("FAIL: invalid widget HWND.");
            return false;
        }

        IntPtr progman = FindWindow("Progman", null);

        if (progman == IntPtr.Zero)
        {
            Log("FAIL: Progman not found.");
            return false;
        }

        SendMessageTimeout(
            progman,
            WM_SPAWN_WORKER,
            UIntPtr.Zero,
            IntPtr.Zero,
            SMTO_NORMAL,
            1000,
            out _
        );

        SendMessageTimeout(
            progman,
            WM_SPAWN_WORKER,
            (UIntPtr)0xD,
            (IntPtr)0x1,
            SMTO_NORMAL,
            1000,
            out _
        );

        IntPtr parent = FindDesktopWorkerW();

        if (parent == IntPtr.Zero)
        {
            Log("FAIL: WorkerW not found.");
            DumpDesktopWindows();
            return false;
        }

        if (!GetWindowRect(hwnd, out RECT widgetRect))
        {
            Log($"FAIL: GetWindowRect: {Marshal.GetLastWin32Error()}");
            return false;
        }

        POINT parentOrigin = new()
        {
            X = 0,
            Y = 0
        };

        if (!ClientToScreen(parent, ref parentOrigin))
        {
            Log($"FAIL: ClientToScreen: {Marshal.GetLastWin32Error()}");
            return false;
        }

        int x = widgetRect.Left - parentOrigin.X;
        int y = widgetRect.Top - parentOrigin.Y;

        IntPtr oldParent = GetParent(hwnd);

        IntPtr oldStyle =
            GetWindowStyle(hwnd, GWL_STYLE);

        IntPtr oldExStyle =
            GetWindowStyle(hwnd, GWL_EXSTYLE);

        long newStyle =
            (oldStyle.ToInt64() & ~WS_POPUP) | WS_CHILD;

        long newExStyle =
            oldExStyle.ToInt64() & ~WS_EX_TRANSPARENT;

        // Change normal window style.
        Marshal.SetLastPInvokeError(0);

        SetWindowStyle(
            hwnd,
            GWL_STYLE,
            new IntPtr(newStyle)
        );

        int styleError = Marshal.GetLastWin32Error();

        // Remove click-through extended style.
        Marshal.SetLastPInvokeError(0);

        SetWindowStyle(
            hwnd,
            GWL_EXSTYLE,
            new IntPtr(newExStyle)
        );

        int exStyleError = Marshal.GetLastWin32Error();

        if (styleError != 0 || exStyleError != 0)
        {
            SetWindowStyle(hwnd, GWL_STYLE, oldStyle);
            SetWindowStyle(hwnd, GWL_EXSTYLE, oldExStyle);

            Log($"FAIL: style update. Style={styleError}, ExStyle={exStyleError}");
            return false;
        }

        // Change parent.
        Marshal.SetLastPInvokeError(0);

        IntPtr previousParent = SetParent(hwnd, parent);

        int parentError = Marshal.GetLastWin32Error();

        if (previousParent == IntPtr.Zero && parentError != 0)
        {
            SetWindowStyle(hwnd, GWL_STYLE, oldStyle);
            SetWindowStyle(hwnd, GWL_EXSTYLE, oldExStyle);

            Log($"FAIL: SetParent: {parentError}");
            return false;
        }

        // Keep original dimensions.
        bool positioned = SetWindowPos(
            hwnd,
            IntPtr.Zero,
            x,
            y,
            0,
            0,
            SWP_NOSIZE |
            SWP_NOZORDER |
            SWP_NOACTIVATE |
            SWP_FRAMECHANGED |
            SWP_SHOWWINDOW
        );

        if (!positioned)
        {
            int error = Marshal.GetLastWin32Error();

            SetParent(hwnd, oldParent);
            SetWindowStyle(hwnd, GWL_STYLE, oldStyle);
            SetWindowStyle(hwnd, GWL_EXSTYLE, oldExStyle);

            Log($"FAIL: SetWindowPos: {error}");
            return false;
        }

        Log(
            $"SUCCESS: parent=0x{parent.ToInt64():X}, " +
            $"position=({x},{y}), " +
            $"style=0x{newStyle:X}, " +
            $"exStyle=0x{newExStyle:X}"
        );

        return true;
    }

    // =========================
    // Move widget
    // =========================

    public static bool MoveToScreenPosition(
        IntPtr hwnd,
        int screenX,
        int screenY)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            return false;

        IntPtr parent = GetParent(hwnd);

        if (parent == IntPtr.Zero)
            return false;

        POINT point = new()
        {
            X = screenX,
            Y = screenY
        };

        if (!ScreenToClient(parent, ref point))
            return false;

        return SetWindowPos(
            hwnd,
            IntPtr.Zero,
            point.X,
            point.Y,
            0,
            0,
            SWP_NOSIZE |
            SWP_NOZORDER |
            SWP_NOACTIVATE |
            SWP_SHOWWINDOW
        );
    }

    // =========================
    // Find WorkerW
    // =========================

    private static IntPtr FindDesktopWorkerW()
    {
        IntPtr progman = FindWindow("Progman", null);

        if (progman == IntPtr.Zero)
            return IntPtr.Zero;

        IntPtr child = IntPtr.Zero;

        while ((child = FindWindowEx(
            progman,
            child,
            "WorkerW",
            null)) != IntPtr.Zero)
        {
            if (!GetWindowRect(child, out RECT rect))
                continue;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width > 0 && height > 0)
            {
                Log(
                    $"Candidate direct Progman child WorkerW=0x{child.ToInt64():X}"
                );

                return child;
            }
        }

        // Classic WorkerW layout fallback.
        IntPtr found = IntPtr.Zero;

        EnumWindows((top, _) =>
        {
            IntPtr view = FindWindowEx(
                top,
                IntPtr.Zero,
                "SHELLDLL_DefView",
                null
            );

            if (view == IntPtr.Zero)
                return true;

            IntPtr next = GetWindow(top, GW_HWNDNEXT);

            while (next != IntPtr.Zero)
            {
                StringBuilder className = new(128);

                GetClassName(
                    next,
                    className,
                    className.Capacity
                );

                if (className.ToString() == "WorkerW")
                {
                    if (GetWindowRect(next, out RECT rect) &&
                        rect.Right > rect.Left &&
                        rect.Bottom > rect.Top)
                    {
                        found = next;
                        return false;
                    }
                }

                next = GetWindow(next, GW_HWNDNEXT);
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    // =========================
    // Diagnostics
    // =========================

    private static void DumpDesktopWindows()
    {
        IntPtr progman = FindWindow("Progman", null);

        Log($"Desktop dump. Progman=0x{progman.ToInt64():X}");

        EnumWindows((top, _) =>
        {
            StringBuilder className = new(128);

            GetClassName(
                top,
                className,
                className.Capacity
            );

            string name = className.ToString();

            IntPtr view = FindWindowEx(
                top,
                IntPtr.Zero,
                "SHELLDLL_DefView",
                null
            );

            if (name.Contains("WorkerW",
                    StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Progman",
                    StringComparison.OrdinalIgnoreCase) ||
                view != IntPtr.Zero)
            {
                Log(
                    $"Top HWND=0x{top.ToInt64():X}, " +
                    $"class={name}, " +
                    $"DefView=0x{view.ToInt64():X}"
                );
            }

            return true;
        }, IntPtr.Zero);
    }
}
