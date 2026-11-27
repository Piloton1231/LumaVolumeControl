using LumaVolumeControl.Models;

namespace LumaVolumeControl.Infrastructure;

public static class AppProfileIdentity
{
    public static string Resolve(AppSettings settings, string processName, string displayName)
    {
        var process = NormalizeProcess(processName);
        if (settings.AppVolumeProfiles.ContainsKey(process)) return process;
        var byProcess = settings.AppVolumeProfiles.Where(pair => NormalizeProcess(pair.Value.ProcessName) == process).Take(2).ToArray();
        if (byProcess.Length == 1) return byProcess[0].Key;
        // Descriptions are shared across installed/portable copies and renamed
        // executables. Never choose arbitrarily between conflicting profiles.
        if (string.IsNullOrWhiteSpace(displayName) || string.Equals(displayName, processName, StringComparison.OrdinalIgnoreCase)) return process;
        var byName = settings.AppVolumeProfiles.Where(pair => string.Equals(pair.Value.DisplayName.Trim(), displayName.Trim(), StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return byName.Length == 1 ? byName[0].Key : process;
    }

    private static string NormalizeProcess(string name)
    {
        var file = Path.GetFileName(name.Trim());
        if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) file = file[..^4];
        return file.ToLowerInvariant();
    }
}
