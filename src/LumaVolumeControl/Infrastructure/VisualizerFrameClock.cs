namespace LumaVolumeControl.Infrastructure;

// Schedule on WPF's composition clock rather than an unrelated dispatcher timer.
public sealed class VisualizerFrameClock
{
    private TimeSpan _last;
    private bool _running;
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1d / 60);
    public event EventHandler? Tick;
    public void Start()
    {
        if (_running) return;
        _running = true;
        _last = TimeSpan.MinValue;
        CompositionTarget.Rendering += Render;
    }
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= Render;
    }
    private void Render(object? sender, EventArgs args)
    {
        if (args is not RenderingEventArgs rendering) return;
        var now = rendering.RenderingTime;
        if (_last != TimeSpan.MinValue && now - _last < Interval - TimeSpan.FromMilliseconds(1)) return;
        _last = now;
        Tick?.Invoke(this, EventArgs.Empty);
    }
}
