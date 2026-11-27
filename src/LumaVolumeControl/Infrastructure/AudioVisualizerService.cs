using LumaVolumeControl.Models;
using NAudio.Dsp;
using NAudio.Wave;

#pragma warning disable CS0618 // NAudio 3 preview still marks the stable system-loopback wrapper obsolete.

namespace LumaVolumeControl.Infrastructure;

public sealed class AudioVisualizerService : IDisposable
{
    private const int FftSize = 8192;
    private const int FftExponent = 13;
    private const int BandCount = 56;
    // Match classic oscilloscope visualizers: retain a full 2048-sample X/Y trace
    // instead of a short waveform fragment.
    private const int WaveCount = 2048;
    private readonly object _gate = new();
    private readonly HashSet<string> _consumers = new(StringComparer.Ordinal);
    private readonly Complex[] _fft = new Complex[FftSize];
    private readonly Complex[] _fftRight = new Complex[FftSize];
    private readonly float[] _spectrumLeft = new float[FftSize];
    private readonly float[] _spectrumRight = new float[FftSize];
    private int _spectrumHop;
    private int _sampleRate = 48000;
    private readonly float[] _bands = new float[BandCount];
    private readonly float[] _waveLeft = new float[WaveCount];
    private readonly float[] _waveRight = new float[WaveCount];
    private WasapiLoopbackCapture? _capture;
    private int _fftPosition;
    private int _wavePosition;
    private float _leftPeak;
    private float _rightPeak;
    private int _sensitivity = 120;
    private bool _disposed;
    private bool _spectrumActive;
    private readonly float[] _fftWindow = Enumerable.Range(0, FftSize).Select(i => (float)FastFourierTransform.HammingWindow(i, FftSize)).ToArray();

    public void UpdateSettings(AppSettings settings)
    {
        lock (_gate)
        {
            _sensitivity = settings.VisualizerSensitivity;
            _spectrumActive = settings.VisualizerStyle == VisualizerStyle.NeonSpectrum;
        }
    }

    public void SetConsumerActive(string consumer, bool active)
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (active) _consumers.Add(consumer); else _consumers.Remove(consumer);
            if (_consumers.Count > 0 && _capture is null) StartCaptureCore();
            else if (_consumers.Count == 0 && _capture is not null) StopCaptureCore();
        }
    }

    public AudioVisualizerFrame GetFrame()
    {
        lock (_gate)
        {
            var left = new float[WaveCount];
            var right = new float[WaveCount];
            var tail = WaveCount - _wavePosition;
            Array.Copy(_waveLeft, _wavePosition, left, 0, tail);
            Array.Copy(_waveLeft, 0, left, tail, _wavePosition);
            Array.Copy(_waveRight, _wavePosition, right, 0, tail);
            Array.Copy(_waveRight, 0, right, tail, _wavePosition);
            return new AudioVisualizerFrame((float[])_bands.Clone(), left, right, _leftPeak, _rightPeak);
        }
    }

    private void StartCaptureCore()
    {
        try
        {
            var capture = new WasapiLoopbackCapture();
            capture.DataAvailable += Capture_DataAvailable;
            capture.RecordingStopped += Capture_RecordingStopped;
            _capture = capture;
            _sampleRate = capture.WaveFormat.SampleRate;
            _fftPosition = 0;
            _spectrumHop = 0;
            Array.Clear(_spectrumLeft);
            Array.Clear(_spectrumRight);
            capture.StartRecording();
        }
        catch
        {
            _capture?.Dispose();
            _capture = null;
        }
    }

    private void StopCaptureCore()
    {
        var capture = _capture;
        _capture = null;
        if (capture is null) return;
        capture.DataAvailable -= Capture_DataAvailable;
        capture.RecordingStopped -= Capture_RecordingStopped;
        try { capture.StopRecording(); } catch { }
        capture.Dispose();
        Array.Clear(_bands);
        _leftPeak = 0;
        _rightPeak = 0;
    }

    private void Capture_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        lock (_gate)
        {
            if (_disposed || _consumers.Count == 0 || _capture is not null) return;
            StartCaptureCore();
        }
    }

    private void Capture_DataAvailable(object? sender, WaveInEventArgs e)
    {
        if (sender is not WasapiLoopbackCapture capture || e.BytesRecorded <= 0) return;
        lock (_gate)
        {
            // Some devices expose 32-bit float audio as WAVE_FORMAT_EXTENSIBLE.
            // Normalize it before decoding so every supported output format follows the
            // same sample-reading path.
            var format = capture.WaveFormat is WaveFormatExtensible extensible
                ? extensible.ToStandardWaveFormat()
                : capture.WaveFormat;
            var channels = Math.Max(1, format.Channels);
            var bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
            var frameSize = bytesPerSample * channels;
            var leftMax = 0f;
            var rightMax = 0f;

            for (var offset = 0; offset + frameSize <= e.BytesRecorded; offset += frameSize)
            {
                var left = ReadSample(e.Buffer, offset, format);
                var right = channels > 1 ? ReadSample(e.Buffer, offset + bytesPerSample, format) : left;
                leftMax = Math.Max(leftMax, Math.Abs(left));
                rightMax = Math.Max(rightMax, Math.Abs(right));
                _waveLeft[_wavePosition] = left;
                _waveRight[_wavePosition] = right;
                _wavePosition = (_wavePosition + 1) % WaveCount;

                if (!_spectrumActive) continue;
                _spectrumLeft[_fftPosition] = left;
                _spectrumRight[_fftPosition] = right;
                _fftPosition = (_fftPosition + 1) & (FftSize - 1);
                // Retain the long analysis window for bass resolution, but
                // slide it forward at 60 Hz instead of waiting 8192 samples.
                if (++_spectrumHop >= Math.Max(1, _sampleRate / 60))
                {
                    _spectrumHop = 0;
                    for (var i = 0; i < FftSize; i++)
                    {
                        var source = (_fftPosition + i) & (FftSize - 1);
                        _fft[i] = new Complex { X = _spectrumLeft[source] * _fftWindow[i], Y = 0 };
                        _fftRight[i] = new Complex { X = _spectrumRight[source] * _fftWindow[i], Y = 0 };
                    }
                    CalculateSpectrum();
                }
            }

            _leftPeak = SmoothPeak(_leftPeak, leftMax);
            _rightPeak = SmoothPeak(_rightPeak, rightMax);
        }
    }

    private void CalculateSpectrum()
    {
        FastFourierTransform.FFT(true, FftExponent, _fft);
        FastFourierTransform.FFT(true, FftExponent, _fftRight);
        var sensitivity = _sensitivity / 100f;
        // Each bar owns a distinct set of FFT bins. Previously several low
        // bars read the very same bin after logarithmic rounding.
        var binHz = _sampleRate / (double)FftSize;
        var upperHz = Math.Min(18000, _sampleRate * 0.48);
        var start = Math.Max(1, (int)Math.Floor(40 / binHz));
        for (var band = 0; band < BandCount; band++)
        {
            var boundaryHz = 40 * Math.Pow(upperHz / 40, (band + 1d) / BandCount);
            var end = Math.Min(FftSize / 2, Math.Max(start + 1, (int)Math.Round(boundaryHz / binHz)));
            var magnitude = 0d;
            for (var bin = start; bin < end; bin++)
            {
                var leftPower = _fft[bin].X * _fft[bin].X + _fft[bin].Y * _fft[bin].Y;
                var rightPower = _fftRight[bin].X * _fftRight[bin].X + _fftRight[bin].Y * _fftRight[bin].Y;
                magnitude = Math.Max(magnitude, Math.Sqrt(Math.Max(leftPower, rightPower)));
            }
            start = end;
            // NAudio's forward FFT already applies 1/N scaling. Dividing by FftSize here
            // a second time pushes all bands below the visible range.
            var decibels = 20 * Math.Log10(Math.Max(1e-8, magnitude * 2));
            // Map roughly -80 dB..-15 dB to the full display height. This keeps quiet
            // system audio visible without pinning ordinary playback at 100%.
            var value = (float)Math.Clamp((decibels + 80) / 65 * sensitivity, 0, 1);
            _bands[band] = value >= _bands[band] ? value : _bands[band] * 0.78f;
        }
    }

    private static float ReadSample(byte[] buffer, int offset, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            return Math.Clamp(BitConverter.ToSingle(buffer, offset), -1f, 1f);
        if (format.BitsPerSample == 16)
            return BitConverter.ToInt16(buffer, offset) / 32768f;
        if (format.BitsPerSample == 24)
        {
            var value = buffer[offset] | buffer[offset + 1] << 8 | buffer[offset + 2] << 16;
            if ((value & 0x800000) != 0) value |= unchecked((int)0xFF000000);
            return value / 8388608f;
        }
        if (format.BitsPerSample == 32) return BitConverter.ToInt32(buffer, offset) / 2147483648f;
        return 0;
    }

    private static float SmoothPeak(float current, float next) => next >= current ? next : current * 0.84f;

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _consumers.Clear();
            StopCaptureCore();
        }
    }
}

public sealed record AudioVisualizerFrame(float[] Bands, float[] WaveLeft, float[] WaveRight, float LeftPeak, float RightPeak)
{
    public static AudioVisualizerFrame Empty { get; } = new(new float[56], new float[2048], new float[2048], 0, 0);
}
