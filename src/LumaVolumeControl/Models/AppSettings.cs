using System.Text.Json.Serialization;

namespace LumaVolumeControl.Models;

public enum AppLanguage
{
    Japanese,
    English,
    Korean,
    ChineseSimplified,
    ChineseTraditional
}

public enum OverlayPlacement
{
    Center,
    TopCenter,
    BottomCenter,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public enum OverlayCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public enum VisualizerStyle
{
    NeonSpectrum,
    SmoothWave,
    AnalogVu,
    VfdMeter,
    LedMeter,
    RoundOscilloscope
}

public sealed class AppVolumeProfile
{
    public string ProcessName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Volume { get; set; } = 30;
}

public sealed class AppSettings
{
    public bool AutoVolumeEnabled { get; set; } = true;
    public int FirstLaunchVolume { get; set; } = 30;
    public bool ShowOverlay { get; set; } = true;
    public bool AnimateVolumeChange { get; set; } = true;
    public int OverlayDurationMs { get; set; } = 1800;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OverlayPlacement OverlayPlacement { get; set; } = OverlayPlacement.Center;
    public bool ShowVersionBadge { get; set; } = true;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OverlayCorner VersionBadgeCorner { get; set; } = OverlayCorner.BottomRight;
    public bool RunAtStartup { get; set; }
    public bool StartMinimized { get; set; }
    public bool CloseMixerOnFocusLoss { get; set; } = true;
    public bool CompactMixerMode { get; set; }
    public int VolumeStep { get; set; } = 5;
    public int UiScalePercent { get; set; } = 100;
    public bool VisualizerAlwaysOnTop { get; set; } = true;
    public int VisualizerSensitivity { get; set; } = 120;
    public int AudioReactionSensitivity { get; set; } = 100;
    public int OscilloscopeSensitivity { get; set; } = 100;
    public int VisualizerFrameRate { get; set; } = 30;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VisualizerStyle VisualizerStyle { get; set; } = VisualizerStyle.NeonSpectrum;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppLanguage Language { get; set; } = AppLanguage.Japanese;
    public Dictionary<string, AppVolumeProfile> AppVolumeProfiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // v1.1.0以前の設定を新しい「アプリ名 + 音量」形式へ移行するためだけに読み込みます。
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? AppVolumes { get; set; }

    public void Normalize()
    {
        FirstLaunchVolume = Math.Clamp(FirstLaunchVolume, 0, 100);
        OverlayDurationMs = Math.Clamp(OverlayDurationMs, 600, 5000);
        VolumeStep = Math.Clamp(VolumeStep, 1, 20);
        UiScalePercent = Math.Clamp(UiScalePercent, 70, 100);
        VisualizerSensitivity = Math.Clamp(VisualizerSensitivity, 50, 200);
        AudioReactionSensitivity = Math.Clamp(AudioReactionSensitivity, 10, 300);
        OscilloscopeSensitivity = Math.Clamp(OscilloscopeSensitivity, 25, 200);
        VisualizerFrameRate = VisualizerFrameRate >= 50 ? 60 : 30;
        if (!Enum.IsDefined(VisualizerStyle)) VisualizerStyle = VisualizerStyle.NeonSpectrum;
        if (!Enum.IsDefined(OverlayPlacement)) OverlayPlacement = OverlayPlacement.Center;
        if (!Enum.IsDefined(VersionBadgeCorner)) VersionBadgeCorner = OverlayCorner.BottomRight;
        if (!Enum.IsDefined(Language)) Language = AppLanguage.Japanese;
        AppVolumeProfiles = new Dictionary<string, AppVolumeProfile>(AppVolumeProfiles ?? [], StringComparer.OrdinalIgnoreCase);
        if (AppVolumes is not null)
        {
            foreach (var (key, volume) in AppVolumes)
            {
                AppVolumeProfiles.TryAdd(key, new AppVolumeProfile
                {
                    ProcessName = key,
                    DisplayName = key,
                    Volume = Math.Clamp(volume, 0, 100)
                });
            }
            AppVolumes = null;
        }
        foreach (var (key, profile) in AppVolumeProfiles.ToArray())
        {
            profile.ProcessName = string.IsNullOrWhiteSpace(profile.ProcessName) ? key : profile.ProcessName;
            profile.DisplayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.ProcessName : profile.DisplayName;
            profile.Volume = Math.Clamp(profile.Volume, 0, 100);
        }
    }
}

public sealed record AudioAppSession(
    string Key,
    string Name,
    string? ExecutablePath,
    int ProcessId,
    int Volume,
    bool IsMuted,
    float Peak,
    bool IsRemembered);

public sealed record AudioSnapshot(
    string DeviceName,
    int MasterVolume,
    bool MasterMuted,
    float MasterPeak,
    IReadOnlyList<AudioAppSession> Sessions);

public sealed record AutoVolumeApplied(string AppName, int FromVolume, int ToVolume, int ProcessId = 0);
