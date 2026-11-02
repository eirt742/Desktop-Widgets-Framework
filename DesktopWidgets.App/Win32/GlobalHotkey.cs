
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace DesktopWidgets.App;

public sealed class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;

    private static int _nextId = 9000;

    private readonly int _id;
    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;

    private bool _registered;
    private bool _disposed;

    public event Action? Pressed;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(
        IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public GlobalHotkey(Window window, Key key, ModifierKeys modifiers)
    {
        _id = _nextId++;

        _hwnd = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(_hwnd)
            ?? throw new InvalidOperationException("Cannot get window source.");

        _source.AddHook(WndProc);

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        _registered = RegisterHotKey(
            _hwnd,
            _id,
            (uint)modifiers | MOD_NOREPEAT,
            vk
        );

        if (!_registered)
        {
            _source.RemoveHook(WndProc);

            throw new InvalidOperationException(
                $"Failed to register hotkey. Error: {Marshal.GetLastWin32Error()}"
            );
        }
    }

    private IntPtr WndProc(
        IntPtr hwnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
        {
            Pressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _source.RemoveHook(WndProc);

        if (_registered)
            UnregisterHotKey(_hwnd, _id);

        _disposed = true;
    }
}
