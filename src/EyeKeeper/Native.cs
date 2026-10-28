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
        SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0037); // Apply extended-style changes without moving/activating.
    }
}

// Registered only while the reminder is visible, and only once for all displays.
// WM_HOTKEY arrives even when the click-through overlay cannot take keyboard focus.
public sealed class EscapeShortcut : IDisposable
{
    public const int Id = 0x4E51;
    public bool IsRegistered { get; }
    readonly HwndSource source;
    readonly IntPtr handle;
    readonly Window owner;
    readonly Action cancel;
    bool disposed, pending;
    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window, int id);
    public EscapeShortcut(Window window, Action action)
    {
        owner = window; cancel = action;
        handle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("Create the window before registering Escape.");
        IsRegistered = RegisterHotKey(handle, Id, 0x4000, 0x1B);
        if (IsRegistered) source.AddHook(Receive);
        else Storage.Log(new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Could not register the temporary Escape shortcut."));
    }
    IntPtr Receive(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt32() == Id && !disposed)
        {
            handled = true;
            if (!pending)
            {
                pending = true;
                owner.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
                {
                    if (!disposed) cancel();
                }));
            }
        }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        if (IsRegistered)
        {
            UnregisterHotKey(handle, Id);
            if (!source.IsDisposed) source.RemoveHook(Receive);
        }
    }
}
