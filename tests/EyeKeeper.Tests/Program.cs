using EyeKeeper;
using System.Text.Json;

int passed = 0;
void Test(string name, Action test)
{
    try { test(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception ex) { Console.Error.WriteLine("FAIL " + name + ": " + ex.Message); Environment.Exit(1); }
}
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
double time = 0;
Preferences prefs = new();
BreakEngine Fresh() { time = 0; prefs = new(); return new(prefs, () => time); }

Test("default rhythm and 30-second warning boundary", () =>
{
    var e = Fresh(); Equal(1200, e.Remaining);
    time = 1169.999; e.Tick(); Equal(Phase.Working, e.Phase);
    time = 1170; e.Tick(); Equal(Phase.Warning, e.Phase); Equal(30, e.Remaining);
});
Test("rest starts at deadline and completes once", () =>
{
    var e = Fresh(); time = 1200; e.Tick(); Equal(Phase.Resting, e.Phase); Equal(20, e.Remaining);
    time = 1219.99; e.Tick(); Equal(Phase.Resting, e.Phase);
    time = 1220; e.Tick(); Equal(Phase.Working, e.Phase); Equal(1, e.Completed); Equal(1200, e.Remaining);
    e.Tick(); Equal(1, e.Completed);
});
Test("late tick still grants the full rest", () =>
{
    var e = Fresh(); time = 1400; e.Tick(); Equal(20, e.Remaining); Equal(Phase.Resting, e.Phase);
});
Test("pause freezes time and resume preserves it", () =>
{
    var e = Fresh(); time = 200; e.Pause(); time = 10000; e.Tick(); Equal(1000, e.Remaining);
    e.Resume(); Equal(Phase.Working, e.Phase); Equal(1000, e.Remaining);
});
Test("timed pause automatically resumes", () =>
{
    var e = Fresh(); time = 100; e.Pause(30); time = 1899; e.Tick(); Equal(Phase.Paused, e.Phase);
    time = 1900; e.Tick(); Equal(Phase.Working, e.Phase); Equal(1100, e.Remaining);
});
Test("pausing during rest dismisses it without counting completion", () =>
{
    var e = Fresh(); e.RestNow(); e.Pause(); e.Resume(); Equal(Phase.Working, e.Phase); Equal(1200, e.Remaining); Equal(0, e.Completed);
});
Test("snooze rest for exactly five minutes", () =>
{
    var e = Fresh(); e.RestNow(); e.Snooze(); Equal(300, e.Remaining); Equal(0, e.Completed);
    time = 270; e.Tick(); Equal(Phase.Warning, e.Phase);
    time = 300; e.Tick(); Equal(Phase.Resting, e.Phase);
});
Test("resume warning retains warning phase", () =>
{
    var e = Fresh(); time = 1180; e.Tick(); e.Pause(); time = 5000; e.Resume(); Equal(Phase.Warning, e.Phase); Equal(20, e.Remaining);
});
Test("manual rest and skip reset work deadline", () =>
{
    var e = Fresh(); time = 50; e.RestNow(); Equal(20, e.Remaining);
    e.Restart(); Equal(1200, e.Remaining); Equal(0, e.Completed);
});
Test("invalid saved values are constrained", () =>
{
    var p = new Preferences { WorkMinutes = -5, RestSeconds = int.MaxValue, Palette = "invalid" };
    p.Validate(); Equal(1, p.WorkMinutes); Equal(300, p.RestSeconds); Equal("Random", p.Palette);
});
Test("settings JSON roundtrip", () =>
{
    var p = new Preferences { WorkMinutes = 45, RestSeconds = 30, StartWithWindows = false, ReducedMotion = true, Palette = "Lagoon" };
    var copy = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(p))!;
    Equal(45, copy.WorkMinutes); Equal(30, copy.RestSeconds); Equal(false, copy.StartWithWindows); Equal(true, copy.ReducedMotion); Equal("Lagoon", copy.Palette);
});
Test("random colors never immediately repeat", () =>
{
    for (int p = 0; p < Palettes.Colors.Length; p++) for (int j = 0; j < 100; j++)
    {
        int next = Palettes.Choose("Random", p);
        if (next == p || next < 0 || next >= Palettes.Colors.Length) throw new Exception("Invalid palette selection");
    }
    for (int p = 0; p < Palettes.Colors.Length; p++) Equal(p, Palettes.Choose(Palettes.PaletteNames[p]));
});
Test("light, dark and vivid random modes stay in their color families", () =>
{
    foreach (var (name, first) in new[] { ("Random light", 6), ("Random dark", 12), ("Random vivid", 0) })
        for (int i = 0; i < 100; i++)
        {
            int next = Palettes.Choose(name, first);
            if (next < first || next >= first + 6 || next == first) throw new Exception("Wrong family or repeated color");
        }
});
Test("ring progress has subsecond precision", () =>
{
    var e = Fresh(); e.RestNow(); time = .125;
    Equal(19.875, e.RemainingExact); Equal(20, e.Remaining);
});
Test("notifications only fire for transitions", () =>
{
    var e = Fresh(); int changed = 0; e.Changed += () => changed++;
    for (int i = 0; i < 1170; i++) { time = i; e.Tick(); }
    Equal(0, changed); time = 1170; e.Tick(); e.Tick(); Equal(1, changed);
    time = 1200; e.Tick(); e.Tick(); Equal(2, changed);
});
Test("fluid texture is opaque, varied and deterministic", () =>
{
    var a = FluidTexture.Pixels(0, 2, 128, 80);
    var b = FluidTexture.Pixels(0, 2, 128, 80);
    Equal(128 * 80, a.Length);
    if (!a.SequenceEqual(b)) throw new Exception("Nondeterministic rendering");
    if (a.Any(pixel => (uint)pixel >> 24 != 255)) throw new Exception("Transparent pixels");
    if (a.Distinct().Count() < 500) throw new Exception("Color field lacks detail");
});
Test("liquid folds animate over time", () =>
{
    var a = FluidTexture.Pixels(0, 0, 128, 80); var b = FluidTexture.Pixels(0, 3, 128, 80);
    int different = a.Zip(b).Count(p => p.First != p.Second);
    if (different < a.Length * .9) throw new Exception("Background is static");
});
Test("every atmosphere produces a distinct fluid color field", () =>
{
    var hashes = new HashSet<string>();
    for (int i = 0; i < Palettes.Colors.Length; i++)
    {
        var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(FluidTexture.Pixels(i, 1, 64, 40).AsSpan());
        hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)));
    }
    Equal(Palettes.Colors.Length, hashes.Count);
});
Test("eight fluid styles are distinct and animated", () =>
{
    var hashes = new HashSet<string>();
    for (int i = 0; i < FluidStyles.Styles.Length; i++)
    {
        var frame = FluidTexture.Pixels(0, 0, 96, 60, i);
        if (frame.SequenceEqual(FluidTexture.Pixels(0, 3, 96, 60, i))) throw new Exception("Static motion style");
        hashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(frame.AsSpan()))));
        Equal(i, FluidStyles.Choose(FluidStyles.Styles[i]));
        if (FluidStyles.Choose("Random", i) == i) throw new Exception("Repeated motion style");
    }
    Equal(8, hashes.Count);
});
Test("animation toggles survive save and load", () =>
{
    var p = JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences {
        MotionStyle = "Marble", FlowingBackground = false, BreathingRing = false, LiquidTransitions = false
    }))!;
    Equal("Marble", p.MotionStyle); Equal(false, p.FlowingBackground); Equal(false, p.BreathingRing); Equal(false, p.LiquidTransitions);
});
Test("demo begins with the full 30-second heads-up", () =>
{
    var e = Fresh(); e.Demonstrate(); Equal(Phase.Warning, e.Phase); Equal(30, e.Remaining);
    time = 30; e.Tick(); Equal(Phase.Resting, e.Phase); Equal(20, e.Remaining);
});
void OnSta(Action action)
{
    Exception? failure = null;
    var thread = new System.Threading.Thread(() =>
    {
        try { action(); }
        catch (Exception ex) { failure = ex; }
        finally { System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
    });
    thread.SetApartmentState(System.Threading.ApartmentState.STA); thread.Start(); thread.Join();
    if (failure != null) throw new Exception("STA window regression failed", failure);
}
Test("closing callbacks cannot recursively close a window", () => OnSta(() =>
{
    for (int i = 0; i < 20; i++)
    {
        var window = new SafeWindow { Width = 1, Height = 1, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false };
        int closing = 0, closed = 0;
        window.Closing += (_, _) => { closing++; window.RequestClose(); };
        window.Deactivated += (_, _) => window.RequestClose();
        window.Closed += (_, _) => { closed++; window.RequestClose(); };
        window.Show(); window.RequestClose(); window.RequestClose();
        Equal(1, closing); Equal(1, closed);
    }
}));
Test("a canceled close can be retried safely", () => OnSta(() =>
{
    var window = new SafeWindow { Width = 1, Height = 1, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false };
    int attempts = 0, closed = 0;
    window.Closing += (_, e) => { attempts++; e.Cancel = attempts == 1; window.RequestClose(); };
    window.Closed += (_, _) => closed++;
    window.Show(); window.RequestClose(); Equal(0, closed);
    window.RequestClose(); Equal(2, attempts); Equal(1, closed);
}));
Test("Close menu is independent of the separate application Quit action", () => OnSta(() =>
{
    var e = Fresh();
    var panel = new ControlPanel(prefs, e);
    int closed = 0, quit = 0;
    panel.CloseRequested = () => closed++;
    panel.QuitRequested = () => quit++;
    IEnumerable<System.Windows.DependencyObject> Walk(System.Windows.DependencyObject node)
    {
        yield return node;
        foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(node).OfType<System.Windows.DependencyObject>())
            foreach (var nested in Walk(child)) yield return nested;
    }
    var nodes = Walk(panel).ToArray();
    var close = nodes.OfType<System.Windows.Controls.Button>().Single(b => Equals(b.Content, "Close menu"));
    close.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    Equal(1, closed); Equal(0, quit); Equal(Phase.Working, e.Phase);
    var section = nodes.OfType<System.Windows.Controls.Expander>().Single(x => Equals(x.Header, "Application"));
    Equal(false, section.IsExpanded);
    var exit = Walk(section).OfType<System.Windows.Controls.Button>().Single(b => Equals(b.Content, "Quit EyeKeeper completely…"));
    exit.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    Equal(1, closed); Equal(1, quit);
}));
var benchmark = System.Diagnostics.Stopwatch.StartNew();
FluidTexture.Pixels(0, 0, 480, 300); benchmark.Restart();
for (int i = 0; i < 12; i++) FluidTexture.Pixels(0, i / 30d, 480, 300);
Console.WriteLine(FormattableString.Invariant($"INFO CPU fallback rasterizer: {benchmark.Elapsed.TotalMilliseconds / 12:F1} ms/frame at 480x300 (12-frame local benchmark; UI composition excluded)."));
Console.WriteLine($"\n{passed} tests passed. Window regressions run offscreen at (-32000,-32000). No tray, startup or settings changes.");
