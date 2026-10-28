using System;
using System.Collections.Generic;
using System.IO;
using System.Media;

namespace EyeKeeper;

public enum SoundCue { Tick, Warning, RestStart, Complete }

public static class CountdownBeat
{
    public static double Age(double remaining) => Math.Ceiling(remaining) - remaining;
    public static double Strength(double age) => Math.Pow(1 - Math.Clamp(age / .58, 0, 1), 3);
}

// One clock for the whole overlay group: multiple monitors never duplicate a tick.
public sealed class SoundCueClock
{
    Phase lastPhase = Phase.Working;
    int lastSecond = -1, lastCompleted;
    public SoundCue? Observe(Phase phase, int second, int completed)
    {
        SoundCue? cue = null;
        if (phase != lastPhase)
        {
            cue = phase switch
            {
                Phase.Warning => SoundCue.Warning,
                Phase.Resting => SoundCue.RestStart,
                Phase.Working when completed > lastCompleted => SoundCue.Complete,
                _ => null
            };
        }
        else if (phase == Phase.Resting && second > 0 && lastSecond > second)
            cue = SoundCue.Tick;
        lastPhase = phase; lastSecond = second; lastCompleted = completed;
        return cue;
    }
}

public static class SoundDesign
{
    public const int SampleRate = 44100;
    public static short[] Samples(SoundCue cue, double volume)
    {
        volume = double.IsFinite(volume) ? Math.Clamp(volume, 0, 1) : .36;
        double duration = cue switch { SoundCue.Tick => .16, SoundCue.Warning => .65, SoundCue.RestStart => .82, _ => .95 };
        var samples = new short[(int)(duration * SampleRate)];
        (double f, double start, double gain)[] notes = cue switch
        {
            SoundCue.Tick => [(740, 0, .48), (1110, .006, .08)],
            SoundCue.Warning => [(523.25, 0, .40), (783.99, .13, .30)],
            SoundCue.RestStart => [(440, 0, .35), (659.25, .12, .30), (880, .24, .20)],
            _ => [(523.25, 0, .30), (659.25, .12, .26), (783.99, .24, .23), (1046.50, .36, .15)]
        };
        for (int i = 0; i < samples.Length; i++)
        {
            double time = i / (double)SampleRate, value = 0;
            foreach (var (frequency, start, gain) in notes)
            {
                double t = time - start;
                if (t < 0) continue;
                double attack = 1 - Math.Exp(-t / (cue == SoundCue.Tick ? .003 : .012));
                double decay = Math.Exp(-t * (cue == SoundCue.Tick ? 32 : 8.5));
                double pitch = cue == SoundCue.Tick ? frequency * t - 180 * t * t : frequency * t;
                double tone = Math.Sin(Math.Tau * pitch) + .11 * Math.Sin(Math.Tau * frequency * 2.003 * t);
                value += gain * attack * decay * tone;
            }
            // End taper prevents discontinuities/clicks, including at the end of the tick.
            double tail = Math.Clamp((duration - time) / .035, 0, 1);
            samples[i] = (short)Math.Clamp(value * tail * volume * 22000, short.MinValue, short.MaxValue);
        }
        return samples;
    }
    public static byte[] Wave(SoundCue cue, double volume)
    {
        short[] samples = Samples(cue, volume);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(36 + samples.Length * 2); writer.Write("WAVEfmt "u8);
        writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(SampleRate);
        writer.Write(SampleRate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(samples.Length * 2);
        foreach (short sample in samples) writer.Write(sample);
        return stream.ToArray();
    }
    public static void Export(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var cue in Enum.GetValues<SoundCue>())
            File.WriteAllBytes(Path.Combine(folder, cue + ".wav"), Wave(cue, .36));
    }
}

public sealed class SoundFeedback : IDisposable
{
    readonly Preferences prefs;
    readonly SoundCueClock clock = new();
    readonly Dictionary<SoundCue, SoundPlayer> players = [];
    readonly List<MemoryStream> streams = [];
    bool failed;
    public Action<SoundCue>? Played;
    public SoundFeedback(Preferences preferences) { prefs = preferences; }
    public void Observe(BreakEngine engine)
    {
        var cue = clock.Observe(engine.Phase, engine.Remaining, engine.Completed);
        if (cue.HasValue) Play(cue.Value);
    }
    public void Play(SoundCue cue)
    {
        if (failed || !prefs.SoundEnabled || prefs.SoundVolume <= 0 || (cue == SoundCue.Tick && !prefs.TickSounds)) return;
        try
        {
            if (!players.TryGetValue(cue, out var player))
            {
                var stream = new MemoryStream(SoundDesign.Wave(cue, prefs.SoundVolume));
                streams.Add(stream); player = new SoundPlayer(stream); player.Load(); players[cue] = player;
            }
            player.Play(); Played?.Invoke(cue);
        }
        catch (Exception error) { failed = true; Storage.Log(error); }
    }
    public void Reload()
    {
        foreach (var player in players.Values) { player.Stop(); player.Dispose(); }
        players.Clear(); foreach (var stream in streams) stream.Dispose(); streams.Clear(); failed = false;
    }
    public void Dispose() => Reload();
}
