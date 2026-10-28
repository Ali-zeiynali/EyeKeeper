using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace EyeKeeper;

public static class SoundLab
{
    public static int Run(string folder)
    {
        using var mutex = new Mutex(true, @"Local\EyeKeeper.SoundLab.v1", out bool first);
        if (!first) return 0;
        Directory.CreateDirectory(folder); SoundDesign.Export(folder);
        var clock = Stopwatch.StartNew();
        void Log(string text) => File.AppendAllText(Path.Combine(folder, "events.log"), $"{clock.Elapsed.TotalSeconds:F3} {text}\n");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var prefs = new Preferences { RestSeconds = 12, SoundVolume = .36, Palette = "Aurora", MotionStyle = "Silk", StartWithWindows = false };
        var engine = new BreakEngine(prefs, () => clock.Elapsed.TotalSeconds);
        using var overlays = new OverlayGroup(prefs, engine);
        overlays.Sound.Played = cue => Log($"AUDIO {cue}");
        var window = new SafeWindow
        {
            Title = "EyeKeeper · Sound & pulse preview",
            Width = 480, Height = 480, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = Art.Brush("#101927"), Foreground = Art.Brush("#EDF6FA")
        };
        var root = new StackPanel { Margin = new Thickness(26) };
        window.Content = root;
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new LogoElement { Width = 40, Height = 40, Margin = new Thickness(0, 0, 12, 0) });
        header.Children.Add(new TextBlock { Text = "Sound & pulse lab", FontSize = 24, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        root.Children.Add(header);
        root.Children.Add(new TextBlock { Text = "5-second heads-up → 12-second rest", Foreground = Art.Brush("#A4B8C9"), FontSize = 13, Margin = new Thickness(0, 16, 0, 8) });
        var status = new TextBlock { Text = "Your preview starts in a moment…", TextWrapping = TextWrapping.Wrap, FontSize = 14, Margin = new Thickness(0, 4, 0, 18), MinHeight = 40 };
        root.Children.Add(status);
        var styles = new ControlPanel(prefs, engine);
        window.Resources = styles.Resources;
        var tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        var soundsOnly = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        var volumeDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        var startDelay = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        int soundStep = 0;
        SoundCue[] sequence = [SoundCue.Warning, SoundCue.Tick, SoundCue.Tick, SoundCue.RestStart, SoundCue.Tick, SoundCue.Complete];
        void Replay()
        {
            soundsOnly.Stop(); soundStep = 0;
            overlays.CloseAll(); engine.Restart();
            overlays.DemoNext = true; engine.Demonstrate(5);
        }
        Button Button(string label, Action action, bool primary = false)
        {
            var button = new Button { Content = label, Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(14, 11, 14, 11) };
            if (primary) { button.Background = Art.Brush("#B9F2E2"); button.Foreground = Art.Brush("#152C32"); }
            button.Click += (_, _) => action(); root.Children.Add(button); return button;
        }
        Button("Replay screen + sound", Replay, true);
        Button("Play sounds only", () =>
        {
            engine.Restart(); soundsOnly.Stop(); soundStep = 0;
            overlays.Sound.Play(sequence[soundStep++]); soundsOnly.Start();
            status.Text = "Playing the four sound cues…";
        });
        var volumeLabel = new TextBlock { Text = "Volume · 36%", Foreground = Art.Brush("#A4B8C9"), FontSize = 12, Margin = new Thickness(0, 2, 0, 7) };
        root.Children.Add(volumeLabel);
        var slider = new Slider { Minimum = 0, Maximum = 65, Value = 36, Margin = new Thickness(0, 0, 0, 9) };
        slider.ValueChanged += (_, e) =>
        {
            prefs.SoundVolume = e.NewValue / 100; volumeLabel.Text = $"Volume · {e.NewValue:0}%";
            volumeDelay.Stop(); volumeDelay.Start();
        };
        root.Children.Add(slider);
        var mute = new CheckBox { Content = "Mute preview audio", Margin = new Thickness(0, 6, 0, 8) };
        mute.Checked += (_, _) => { prefs.SoundEnabled = false; overlays.Sound.Reload(); };
        mute.Unchecked += (_, _) => prefs.SoundEnabled = true;
        root.Children.Add(mute);
        root.Children.Add(new TextBlock { Text = "Preview only · your installed version is unchanged.", FontSize = 11, Foreground = Art.Brush("#8295AC"), Margin = new Thickness(0, 5, 0, 0) });
        engine.Changed += () =>
        {
            Log($"PHASE {engine.Phase} remaining={engine.Remaining}");
            overlays.Synchronize();
            status.Text = engine.Phase switch
            {
                Phase.Warning => "Heads-up: listen to the soft ticks.",
                Phase.Resting => "Resting: the ring pulses on every second.",
                _ => "Preview complete. Replay it, adjust volume, or close this window."
            };
            if (engine.Phase == Phase.Resting) window.WindowState = WindowState.Minimized;
            if (engine.Phase == Phase.Working) { window.WindowState = WindowState.Normal; }
        };
        tick.Tick += (_, _) => engine.Tick();
        soundsOnly.Tick += (_, _) =>
        {
            if (soundStep >= sequence.Length) { soundsOnly.Stop(); status.Text = "Sounds finished. You can adjust the volume and replay."; return; }
            overlays.Sound.Play(sequence[soundStep++]);
        };
        volumeDelay.Tick += (_, _) => { volumeDelay.Stop(); overlays.Sound.Reload(); };
        startDelay.Tick += (_, _) => { startDelay.Stop(); Replay(); };
        window.Loaded += (_, _) => { Log("LAB OPEN — no startup or settings changes"); tick.Start(); startDelay.Start(); };
        window.Closed += (_, _) =>
        {
            tick.Stop(); soundsOnly.Stop(); volumeDelay.Stop(); startDelay.Stop(); overlays.Dispose();
            Log("LAB CLOSED"); app.Shutdown();
        };
        app.DispatcherUnhandledException += (_, e) => { Log("ERROR " + e.Exception); e.Handled = true; app.Shutdown(1); };
        window.Show(); return app.Run();
    }
}
