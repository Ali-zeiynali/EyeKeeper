using System;

namespace EyeKeeper;

public enum Phase { Working, Warning, Resting, Paused }

public sealed class Preferences
{
    public int WorkMinutes { get; set; } = 20;
    public int RestSeconds { get; set; } = 20;
    public bool StartWithWindows { get; set; } = true;
    public bool ReducedMotion { get; set; }
    public bool FlowingBackground { get; set; } = true;
    public bool BreathingRing { get; set; } = true;
    public bool LiquidTransitions { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public bool TickSounds { get; set; } = true;
    public double SoundVolume { get; set; } = .36;
    public string Palette { get; set; } = "Random";
    public string MotionStyle { get; set; } = "Random";
    public void Validate()
    {
        WorkMinutes = Math.Clamp(WorkMinutes, 1, 180);
        RestSeconds = Math.Clamp(RestSeconds, 5, 300);
        SoundVolume = double.IsFinite(SoundVolume) ? Math.Clamp(SoundVolume, 0, 1) : .36;
        if (Array.IndexOf(Palettes.Names, Palette) < 0) Palette = "Random";
        if (Array.IndexOf(FluidStyles.Names, MotionStyle) < 0) MotionStyle = "Random";
    }
}

// All deadlines use monotonic time. Wall-clock edits cannot trigger or prolong a break.
public sealed class BreakEngine
{
    readonly Func<double> now;
    readonly Preferences prefs;
    double deadline;
    double pausedRemaining;
    double? resumeAt;
    public Phase Phase { get; private set; } = Phase.Working;
    public int Completed { get; private set; }
    public double RemainingExact => Math.Max(0, Phase == Phase.Paused ? pausedRemaining : deadline - now());
    public int Remaining => (int)Math.Ceiling(RemainingExact);
    public event Action? Changed;
    public BreakEngine(Preferences preferences, Func<double> clock)
    {
        prefs = preferences; now = clock; deadline = now() + prefs.WorkMinutes * 60;
    }
    public void Tick()
    {
        if (Phase == Phase.Paused)
        {
            if (resumeAt.HasValue && now() >= resumeAt) Resume();
            return;
        }
        if (now() >= deadline)
        {
            if (Phase == Phase.Resting) { Completed++; Restart(); }
            else { Phase = Phase.Resting; deadline = now() + prefs.RestSeconds; Changed?.Invoke(); }
        }
        else if (Phase == Phase.Working && deadline - now() <= 30)
        {
            Phase = Phase.Warning; Changed?.Invoke();
        }
    }
    public void Restart()
    {
        Phase = Phase.Working; deadline = now() + prefs.WorkMinutes * 60;
        resumeAt = null; Changed?.Invoke();
    }
    public void RestNow()
    {
        Phase = Phase.Resting; deadline = now() + prefs.RestSeconds;
        resumeAt = null; Changed?.Invoke();
    }
    public void Demonstrate(int warningSeconds = 30)
    {
        Phase = Phase.Warning; deadline = now() + Math.Clamp(warningSeconds, 1, 30); resumeAt = null; Changed?.Invoke();
    }
    public void Snooze()
    {
        Phase = Phase.Working; deadline = now() + 300; resumeAt = null; Changed?.Invoke();
    }
    public void Pause(int? minutes = null)
    {
        if (Phase != Phase.Paused)
            pausedRemaining = Phase == Phase.Resting ? prefs.WorkMinutes * 60 : Math.Max(1, deadline - now());
        Phase = Phase.Paused; resumeAt = minutes.HasValue ? now() + minutes.Value * 60 : null;
        Changed?.Invoke();
    }
    public void Resume()
    {
        if (Phase != Phase.Paused) return;
        deadline = now() + pausedRemaining; resumeAt = null;
        Phase = pausedRemaining <= 30 ? Phase.Warning : Phase.Working;
        Changed?.Invoke();
    }
}

public static class Palettes
{
    public static readonly string[] PaletteNames = ["Aurora", "Lagoon", "Dusk", "Rose", "Ember", "Iris",
        "Daylight", "Porcelain", "Apricot", "Mint", "Skylight", "Sakura",
        "Midnight", "Abyss", "Forest", "Obsidian", "Bordeaux", "Solar"];
    public static readonly string[] Names = ["Random", "Random light", "Random dark", "Random vivid", .. PaletteNames];
    public static readonly string[][] Colors = [
        ["#10072F", "#4A16D9", "#F64BE8", "#00E4FF"],
        ["#011C31", "#0065BD", "#00F5C6", "#55B8FF"],
        ["#1A073D", "#6222EE", "#FF55B7", "#FF9A50"],
        ["#30051D", "#C90072", "#FF4CB8", "#FFB76B"],
        ["#260B23", "#D61E42", "#FF7B20", "#FFE26A"],
        ["#08052C", "#3722DD", "#AD57FF", "#55DDFF"],
        ["#2376BD", "#4FD8FF", "#FFF17B", "#FF74D0"],
        ["#5F49AE", "#9B7FFF", "#FAADF0", "#97EFFF"],
        ["#9E3674", "#FF7D83", "#FFE196", "#B2A5FF"],
        ["#00777E", "#39DEBA", "#DDFF8B", "#7DDAFF"],
        ["#245DBD", "#5DA4FF", "#AEF4FF", "#F5A8FF"],
        ["#9443B7", "#F27BC9", "#FFD2D8", "#A998FF"],
        ["#030315", "#17005E", "#6700F5", "#D451FF"],
        ["#010C1D", "#003076", "#006FF0", "#00F1DB"],
        ["#031A18", "#005A53", "#00C682", "#B1F668"],
        ["#070616", "#252044", "#8055EB", "#E38EFF"],
        ["#160314", "#660039", "#E71279", "#FF8647"],
        ["#1A0414", "#860B39", "#FF420A", "#FFCA36"]
    ];
    public static int Choose(string name, int previous = -1)
    {
        int selected = Array.IndexOf(PaletteNames, name);
        if (selected >= 0) return selected;
        int first = name == "Random light" ? 6 : name == "Random dark" ? 12 : 0;
        int count = name == "Random" || Array.IndexOf(Names, name) < 0 ? Colors.Length : 6;
        bool exclude = previous >= first && previous < first + count;
        selected = first + Random.Shared.Next(count - (exclude ? 1 : 0));
        return exclude && selected >= previous ? selected + 1 : selected;
    }
}
