using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace EyeKeeper;

public static class Native
{
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
    public static void Place(Window window, Forms.Screen screen)
    {
        var r = screen.Bounds;
        SetWindowPos(new WindowInteropHelper(window).Handle, new IntPtr(-1), r.X, r.Y, r.Width, r.Height, 0x0010);
    }
    public static void ClickThrough(Window window, bool enabled)
    {
        var h = new WindowInteropHelper(window).Handle;
        long style = GetWindowLongPtr(h, -20).ToInt64();
        const long transparent = 0x20, noActivate = 0x08000000, toolWindow = 0x80;
        style = enabled ? style | transparent | noActivate | toolWindow : (style & ~transparent & ~noActivate) | toolWindow;
        SetWindowLongPtr(h, -20, new IntPtr(style));
    }
}
