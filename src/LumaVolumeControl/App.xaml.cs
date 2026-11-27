using LumaVolumeControl.Infrastructure;
using LumaVolumeControl.Models;
using LumaVolumeControl.Windows;
using Localization = LumaVolumeControl.Infrastructure.Localization;

namespace LumaVolumeControl;

public partial class App : Application
{
    private AppController? _controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = new SettingsService().Load();
        Localization.SetLanguage(settings.Language);
        var languageIndex = Array.FindIndex(e.Args, value => value.Equals("--preview-language", StringComparison.OrdinalIgnoreCase));
        if (languageIndex >= 0 && languageIndex + 1 < e.Args.Length && Enum.TryParse<AppLanguage>(e.Args[languageIndex + 1], true, out var previewLanguage))
            Localization.SetLanguage(previewLanguage);

        if (e.Args.Contains("--silent-install", StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var installer = new InstallerService();
                await installer.InstallAsync(desktopShortcut: true, launchAtStartup: false);
                Shutdown(0);
            }
            catch { Shutdown(1); }
            return;
        }

        if (e.Args.Contains("--finish-uninstall", StringComparer.OrdinalIgnoreCase))
        {
            var index = Array.FindIndex(e.Args, value => value.Equals("--finish-uninstall", StringComparison.OrdinalIgnoreCase));
            var target = index >= 0 && index + 1 < e.Args.Length ? e.Args[index + 1] : new InstallerService().InstallDirectory;
            ShowInstaller(InstallerWindowMode.FinishUninstall, target);
            return;
        }

        if (e.Args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
        {
            ShowInstaller(InstallerWindowMode.UninstallConfirm, null);
            return;
        }

        if (e.Args.Contains("--install", StringComparer.OrdinalIgnoreCase) || InstallerService.IsSetupExecutableName(Environment.ProcessPath))
        {
            ShowInstaller(InstallerWindowMode.Install, null);
            return;
        }

        _controller = new AppController(e.Args);
        _controller.Start();
    }

    private void ShowInstaller(InstallerWindowMode mode, string? target)
    {
        var window = new InstallerWindow(mode, target);
        MainWindow = window;
        window.Closed += (_, _) => Shutdown();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        base.OnExit(e);
    }
}
