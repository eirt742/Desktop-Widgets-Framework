using System;
using System.Runtime.InteropServices;

namespace DesktopWidgets
{
    public static class WindowVisibilityHelper
    {
        private const int GWL_EXSTYLE = -20;

        private const long WS_EX_TOOLWINDOW = 0x00000080L;
        private const long WS_EX_APPWINDOW = 0x00040000L;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(
            IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(
            IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(
            IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(
            IntPtr hWnd, int nIndex, int dwNewLong);

        public static void HideFromAltTab(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return;

            long exStyle = IntPtr.Size == 8
                ? GetWindowLongPtr64(hwnd, GWL_EXSTYLE).ToInt64()
                : GetWindowLong32(hwnd, GWL_EXSTYLE);

            exStyle |= WS_EX_TOOLWINDOW;
            exStyle &= ~WS_EX_APPWINDOW;

            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr64(
                    hwnd,
                    GWL_EXSTYLE,
                    new IntPtr(exStyle));
            }
            else
            {
                SetWindowLong32(
                    hwnd,
                    GWL_EXSTYLE,
                    unchecked((int)exStyle));
            }
        }
    }
}