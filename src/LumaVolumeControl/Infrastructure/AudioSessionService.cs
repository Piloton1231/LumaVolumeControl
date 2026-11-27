using LumaVolumeControl.Models;
using NAudio.CoreAudioApi;

namespace LumaVolumeControl.Infrastructure;

public sealed class AudioSessionService : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly SettingsService _settingsService;
    private readonly object _gate = new();
    private readonly HashSet<string> _appliedSessionIds = new(StringComparer.Ordinal);
    private readonly Dictionary<int, ProcessIdentity> _processCache = [];
    private AppSettings _settings;
    private bool _disposed;

    public AudioSessionService(AppSettings settings, SettingsService settingsService)
    {
        _settings = settings;
        _settingsService = settingsService;
    }

    public event EventHandler<AutoVolumeApplied>? AutoVolumeApplied;

    public void UpdateSettings(AppSettings settings, bool reapply = false)
    {
        lock (_gate)
        {
            _settings = settings;
            if (reapply) _appliedSessionIds.Clear();
        }
    }

    public AudioSnapshot ReadSnapshot(bool applyAutomaticRules = true)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            try
            {
                using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                using var manager = device.AudioSessionManager;
                var collected = new Dictionary<string, MutableSession>(StringComparer.OrdinalIgnoreCase);
                var activeSessionIds = new HashSet<string>(StringComparer.Ordinal);
                var activeProcessIds = new HashSet<int>();
                var settingsChanged = false;

                var sessions = manager.Sessions;
                for (var index = 0; index < sessions.Count; index++)
                {
                    using var session = sessions[index];
                    var pid = unchecked((int)session.GetProcessID);
                    if (pid > 0) activeProcessIds.Add(pid);
                    var sessionId = session.GetSessionInstanceIdentifier;
                    var info = ResolveProcess(pid, session.DisplayName);
                    // Loopback capture can create an audio session for this app.
                    // Exclude it before automatic volume rules or profile creation.
                    if (pid == Environment.ProcessId
                        || string.Equals(info.Key, "lumavolumecontrol", StringComparison.OrdinalIgnoreCase)) continue;
                    activeSessionIds.Add(sessionId);
                    var volume = Percent(session.SimpleAudioVolume.Volume);
                    var muted = session.SimpleAudioVolume.Mute;
                    var peak = session.AudioMeterInformation.MasterPeakValue;
                    var isRemembered = _settings.AppVolumeProfiles.TryGetValue(info.Key, out var profile);

                    if (applyAutomaticRules && _settings.AutoVolumeEnabled && pid > 0 && !_appliedSessionIds.Contains(sessionId))
                    {
                        var target = isRemembered ? profile!.Volume : _settings.FirstLaunchVolume;
                        if (!isRemembered)
                        {
                            profile = CreateProfile(info.Key, info.Name, target);
                            _settings.AppVolumeProfiles[info.Key] = profile;
                            isRemembered = true;
                            settingsChanged = true;
                        }
                        else if (!string.Equals(profile!.DisplayName, info.Name, StringComparison.Ordinal))
                        {
                            profile.DisplayName = info.Name;
                            settingsChanged = true;
                        }
                        if (volume != target)
                        {
                            session.SimpleAudioVolume.Volume = target / 100f;
                            AutoVolumeApplied?.Invoke(this, new AutoVolumeApplied(info.Name, volume, target, pid));
                            volume = target;
                        }
                        _appliedSessionIds.Add(sessionId);
                    }

                    if (!collected.TryGetValue(info.Key, out var row))
                    {
                        row = new MutableSession(info.Key, info.Name, info.Path, pid, volume, muted, peak, isRemembered);
                        collected.Add(info.Key, row);
                    }
                    else
                    {
                        row.Volume = Math.Max(row.Volume, volume);
                        row.Peak = Math.Max(row.Peak, peak);
                        row.IsMuted &= muted;
                    }
                }

                _appliedSessionIds.RemoveWhere(id => !activeSessionIds.Contains(id));
                foreach (var cachedPid in _processCache.Keys.Where(pid => !activeProcessIds.Contains(pid)).ToArray())
                    _processCache.Remove(cachedPid);
                if (settingsChanged) _settingsService.Save(_settings);

                var result = collected.Values
                    .Where(item => item.ProcessId > 0)
                    .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(item => item.ToRecord())
                    .ToArray();

                return new AudioSnapshot(
                    device.FriendlyName,
                    Percent(device.AudioEndpointVolume.MasterVolumeLevelScalar),
                    device.AudioEndpointVolume.Mute,
                    device.AudioMeterInformation.MasterPeakValue,
                    result);
            }
            catch
            {
                return new AudioSnapshot("オーディオデバイスが見つかりません", 0, false, 0, []);
            }
        }
    }

    public void SetMasterVolume(int value)
    {
        lock (_gate)
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0, 100) / 100f;
        }
    }

    public void SetMasterMute(bool muted)
    {
        lock (_gate)
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            device.AudioEndpointVolume.Mute = muted;
        }
    }

    public void SetSessionVolume(string key, string displayName, int value)
    {
        lock (_gate)
        {
            value = Math.Clamp(value, 0, 100);
            ForEachSession(key, session => session.SimpleAudioVolume.Volume = value / 100f);
            SaveProfileCore(key, displayName, value);
        }
    }

    public void SetSessionMute(string key, bool muted)
    {
        lock (_gate)
        {
            ForEachSession(key, session => session.SimpleAudioVolume.Mute = muted);
        }
    }

    public void ApplySavedSessionVolume(string key, int value)
    {
        lock (_gate)
        {
            // The profile is already saved by Settings. A disconnected audio
            // device must not prevent editing profiles for apps that are closed.
            try { ForEachSession(key, session => session.SimpleAudioVolume.Volume = Math.Clamp(value, 0, 100) / 100f); }
            catch (System.Runtime.InteropServices.COMException) { }
            catch (InvalidOperationException) { }
        }
    }

    private void SaveProfileCore(string key, string displayName, int value)
    {
        _settings.AppVolumeProfiles[key] = CreateProfile(key, displayName, value);
        _settingsService.Save(_settings);
    }

    private static AppVolumeProfile CreateProfile(string key, string displayName, int value) => new()
    {
        ProcessName = key,
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName,
        Volume = Math.Clamp(value, 0, 100)
    };

    private void ForEachSession(string key, Action<AudioSessionControl> action)
    {
        using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        using var manager = device.AudioSessionManager;
        var sessions = manager.Sessions;
        for (var index = 0; index < sessions.Count; index++)
        {
            using var session = sessions[index];
            var info = ResolveProcess(unchecked((int)session.GetProcessID), session.DisplayName);
            if (string.Equals(info.Key, key, StringComparison.OrdinalIgnoreCase)) action(session);
        }
    }

    private ProcessIdentity ResolveProcess(int processId, string displayName)
    {
        if (processId <= 0) return new("system-sounds", string.IsNullOrWhiteSpace(displayName) ? "システム サウンド" : displayName, null);
        if (_processCache.TryGetValue(processId, out var cached))
            return cached with { Key = AppProfileIdentity.Resolve(_settings, cached.Key, cached.Name) };
        try
        {
            using var process = Process.GetProcessById(processId);
            var name = process.ProcessName;
            string? path = null;
            try { path = process.MainModule?.FileName; } catch { }
            var friendly = TryFileDescription(path) ?? name;
            var resolved = new ProcessIdentity(name.ToLowerInvariant(), friendly, path);
            _processCache[processId] = resolved;
            return resolved with { Key = AppProfileIdentity.Resolve(_settings, resolved.Key, resolved.Name) };
        }
        catch
        {
            var fallback = string.IsNullOrWhiteSpace(displayName) ? $"アプリ {processId}" : displayName;
            var key = AppProfileIdentity.Resolve(_settings, $"pid-{processId}", fallback);
            return new ProcessIdentity(key, fallback, null);
        }
    }

    private static string? TryFileDescription(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
            return string.IsNullOrWhiteSpace(description) ? null : description;
        }
        catch { return null; }
    }

    private static int Percent(float value) => (int)Math.Round(Math.Clamp(value, 0f, 1f) * 100);

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AudioSessionService));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _enumerator.Dispose();
    }

    private sealed class MutableSession(string key, string name, string? path, int processId, int volume, bool isMuted, float peak, bool isRemembered)
    {
        public string Key { get; } = key;
        public string Name { get; } = name;
        public string? Path { get; } = path;
        public int ProcessId { get; } = processId;
        public int Volume { get; set; } = volume;
        public bool IsMuted { get; set; } = isMuted;
        public float Peak { get; set; } = peak;
        public bool IsRemembered { get; } = isRemembered;
        public AudioAppSession ToRecord() => new(Key, Name, Path, ProcessId, Volume, IsMuted, Peak, IsRemembered);
    }

    private sealed record ProcessIdentity(string Key, string Name, string? Path);
}
