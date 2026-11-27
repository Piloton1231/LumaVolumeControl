using Microsoft.Win32;

namespace LumaVolumeControl.Infrastructure;

public sealed class InstallerService
{
    public const string ProductKey = "LumaVolumeControl";
    public const string ProductName = "Luma Volume Control";
    public const string Version = "1.5.3";

    public string InstallDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", ProductKey);
    public string InstalledExecutable => Path.Combine(InstallDirectory, ProductKey + ".exe");

    public static bool IsSetupExecutableName(string? path)
    {
        var name = Path.GetFileNameWithoutExtension(path ?? string.Empty);
        return name.EndsWith("-Setup", StringComparison.OrdinalIgnoreCase) || name.Contains("-Setup-", StringComparison.OrdinalIgnoreCase);
    }

    public async Task InstallAsync(bool desktopShortcut, bool launchAtStartup, IProgress<double>? progress = null)
    {
        var source = Environment.ProcessPath ?? throw new InvalidOperationException(Localization.T("Installer.ExecutableMissing"));
        foreach (var process in Process.GetProcessesByName(ProductKey))
        {
            if (process.Id == Environment.ProcessId) continue;
            try { process.Kill(true); await process.WaitForExitAsync(); } catch { }
        }

        Directory.CreateDirectory(InstallDirectory);
        progress?.Report(0.18);
        await CopyFileAsync(source, InstalledExecutable);
        progress?.Report(0.64);

        var startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", ProductName);
        Directory.CreateDirectory(startMenuFolder);
        CreateShortcut(Path.Combine(startMenuFolder, ProductName + ".lnk"), InstalledExecutable);
        if (desktopShortcut) CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProductName + ".lnk"), InstalledExecutable);
        RegisterUninstaller();
        StartupService.Apply(launchAtStartup, InstalledExecutable);
        progress?.Report(1);
    }

    public void StartInstalledApp() => Process.Start(new ProcessStartInfo(InstalledExecutable) { UseShellExecute = true });

    public async Task FinishUninstallAsync(string targetDirectory, IProgress<double>? progress = null)
    {
        var expected = Path.GetFullPath(InstallDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var requested = Path.GetFullPath(targetDirectory).TrimEnd(Path.DirectorySeparatorChar);
        if (!string.Equals(expected, requested, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(Localization.T("Installer.InvalidTarget"));
        await Task.Delay(800);
        progress?.Report(0.2);
        foreach (var process in Process.GetProcessesByName(ProductKey))
        {
            if (process.Id == Environment.ProcessId) continue;
            try { process.Kill(true); await process.WaitForExitAsync(); } catch { }
        }
        progress?.Report(0.45);
        RemoveShortcuts();
        StartupService.Apply(false, InstalledExecutable);
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true)) key?.DeleteSubKeyTree(ProductKey, false);
        progress?.Report(0.7);
        Exception? failure = null;
        for (var attempt = 0; attempt < 12 && Directory.Exists(targetDirectory); attempt++)
        {
            try { Directory.Delete(targetDirectory, true); failure = null; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { failure = ex; await Task.Delay(250 + attempt * 75); }
        }
        if (Directory.Exists(targetDirectory)) throw new IOException(Localization.T("Installer.DeleteFolderFailed"), failure);
        progress?.Report(1);
    }

    public string CreateTemporaryUninstaller()
    {
        var source = Environment.ProcessPath ?? throw new InvalidOperationException(Localization.T("Installer.ExecutableMissing"));
        var directory = Path.Combine(Path.GetTempPath(), "LumaVolumeControl-Uninstall-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "LumaVolumeControl-Uninstaller.exe");
        File.Copy(source, target, true);
        return target;
    }

    public void ScheduleTemporaryUninstallerCleanup()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return;
        var parent = Path.GetDirectoryName(executable);
        if (parent is null || !parent.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase)) return;
        NativeMethods.MoveFileEx(executable, null, NativeMethods.MoveFileDelayUntilReboot);
        NativeMethods.MoveFileEx(parent, null, NativeMethods.MoveFileDelayUntilReboot);
    }

    private void RegisterUninstaller()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + ProductKey);
        key?.SetValue("DisplayName", ProductName);
        key?.SetValue("DisplayVersion", Version);
        key?.SetValue("Publisher", "Luma");
        key?.SetValue("DisplayIcon", InstalledExecutable);
        key?.SetValue("InstallLocation", InstallDirectory);
        key?.SetValue("UninstallString", $"\"{InstalledExecutable}\" --uninstall");
        key?.SetValue("EstimatedSize", 80000, RegistryValueKind.DWord);
        key?.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key?.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static async Task CopyFileAsync(string source, string destination)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException(Localization.T("Installer.ShortcutUnavailable"));
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
        shortcut.Description = Localization.T("Installer.ShortcutDesc");
        shortcut.IconLocation = targetPath;
        shortcut.Save();
    }

    private static void RemoveShortcuts()
    {
        var startMenuFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", ProductName);
        var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProductName + ".lnk");
        try { if (Directory.Exists(startMenuFolder)) Directory.Delete(startMenuFolder, true); } catch { }
        try { if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut); } catch { }
    }
}
