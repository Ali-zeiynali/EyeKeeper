using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;

namespace EyeKeeper;

public sealed class Overlay : SafeWindow
{
    public RestVisual Visual { get; }
    readonly Forms.Screen screen;
    public Forms.Screen Screen => screen;
    public Overlay(Forms.Screen monitor, Preferences prefs, int palette, int mode, bool secondary, Action skip, Action snooze)
    {
        screen = monitor;
        Title = "EyeKeeper · " + monitor.DeviceName;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
        ShowInTaskbar = false; ShowActivated = false; Left = -32000; Top = -32000;
        Width = 1; Height = 1;
        Visual = new RestVisual { Palette = palette, MotionStyle = mode, Secondary = secondary, ReducedMotion = prefs.ReducedMotion,
            FlowingBackground = prefs.FlowingBackground, BreathingRing = prefs.BreathingRing };
        Visual.EnableHardware();
        Content = Visual;
        SourceInitialized += (_, _) => { Native.Place(this, screen); Native.ClickThrough(this, true); };
        Loaded += (_, _) => Native.Place(this, screen);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; skip(); }
            if (e.Key == Key.S) { e.Handled = true; snooze(); }
        };
        MouseLeftButtonUp += (_, e) =>
        {
            if (e.GetPosition(this).Y >= ActualHeight - 85 && Math.Abs(e.GetPosition(this).X - ActualWidth / 2) <= 180)
            {
                if (e.GetPosition(this).X < ActualWidth / 2) skip(); else snooze();
            }
        };
    }
    public void EnableRest(bool activate)
    {
        Native.ClickThrough(this, false);
        if (activate) { Activate(); Focus(); }
    }
    public void Dismiss(bool animate)
    {
        if (!animate) { RequestClose(); return; }
        Native.ClickThrough(this, true);
        var animation = Stopwatch.StartNew();
        EventHandler? drain = null;
        drain = (_, _) =>
        {
            Visual.Drain = Math.Clamp(animation.Elapsed.TotalSeconds / 1.05, 0, 1);
            Visual.Time += 1d / 60;
            Visual.InvalidateVisual();
            if (Visual.Drain >= 1) { CompositionTarget.Rendering -= drain; RequestClose(); }
        };
        Closed += (_, _) => CompositionTarget.Rendering -= drain;
        CompositionTarget.Rendering += drain;
    }
}

public sealed class OverlayGroup : IDisposable
{
    readonly List<Overlay> windows = [];
    readonly Preferences prefs;
    readonly BreakEngine engine;
    readonly Stopwatch clock = Stopwatch.StartNew();
    double shownAt, restAt = -1;
    int palette = -1, mode = -1;
    public bool DemoNext { get; set; }
    BitmapSource? fluidFrame;
    Task<BitmapSource>? pendingFrame;
    double frameAt = -1;
    public OverlayGroup(Preferences preferences, BreakEngine timer) { prefs = preferences; engine = timer; }
    public void Synchronize()
    {
        if (engine.Phase is Phase.Working or Phase.Paused) { CloseAll(true); return; }
        if (windows.Count == 0)
        {
            palette = DemoNext ? 0 : Palettes.Choose(prefs.Palette, palette);
            mode = DemoNext ? 1 : FluidStyles.Choose(prefs.MotionStyle, mode);
            DemoNext = false; shownAt = clock.Elapsed.TotalSeconds;
            fluidFrame = FluidEffect.Available ? null : FluidTexture.Create(palette, 0, 480, 300, mode); frameAt = -1; pendingFrame = null;
            var primary = Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];
            var win = new Overlay(primary, prefs, palette, mode, false, engine.Restart, engine.Snooze);
            windows.Add(win); win.Show(); CompositionTarget.Rendering += Render;
        }
        if (engine.Phase == Phase.Resting && restAt < 0)
        {
            restAt = clock.Elapsed.TotalSeconds;
            foreach (var screen in Forms.Screen.AllScreens)
            {
                if (screen.DeviceName == (Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0]).DeviceName) continue;
                var win = new Overlay(screen, prefs, palette, mode, true, engine.Restart, engine.Snooze);
                windows.Add(win); win.Show(); win.EnableRest(false);
            }
            windows[0].EnableRest(true);
        }
    }
    void Render(object? sender, EventArgs args)
    {
        double t = clock.Elapsed.TotalSeconds;
        if (pendingFrame?.IsCompleted == true)
        {
            if (pendingFrame.IsCompletedSuccessfully) fluidFrame = pendingFrame.Result;
            else if (pendingFrame.Exception != null) Storage.Log(pendingFrame.Exception);
            pendingFrame = null;
        }
        if (restAt >= 0 && !FluidEffect.Available && !prefs.ReducedMotion && prefs.FlowingBackground && pendingFrame == null && t - frameAt >= 1d / 24)
        {
            frameAt = t;
            int selected = palette, style = mode; double elapsed = t - shownAt;
            var bounds = Forms.SystemInformation.VirtualScreen;
            int frameWidth = bounds.Width > bounds.Height * 2.3 ? 640 : 480;
            int frameHeight = Math.Clamp((int)(frameWidth * bounds.Height / (double)bounds.Width), 144, 480);
            pendingFrame = Task.Run(() => FluidTexture.Create(selected, elapsed, frameWidth, frameHeight, style));
        }
        var desktop = Forms.SystemInformation.VirtualScreen;
        for (int i = 0; i < windows.Count; i++)
        {
            var win = windows[i]; var v = win.Visual;
            v.FluidFrame = fluidFrame;
            var monitor = win.Screen.Bounds;
            double scale = win.ActualWidth / monitor.Width;
            v.SceneBounds = new Rect((desktop.X - monitor.X) * scale, (desktop.Y - monitor.Y) * scale, desktop.Width * scale, desktop.Height * scale);
            v.CapsuleBounds = new Rect((win.ActualWidth - 300) / 2, 20, 300, 64);
            v.Time = t - shownAt;
            v.Entrance = prefs.ReducedMotion || !prefs.LiquidTransitions ? 1 : 1 - Math.Pow(1 - Math.Clamp((t - shownAt) / .7, 0, 1), 3);
            v.Morph = restAt < 0 ? 0 : prefs.ReducedMotion || !prefs.LiquidTransitions ? 1 : Math.Clamp((t - restAt) / 1.5, 0, 1);
            v.Seconds = engine.Remaining; v.Duration = prefs.RestSeconds;
            v.Progress = engine.RemainingExact / prefs.RestSeconds;
            v.InvalidateVisual();
        }
    }
    public void CloseAll(bool animate = false)
    {
        CompositionTarget.Rendering -= Render;
        foreach (var window in windows) window.Dismiss(animate && prefs.LiquidTransitions && !prefs.ReducedMotion);
        windows.Clear(); restAt = -1;
        fluidFrame = null; pendingFrame = null;
    }
    public void Dispose() => CloseAll();
}
