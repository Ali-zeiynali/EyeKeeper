using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace EyeKeeper;

public static class Storage
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EyeKeeper");
    public static string FilePath => Path.Combine(Folder, "settings.json");
    public static Preferences Load()
    {
        try
        {
            var prefs = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new();
            prefs.Validate(); return prefs;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    public static void Save(Preferences prefs)
    {
        prefs.Validate(); Directory.CreateDirectory(Folder);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FilePath, true);
    }
    public static void Startup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            string path = Environment.ProcessPath ?? throw new IOException("Executable path unavailable.");
            if (!path.EndsWith("EyeKeeper.exe", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Use the published EyeKeeper.exe to enable startup.");
            key.SetValue("EyeKeeper", $"\"{path}\" --background");
        }
        else key.DeleteValue("EyeKeeper", false);
    }
    public static void Log(Exception exception)
    {
        try { Directory.CreateDirectory(Folder); File.AppendAllText(Path.Combine(Folder, "error.log"), $"{DateTimeOffset.Now:O} {exception}\n"); }
        catch { /* Logging must never keep a fullscreen overlay alive. */ }
    }
    public static void Event(string message)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "lifecycle.log");
            if (File.Exists(path) && new FileInfo(path).Length > 524288) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} PID={Environment.ProcessId} {message}\n");
        }
        catch { }
    }
}
