
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopWidgets.App;

public sealed class WindowInteraction
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private readonly IntPtr _hwnd;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(
        IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x, int y,
        int cx, int cy,
        uint flags);

    public WindowInteraction(Window window)
    {
        _hwnd = new WindowInteropHelper(window).Handle;

        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Window HWND is not initialized.");
    }

    public void SetClickThrough(bool enabled)
    {
        int style = GetWindowLong(_hwnd, GWL_EXSTYLE);

        if (enabled)
            style |= WS_EX_TRANSPARENT;
        else
            style &= ~WS_EX_TRANSPARENT;

        SetWindowLong(_hwnd, GWL_EXSTYLE, style);

        SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            0, 0, 0, 0,
            SWP_NOMOVE |
            SWP_NOSIZE |
            SWP_NOZORDER |
            SWP_NOACTIVATE |
            SWP_FRAMECHANGED
        );
    }
}
