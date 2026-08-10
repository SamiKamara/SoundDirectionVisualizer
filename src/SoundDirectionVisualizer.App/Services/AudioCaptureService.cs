using NAudio.CoreAudioApi;
using NAudio.Wave;
using SoundDirectionVisualizer.Core.Audio;
using SoundDirectionVisualizer.Core.Direction;
using System.Runtime.Versioning;

namespace SoundDirectionVisualizer.App.Services;

public sealed class AudioCaptureService : IDisposable
{
    private static readonly Guid IeeeFloatSubFormat = new("00000003-0000-0010-8000-00AA00389B71");
    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00AA00389B71");

    private readonly StereoLevelSmoother _smoother = new();
    private readonly AdaptiveStereoCalibration _calibration = new();
    private readonly AdaptiveLoudnessClassifier _loudnessClassifier = new();
    private readonly SemaphoreSlim _transitionGate = new(1, 1);
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private WasapiRecorder? _capture;
    private CaptureDataAvailableHandler? _dataAvailableHandler;
    private StereoSampleEncoding _encoding;
    private double _smoothingFactor;
    private double _silenceThreshold;
    private double _modelMaximumBalance;
    private bool _automaticCalibration;
    private bool _loudSoundEmphasisEnabled;
    private double _loudSoundThresholdMultiplier;
    private bool _disposed;

    public event EventHandler<DirectionFrame>? FrameAvailable;

    public event EventHandler<string>? CaptureFailed;

    public string? ActiveDeviceName { get; private set; }

    public string? ActiveDeviceId { get; private set; }

    public string? FormatDescription { get; private set; }

    public int? ActiveProcessId { get; private set; }

    public string? ProcessCaptureFallbackReason { get; private set; }

    public string? EndpointCaptureFallbackReason { get; private set; }

    public bool IsProcessCapture => ActiveProcessId.HasValue;

    public async Task StartAsync(
        AppSettings settings,
        int? gameProcessId = null,
        string? gameProcessName = null,
        string? endpointDeviceIdOverride = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        settings.Normalize();

        await _transitionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            StopCore();
            ApplyAnalysisSettings(settings);
            ProcessCaptureFallbackReason = null;
            EndpointCaptureFallbackReason = null;

            if (gameProcessId is > 0 && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                try
                {
                    await StartProcessCaptureAsync(
                            gameProcessId.Value,
                            gameProcessName ?? $"PID {gameProcessId.Value}")
                        .ConfigureAwait(false);
                    return;
                }
                catch (Exception exception)
                {
                    ProcessCaptureFallbackReason = exception.Message;
                    StopCore();
                    ApplyAnalysisSettings(settings);
                }
            }
            else if (gameProcessId is > 0)
            {
                ProcessCaptureFallbackReason =
                    "Direct game capture requires Windows 10 version 2004 (build 19041) or newer.";
            }

            try
            {
                StartEndpointCapture(endpointDeviceIdOverride ?? settings.AudioDeviceId);
            }
            catch (Exception exception) when (!string.IsNullOrWhiteSpace(endpointDeviceIdOverride))
            {
                EndpointCaptureFallbackReason = exception.Message;
                StopCore();
                ApplyAnalysisSettings(settings);
                try
                {
                    StartEndpointCapture(settings.AudioDeviceId);
                }
                catch
                {
                    StopCore();
                    throw;
                }
            }
            catch
            {
                StopCore();
                throw;
            }
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _transitionGate.Wait();
        try
        {
            StopCore();
        }
        finally
        {
            _transitionGate.Release();
            _transitionGate.Dispose();
        }
    }

    [SupportedOSPlatform("windows10.0.19041.0")]
    private async Task StartProcessCaptureAsync(int processId, string processName)
    {
        var capture = await new WasapiRecorderBuilder()
            .WithProcessLoopback((uint)processId, ProcessLoopbackMode.IncludeTargetProcessTree)
            .WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2))
            .BuildAsync()
            .ConfigureAwait(false);

        ConfigureCapture(
            capture,
            $"Game: {processName}",
            processId,
            deviceId: null);
    }

    private void StartEndpointCapture(string? requestedDeviceId)
    {
        _enumerator = new MMDeviceEnumerator();
        _device = ResolveDevice(_enumerator, requestedDeviceId);
        var capture = new WasapiRecorderBuilder()
            .WithDevice(_device)
            .WithLoopbackCapture()
            .Build();

        ConfigureCapture(capture, _device.FriendlyName, processId: null, deviceId: _device.ID);
    }

    private void ConfigureCapture(
        WasapiRecorder capture,
        string sourceName,
        int? processId,
        string? deviceId)
    {
        if (capture.WaveFormat.Channels != 2)
        {
            var channels = capture.WaveFormat.Channels;
            capture.Dispose();
            throw new NotSupportedException(
                $"Version 1 supports stereo input only. The capture source reports {channels} channels.");
        }

        try
        {
            _encoding = ResolveEncoding(capture.WaveFormat);
        }
        catch
        {
            capture.Dispose();
            throw;
        }

        _capture = capture;
        ActiveDeviceName = sourceName;
        ActiveDeviceId = deviceId;
        ActiveProcessId = processId;
        FormatDescription = capture.WaveFormat.ToString();
        _smoother.Reset();
        _calibration.Reset();
        _loudnessClassifier.Reset();

        _dataAvailableHandler = (buffer, _, _, _) => HandleDataAvailable(buffer);
        try
        {
            _capture.DataAvailable += _dataAvailableHandler;
            _capture.RecordingStopped += HandleRecordingStopped;
            _capture.StartRecording();
        }
        catch
        {
            StopCore();
            throw;
        }
    }

    private void ApplyAnalysisSettings(AppSettings settings)
    {
        _smoothingFactor = settings.SmoothingFactor;
        _silenceThreshold = settings.SilenceRmsThreshold;
        _modelMaximumBalance = settings.ModelMaximumBalance;
        _automaticCalibration = settings.AutomaticAudioCalibration;
        _loudSoundEmphasisEnabled = settings.LoudSoundEmphasisEnabled;
        _loudSoundThresholdMultiplier = settings.LoudSoundThresholdMultiplier;
        _smoother.Reset();
        _calibration.Reset();
        _loudnessClassifier.Reset();
    }

    private void StopCore()
    {
        if (_capture is not null)
        {
            if (_dataAvailableHandler is not null)
            {
                _capture.DataAvailable -= _dataAvailableHandler;
            }

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
            _dataAvailableHandler = null;
        }

        _device?.Dispose();
        _device = null;
        _enumerator?.Dispose();
        _enumerator = null;
        ActiveDeviceName = null;
        ActiveDeviceId = null;
        ActiveProcessId = null;
        FormatDescription = null;
        _smoother.Reset();
        _calibration.Reset();
        _loudnessClassifier.Reset();
    }

    private void HandleDataAvailable(ReadOnlySpan<byte> buffer)
    {
        try
        {
            var levels = StereoRmsAnalyzer.Calculate(buffer, 2, _encoding);
            var smoothed = _smoother.Update(levels, _smoothingFactor);
            var calibration = _automaticCalibration
                ? _calibration.Update(
                    smoothed,
                    _silenceThreshold,
                    AdaptiveStereoCalibration.TheoreticalMaximumBalance)
                : new StereoCalibration(_silenceThreshold, _modelMaximumBalance);
            var estimate = StereoDirectionEstimator.Estimate(
                smoothed,
                calibration.SilenceRmsThreshold,
                calibration.ModelMaximumBalance);
            var loudness = _loudSoundEmphasisEnabled
                ? _loudnessClassifier.Update(
                    smoothed,
                    calibration.SilenceRmsThreshold,
                    _loudSoundThresholdMultiplier)
                : SoundLoudness.Ambient;

            FrameAvailable?.Invoke(
                this,
                new DirectionFrame(DateTimeOffset.UtcNow, smoothed, estimate, loudness));
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
            if (extensible.SubFormat == IeeeFloatSubFormat && format.BitsPerSample == 32)
            {
                return StereoSampleEncoding.Float32;
            }

            if (extensible.SubFormat == PcmSubFormat)
            {
                return ResolvePcmEncoding(format.BitsPerSample);
            }
        }

        throw new NotSupportedException($"Unsupported capture sample format: {format}.");
    }

    private static StereoSampleEncoding ResolvePcmEncoding(int bitsPerSample) => bitsPerSample switch
    {
        16 => StereoSampleEncoding.Pcm16,
        24 => StereoSampleEncoding.Pcm24,
        32 => StereoSampleEncoding.Pcm32,
        _ => throw new NotSupportedException($"Unsupported PCM sample width: {bitsPerSample} bits.")
    };
}
