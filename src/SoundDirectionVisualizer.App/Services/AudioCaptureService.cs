using NAudio.CoreAudioApi;
using NAudio.Dmo;
using NAudio.Wave;
using SoundDirectionVisualizer.Core.Audio;
using SoundDirectionVisualizer.Core.Direction;

namespace SoundDirectionVisualizer.App.Services;

public sealed class AudioCaptureService : IDisposable
{
    private readonly StereoLevelSmoother _smoother = new();
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private WasapiLoopbackCapture? _capture;
    private StereoSampleEncoding _encoding;
    private double _smoothingFactor;
    private double _silenceThreshold;
    private double _modelMaximumBalance;

    public event EventHandler<DirectionFrame>? FrameAvailable;

    public event EventHandler<string>? CaptureFailed;

    public string? ActiveDeviceName { get; private set; }

    public string? FormatDescription { get; private set; }

    public void Start(AppSettings settings)
    {
        Stop();
        settings.Normalize();

        _enumerator = new MMDeviceEnumerator();
        _device = ResolveDevice(_enumerator, settings.AudioDeviceId);
        _capture = new WasapiLoopbackCapture(_device);

        if (_capture.WaveFormat.Channels != 2)
        {
            var channels = _capture.WaveFormat.Channels;
            Stop();
            throw new NotSupportedException(
                $"Version 1 supports stereo output only. The selected device reports {channels} channels.");
        }

        _encoding = ResolveEncoding(_capture.WaveFormat);
        _smoothingFactor = settings.SmoothingFactor;
        _silenceThreshold = settings.SilenceRmsThreshold;
        _modelMaximumBalance = settings.ModelMaximumBalance;
        ActiveDeviceName = _device.FriendlyName;
        FormatDescription = _capture.WaveFormat.ToString();
        _smoother.Reset();

        _capture.DataAvailable += HandleDataAvailable;
        _capture.RecordingStopped += HandleRecordingStopped;
        _capture.StartRecording();
    }

    public void Stop()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= HandleDataAvailable;
            _capture.RecordingStopped -= HandleRecordingStopped;

            try
            {
                _capture.StopRecording();
            }
            catch
            {
            }

            _capture.Dispose();
            _capture = null;
        }

        _device?.Dispose();
        _device = null;
        _enumerator?.Dispose();
        _enumerator = null;
        ActiveDeviceName = null;
        FormatDescription = null;
        _smoother.Reset();
    }

    public void Dispose() => Stop();

    private void HandleDataAvailable(object? sender, WaveInEventArgs eventArgs)
    {
        try
        {
            var levels = StereoRmsAnalyzer.Calculate(
                eventArgs.Buffer.AsSpan(0, eventArgs.BytesRecorded),
                2,
                _encoding);
            var smoothed = _smoother.Update(levels, _smoothingFactor);
            var estimate = StereoDirectionEstimator.Estimate(
                smoothed,
                _silenceThreshold,
                _modelMaximumBalance);

            FrameAvailable?.Invoke(
                this,
                new DirectionFrame(DateTimeOffset.UtcNow, smoothed, estimate));
        }
        catch (Exception exception)
        {
            CaptureFailed?.Invoke(this, exception.Message);
        }
    }

    private void HandleRecordingStopped(object? sender, StoppedEventArgs eventArgs)
    {
        if (eventArgs.Exception is not null)
        {
            CaptureFailed?.Invoke(this, eventArgs.Exception.Message);
        }
    }

    private static MMDevice ResolveDevice(MMDeviceEnumerator enumerator, string? requestedId)
    {
        if (!string.IsNullOrWhiteSpace(requestedId))
        {
            try
            {
                var requested = enumerator.GetDevice(requestedId);
                if (requested.State == DeviceState.Active)
                {
                    return requested;
                }

                requested.Dispose();
            }
            catch
            {
            }
        }

        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    }

    private static StereoSampleEncoding ResolveEncoding(WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            return StereoSampleEncoding.Float32;
        }

        if (format.Encoding == WaveFormatEncoding.Pcm)
        {
            return ResolvePcmEncoding(format.BitsPerSample);
        }

        if (format is WaveFormatExtensible extensible)
        {
            if (extensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT && format.BitsPerSample == 32)
            {
                return StereoSampleEncoding.Float32;
            }

            if (extensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_PCM)
            {
                return ResolvePcmEncoding(format.BitsPerSample);
            }
        }

        throw new NotSupportedException($"Unsupported loopback sample format: {format}.");
    }

    private static StereoSampleEncoding ResolvePcmEncoding(int bitsPerSample) => bitsPerSample switch
    {
        16 => StereoSampleEncoding.Pcm16,
        24 => StereoSampleEncoding.Pcm24,
        32 => StereoSampleEncoding.Pcm32,
        _ => throw new NotSupportedException($"Unsupported PCM sample width: {bitsPerSample} bits.")
    };
}
