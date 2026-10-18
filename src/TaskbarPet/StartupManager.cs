using Microsoft.Win32;

namespace TaskbarPet;

/// <summary>
/// Manages the per-user "Run" registry entry. Windows lists these entries in
/// Settings → Apps → Startup, so the user can toggle it there too.
/// </summary>
static class StartupManager
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "TaskbarPet";

    static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>
    /// If autostart is on but points to another location (the exe was moved or reinstalled
    /// elsewhere), point it to this exe again. Call once at startup.
    /// </summary>
    public static void RepairPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is string current && !current.Equals(Command, StringComparison.OrdinalIgnoreCase))
            key.SetValue(ValueName, Command);
    }

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value)
                key.SetValue(ValueName, Command);
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
