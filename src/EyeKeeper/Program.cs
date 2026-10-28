using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace EyeKeeper;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
#if SOUND_LAB_BUILD
        if (args.Length == 0) return SoundLab.Run(Path.Combine(AppContext.BaseDirectory, "sound-preview"));
#endif
        // Preview is intentionally isolated from the installed guardian and preferences.
        if (args.Length > 0 && args[0] == "--sound-lab")
            return SoundLab.Run(args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "sound-preview"));
        // Render mode exits before mutex, tray, timer, preferences, startup or any Show() call.
        if (args.Length > 0 && args[0] == "--render-previews")
        {
            try { Preview.Generate(args.Length > 1 ? args[1] : "artifacts/previews"); return 0; }
            catch (Exception ex) { File.WriteAllText("preview-error.log", ex.ToString()); return 1; }
        }
        if (args.Length > 0 && args[0] == "--diagnostics")
        {
            try
            {
                var effect = new FluidEffect(0, 0);
                File.WriteAllText(args.Length > 1 ? args[1] : "diagnostics.json", System.Text.Json.JsonSerializer.Serialize(new
                {
                    RenderingTier = RenderCapability.Tier >> 16,
                    PixelShader3 = RenderCapability.IsPixelShaderVersionSupported(3, 0),
                    HardwareFluidAvailable = FluidEffect.Available,
                    ShaderLoaded = effect.IsLoaded,
                    Note = "No window created. Capability and shader loading check, not an onscreen GPU benchmark."
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }
            catch (Exception ex) { File.WriteAllText("diagnostics-error.log", ex.ToString()); return 1; }
        }
        string mode = args.Length == 0 ? "--background" : args[0];
        if (mode == "--stop") { Supervisor.RequestStop(); return 0; }
        if (mode is "--watchdog" or "--watchdog-demo" or "--watchdog-autostart")
            return Supervisor.Run(mode == "--watchdog-demo", mode == "--watchdog-autostart");
        if (mode is "--background" or "--demo") return Supervisor.Launch(mode == "--demo");
        if (mode is not "--worker" and not "--worker-demo") return 2;
        using var mutex = new Mutex(true, @"Local\EyeKeeper.Desktop.v1", out bool first);
        if (!first) return 0;
        Supervisor.RecordWorker();
        Storage.Event($"Starting {Assembly.GetExecutingAssembly().GetName().Version}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception error) Storage.Log(error);
            Storage.Event("Fatal unhandled exception");
        };
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Exit += (_, e) => Storage.Event($"Application exit, code={e.ApplicationExitCode}");
        using var host = new TrayHost(app);
        app.DispatcherUnhandledException += (_, e) =>
        {
            // Log the original exception before any cleanup can raise a second one.
            Storage.Log(e.Exception); e.Handled = true;
            // Native window callbacks must unwind before closing affected windows.
            app.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                try { host.RecoverUi(); }
                catch (Exception recoveryError) { Storage.Log(recoveryError); app.Shutdown(1); }
            }));
        };
        try { host.Start(mode == "--worker-demo"); return app.Run(); }
        catch (Exception ex) { Storage.Log(ex); return 1; }
    }
}

public sealed class TrayHost : IDisposable
{
    readonly Application app;
    readonly Preferences prefs = Storage.Load();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly BreakEngine engine;
    readonly OverlayGroup overlays;
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    readonly WorkerHeartbeat heartbeat = new();
    Forms.NotifyIcon? tray;
    System.Drawing.Icon? icon;
    SafeWindow? panelWindow;
    ControlPanel? panel;
    double previousTick;
    bool resumeAfterLock, locked, disposed;
    public TrayHost(Application application)
    {
        app = application; engine = new BreakEngine(prefs, () => clock.Elapsed.TotalSeconds);
        overlays = new OverlayGroup(prefs, engine);
        engine.Changed += () =>
        {
            overlays.Synchronize();
            if (engine.Phase == Phase.Resting) panelWindow?.RequestClose();
            panel?.Refresh();
        };
    }
    public void Start(bool demo = false)
    {
        using var embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream("EyeKeeper.Assets.eyekeeper.ico");
        if (embedded != null) icon = new System.Drawing.Icon(embedded);
        else
        {
            using var bitmap = new System.Drawing.Bitmap(32, 32);
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            { g.Clear(System.Drawing.Color.FromArgb(23, 43, 48)); g.DrawEllipse(System.Drawing.Pens.Aquamarine, 3, 9, 26, 14); g.FillEllipse(System.Drawing.Brushes.Aquamarine, 12, 12, 8, 8); }
            var handle = bitmap.GetHicon(); icon = (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone(); Native.DestroyIcon(handle);
        }
        tray = new Forms.NotifyIcon { Icon = icon, Visible = true, Text = "EyeKeeper · a quiet moment for your eyes" };
        tray.MouseClick += (_, e) => { if (e.Button is Forms.MouseButtons.Right or Forms.MouseButtons.Left) app.Dispatcher.Invoke(OpenPanel); };
        SystemEvents.SessionSwitch += SessionSwitch;
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
        try { Storage.Startup(prefs.StartWithWindows); Storage.Save(prefs); }
        catch (Exception ex) { Storage.Log(ex); tray.Text = "EyeKeeper · startup could not be saved; see settings"; }
        timer.Tick += Tick; previousTick = clock.Elapsed.TotalSeconds; timer.Start();
        if (demo) { overlays.DemoNext = true; engine.Demonstrate(); }
    }
    void Tick(object? sender, EventArgs e)
    {
        heartbeat.Beat();
        if (Supervisor.StopRequested) { app.Shutdown(); return; }
        double current = clock.Elapsed.TotalSeconds;
        if (locked) { previousTick = current; return; }
        // A suspended process or sleeping PC must not surprise the user with a stale break.
        if (current - previousTick > 15 && engine.Phase != Phase.Paused) engine.Restart();
        previousTick = current; engine.Tick(); panel?.Refresh();
        if (tray != null) tray.Text = engine.Phase == Phase.Paused ? "EyeKeeper · paused" : $"EyeKeeper · {engine.Phase} · {engine.Remaining / 60:00}:{engine.Remaining % 60:00}";
    }
    void SessionSwitch(object sender, SessionSwitchEventArgs e) => app.Dispatcher.BeginInvoke(() =>
    {
        if (disposed) return;
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            locked = true;
            resumeAfterLock = engine.Phase != Phase.Paused;
            if (resumeAfterLock) engine.Pause();
            panelWindow?.RequestClose();
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            locked = false;
            if (resumeAfterLock) { resumeAfterLock = false; engine.Restart(); }
        }
    });
    void DisplayChanged(object? sender, EventArgs e) => app.Dispatcher.BeginInvoke(() =>
    {
        if (disposed) return;
        overlays.CloseAll(); overlays.Synchronize(); panelWindow?.RequestClose();
    });
    void PowerChanged(object sender, PowerModeChangedEventArgs e) => app.Dispatcher.BeginInvoke(() =>
    {
        if (disposed) return;
        if (e.Mode == PowerModes.Suspend) { overlays.CloseAll(); panelWindow?.RequestClose(); }
        if (e.Mode == PowerModes.Resume && engine.Phase != Phase.Paused) engine.Restart();
    });
    void OpenPanel()
    {
        if (panelWindow != null) { panelWindow.RequestClose(); return; }
        var edit = new Preferences { WorkMinutes = prefs.WorkMinutes, RestSeconds = prefs.RestSeconds, Palette = prefs.Palette,
            MotionStyle = prefs.MotionStyle, StartWithWindows = prefs.StartWithWindows, ReducedMotion = prefs.ReducedMotion,
            FlowingBackground = prefs.FlowingBackground, BreathingRing = prefs.BreathingRing, LiquidTransitions = prefs.LiquidTransitions,
            SoundEnabled = prefs.SoundEnabled, TickSounds = prefs.TickSounds, SoundVolume = prefs.SoundVolume };
        panel = new ControlPanel(edit, engine);
        var win = new SafeWindow { Content = panel, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false, Topmost = true,
            SizeToContent = SizeToContent.WidthAndHeight, Title = "EyeKeeper preferences" };
        panelWindow = win;
        bool showingDialog = false;
        win.SourceInitialized += (_, _) =>
        {
            panel.Measure(new Size(390, double.PositiveInfinity));
            var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
            var source = HwndSource.FromHwnd(new WindowInteropHelper(win).Handle);
            var transform = source.CompositionTarget.TransformFromDevice;
            var area = screen.WorkingArea;
            var corner = transform.Transform(new Point(area.Right, area.Bottom));
            win.Left = corner.X - panel.DesiredSize.Width - 16; win.Top = Math.Max(transform.Transform(new Point(area.Left, area.Top)).Y + 12, corner.Y - panel.DesiredSize.Height - 16);
            win.MaxHeight = area.Height * transform.M22 - 24;
        };
        win.Deactivated += (_, _) => { if (!showingDialog) win.RequestClose(); };
        win.Closed += (_, _) => { panelWindow = null; panel = null; };
        win.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) win.RequestClose(); };
        panel.CloseRequested = win.RequestClose;
        panel.QuitRequested = () =>
        {
            showingDialog = true;
            MessageBoxResult answer;
            try { answer = MessageBox.Show(win, "Quit EyeKeeper completely?\n\nReminders and automatic recovery will stop until you open EyeKeeper again or sign in to Windows.",
                "Quit EyeKeeper", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No); }
            finally { showingDialog = false; }
            if (answer == MessageBoxResult.Yes) { Storage.Event("User confirmed complete Quit"); Supervisor.RequestStop(); app.Shutdown(); }
            else win.Activate();
        };
        panel.SaveRequested = () =>
        {
            try
            {
                edit.Validate(); Storage.Startup(edit.StartWithWindows); Storage.Save(edit);
                prefs.WorkMinutes = edit.WorkMinutes; prefs.RestSeconds = edit.RestSeconds;
                prefs.Palette = edit.Palette;
                prefs.MotionStyle = edit.MotionStyle;
                prefs.FlowingBackground = edit.FlowingBackground; prefs.BreathingRing = edit.BreathingRing; prefs.LiquidTransitions = edit.LiquidTransitions;
                prefs.StartWithWindows = edit.StartWithWindows; prefs.ReducedMotion = edit.ReducedMotion;
                prefs.SoundEnabled = edit.SoundEnabled; prefs.TickSounds = edit.TickSounds; prefs.SoundVolume = edit.SoundVolume;
                overlays.Sound.Reload();
                engine.Restart(); win.RequestClose();
            }
            catch (Exception ex)
            {
                Storage.Log(ex); showingDialog = true;
                try { MessageBox.Show(win, ex.Message, "Could not save preferences", MessageBoxButton.OK, MessageBoxImage.Warning); }
                finally { showingDialog = false; }
            }
        };
        win.Content = new System.Windows.Controls.ScrollViewer { Content = panel, VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled };
        if (!prefs.ReducedMotion)
        {
            win.Opacity = 0;
            panel.RenderTransformOrigin = new Point(.5, 1);
            var scale = new ScaleTransform(.97, .97); panel.RenderTransform = scale;
            var entrance = new DoubleAnimation(.97, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            win.Loaded += (_, _) =>
            {
                win.BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, entrance); scale.BeginAnimation(ScaleTransform.ScaleYProperty, entrance);
            };
        }
        win.Show(); win.Activate();
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; timer.Stop();
        SystemEvents.SessionSwitch -= SessionSwitch;
        SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        SystemEvents.PowerModeChanged -= PowerChanged;
        overlays.Dispose(); panelWindow?.RequestClose();
        if (tray != null) { tray.Visible = false; tray.Dispose(); } icon?.Dispose();
        heartbeat.Dispose();
    }
    public void RecoverUi()
    {
        if (disposed) return;
        overlays.CloseAll(); panelWindow?.RequestClose();
        if (engine.Phase != Phase.Paused) engine.Restart();
        Storage.Event("Recovered UI exception; background scheduler remains active");
    }
}
