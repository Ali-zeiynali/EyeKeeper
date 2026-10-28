using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text.Json;
using System.Threading;

namespace EyeKeeper;

public sealed class WorkerHeartbeat : IDisposable
{
    readonly MemoryMappedFile memory = MemoryMappedFile.CreateOrOpen($"Local\\EyeKeeper.Health.{Environment.ProcessId}", 8);
    readonly MemoryMappedViewAccessor view;
    long sequence;
    public WorkerHeartbeat() { view = memory.CreateViewAccessor(); Beat(); }
    public void Beat() => view.Write(0, ++sequence);
    public void Dispose() { view.Dispose(); memory.Dispose(); }
}

public static class Supervisor
{
    const string StopName = @"Local\EyeKeeper.Stop.v1";
    static string StopFile => Path.Combine(Storage.Folder, "intentional-stop");
    static string WorkerFile => Path.Combine(Storage.Folder, "worker.json");
    sealed record WorkerRecord(int Pid, long StartTicks, string Path);
    public static void RecordWorker()
    {
        Directory.CreateDirectory(Storage.Folder);
        using var process = Process.GetCurrentProcess();
        File.WriteAllText(WorkerFile, JsonSerializer.Serialize(new WorkerRecord(process.Id, process.StartTime.ToUniversalTime().Ticks, Environment.ProcessPath!)));
    }
    public static int Launch(bool demo = false)
    {
        if (File.Exists(StopFile)) File.Delete(StopFile);
        // A normal launch explicitly resumes protection after an intentional Quit.
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, demo ? "--watchdog-demo" : "--watchdog")
        { UseShellExecute = false, CreateNoWindow = true })?.Dispose();
        return 0;
    }
    public static void RequestStop()
    {
        Directory.CreateDirectory(Storage.Folder);
        File.WriteAllText(StopFile, DateTimeOffset.Now.ToString("O"));
        using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopName);
        stop.Set();
    }
    public static bool StopRequested => File.Exists(StopFile);
    static Process? FindWorker()
    {
        try
        {
            var record = JsonSerializer.Deserialize<WorkerRecord>(File.ReadAllText(WorkerFile));
            if (record == null) return null;
            var process = Process.GetProcessById(record.Pid);
            if (!process.HasExited && process.StartTime.ToUniversalTime().Ticks == record.StartTicks &&
                string.Equals(process.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)) return process;
            process.Dispose();
        }
        catch (Exception ex) when (ex is IOException or JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        return null;
    }
    public static int Run(bool demo = false, bool autostart = false)
    {
        if (StopRequested || (autostart && !Storage.Load().StartWithWindows)) return 0;
        using var mutex = new Mutex(true, @"Local\EyeKeeper.Watchdog.v1", out bool first);
        if (!first) return 0;
        using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopName);
        stop.Reset();
        Storage.Event("Watchdog started");
        Process? worker = FindWorker();
        MemoryMappedFile? health = null;
        MemoryMappedViewAccessor? view = null;
        var clock = Stopwatch.StartNew();
        double lastPoll = 0, lastBeat = clock.Elapsed.TotalSeconds, born = lastBeat;
        long previousSequence = 0;
        int failures = 0;
        try
        {
            while (!stop.WaitOne(0) && !StopRequested)
            {
                if (worker == null || worker.HasExited)
                {
                    if (worker != null)
                    {
                        Storage.Event($"Watchdog: worker {worker.Id} exited unexpectedly, code={worker.ExitCode}");
                        failures = clock.Elapsed.TotalSeconds - born > 60 ? 1 : Math.Min(5, failures + 1);
                        worker.Dispose(); view?.Dispose(); view = null; health?.Dispose(); health = null;
                        if (stop.WaitOne(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, failures - 1))))) break;
                    }
                    if (StopRequested) break;
                    worker = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, demo ? "--worker-demo" : "--worker")
                    { UseShellExecute = false, CreateNoWindow = true });
                    demo = false; born = lastBeat = clock.Elapsed.TotalSeconds; previousSequence = 0;
                    Storage.Event($"Watchdog: launched worker {worker?.Id}");
                    if (worker == null) throw new IOException("Could not start EyeKeeper worker.");
                }
                double now = clock.Elapsed.TotalSeconds;
                // Sleeping and resuming the PC must not count as a frozen UI.
                if (now - lastPoll > 10) lastBeat = now;
                lastPoll = now;
                if (view == null)
                {
                    try { health = MemoryMappedFile.OpenExisting($"Local\\EyeKeeper.Health.{worker.Id}"); view = health.CreateViewAccessor(0, 8, MemoryMappedFileAccess.Read); }
                    catch (FileNotFoundException) { }
                }
                long sequence = view?.ReadInt64(0) ?? 0;
                if (sequence != previousSequence) { lastBeat = now; previousSequence = sequence; }
                if (now - lastBeat > 60)
                {
                    Storage.Event($"Watchdog: worker {worker.Id} UI heartbeat stalled; restarting");
                    worker.Kill(); worker.WaitForExit(5000);
                }
                if (stop.WaitOne(1000)) break;
            }
            Storage.Event("Watchdog stopped intentionally");
            if (worker != null && !worker.HasExited && !worker.WaitForExit(5000)) worker.Kill();
            return 0;
        }
        catch (Exception ex) { Storage.Log(ex); Storage.Event("Watchdog failed; Windows recovery task can restart it"); return 1; }
        finally { view?.Dispose(); health?.Dispose(); worker?.Dispose(); }
    }
}
