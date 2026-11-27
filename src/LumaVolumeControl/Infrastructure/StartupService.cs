using Microsoft.Win32;

namespace LumaVolumeControl.Infrastructure;

public static class StartupService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LumaVolumeControl";

    public static void Apply(bool enabled, string? executablePath = null)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled)
            key?.SetValue(ValueName, $"\"{executablePath ?? Environment.ProcessPath}\" --background");
        else
            key?.DeleteValue(ValueName, false);
    }
}
