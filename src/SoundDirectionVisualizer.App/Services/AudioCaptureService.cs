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

    private readonly StereoLevelSmoother _stereoSmoother = new();
    private readonly AdaptiveStereoCalibration _stereoCalibration = new();
    private readonly AdaptiveLoudnessClassifier _stereoLoudnessClassifier = new();
    private readonly ChannelLevelSmoother _multichannelSmoother = new();
    private readonly AdaptiveStereoCalibration _multichannelCalibration = new();
    private readonly AdaptiveLoudnessClassifier _multichannelLoudnessClassifier = new();
    private readonly MultichannelContentValidator _multichannelValidator = new();
    private readonly SemaphoreSlim _transitionGate = new(1, 1);
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private WasapiRecorder? _primaryCapture;
    private CaptureDataAvailableHandler? _primaryDataAvailableHandler;
    private EventHandler<StoppedEventArgs>? _primaryStoppedHandler;
    private WasapiRecorder? _multichannelCapture;
    private CaptureDataAvailableHandler? _multichannelDataAvailableHandler;
    private EventHandler<StoppedEventArgs>? _multichannelStoppedHandler;
    private StereoSampleEncoding _primaryEncoding;
    private StereoSampleEncoding _multichannelEncoding;
    private ChannelLayout? _multichannelLayout;
    private string? _multichannelProcessName;
    private int? _multichannelProcessId;
    private string? _multichannelRequestedLayout;
    private string? _multichannelObservedLayout;
    private bool _multichannelIsProbe;
    private bool _multichannelSourceForced;
    private bool _multichannelPromoted;
    private double _smoothingFactor;
    private double _silenceThreshold;
    private double _modelMaximumBalance;
    private bool _automaticCalibration;
    private bool _loudSoundEmphasisEnabled;
    private double _loudSoundThresholdMultiplier;
    private bool _channelMeterEnabled;
    private AudioCaptureStatus? _currentStatus;
    private int _sessionGeneration;
    private bool _disposed;

    public event EventHandler<DirectionFrame>? FrameAvailable;

    public event EventHandler<AudioChannelMeterFrame>? ChannelLevelsAvailable;

    public event EventHandler<string>? CaptureFailed;

    public event EventHandler<AudioCaptureStatus>? CaptureStatusChanged;

    public AudioCaptureStatus? CurrentStatus => Volatile.Read(ref _currentStatus);

    public string? ActiveDeviceName => CurrentStatus?.SourceName;

    public string? ActiveDeviceId => CurrentStatus?.DeviceId;

    public string? FormatDescription => CurrentStatus?.FormatDescription;

    public int? ActiveProcessId => CurrentStatus?.ProcessId;

    public string? ProcessCaptureFallbackReason { get; private set; }

    public string? EndpointCaptureFallbackReason { get; private set; }

    public bool IsProcessCapture => CurrentStatus?.IsProcessCapture == true;

    public bool IsMultichannelProbeActive =>
        _multichannelCapture is not null && _multichannelIsProbe && !_multichannelPromoted;

    public async Task StartAsync(
        AppSettings settings,
        int? gameProcessId = null,
        string? gameProcessName = null,
        string? endpointDeviceIdOverride = null,
        bool opportunisticMultichannel = false,
        bool forceMultichannelSource = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        settings.Normalize();

        await _transitionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var generation = Interlocked.Increment(ref _sessionGeneration);
            StopCore();
            ApplyAnalysisSettings(settings);
            _channelMeterEnabled = settings.DebugForceMultichannelSource;
            ProcessCaptureFallbackReason = null;
            EndpointCaptureFallbackReason = null;

            if (gameProcessId is > 0 && forceMultichannelSource)
            {
                var processName = gameProcessName ?? $"PID {gameProcessId.Value}";
                var forcedFailure = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041)
                    ? await TryStartMultichannelProcessCaptureAsync(
                            gameProcessId.Value,
                            processName,
                            isProbe: false,
                            forceSource: true,
                            generation)
                        .ConfigureAwait(false)
                    : "Direct game capture requires Windows 10 version 2004 (build 19041) or newer.";
                if (forcedFailure is null)
                {
                    return;
                }

                ProcessCaptureFallbackReason = forcedFailure;
                StartEndpointCaptureWithFallback(
                    settings.AudioDeviceId,
                    endpointDeviceIdOverride,
                    generation);
                SetEndpointMultichannelFallbackStatus(
                    MultichannelCaptureState.Unavailable,
                    forcedFailure,
                    processName,
                    sourceForced: true);
                return;
            }

            if (gameProcessId is > 0 && OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                if (opportunisticMultichannel)
                {
                    StartEndpointCaptureWithFallback(
                        settings.AudioDeviceId,
                        endpointDeviceIdOverride,
                        generation);

                    var opportunisticFailure = await TryStartMultichannelProcessCaptureAsync(
                            gameProcessId.Value,
                            gameProcessName ?? $"PID {gameProcessId.Value}",
                            isProbe: true,
                            forceSource: false,
                            generation)
                        .ConfigureAwait(false);
                    if (opportunisticFailure is not null)
                    {
                        SetEndpointMultichannelFallbackStatus(
                            MultichannelCaptureState.Unavailable,
                            opportunisticFailure,
                            gameProcessName ?? $"PID {gameProcessId.Value}");
                    }

                    return;
                }

                var processName = gameProcessName ?? $"PID {gameProcessId.Value}";
                var multichannelFailure = await TryStartMultichannelProcessCaptureAsync(
                        gameProcessId.Value,
                        processName,
                        isProbe: false,
                        forceSource: false,
                        generation)
                    .ConfigureAwait(false);
                if (multichannelFailure is null)
                {
                    return;
                }

                try
                {
                    await StartStereoProcessCaptureAsync(
                            gameProcessId.Value,
                            processName,
                            generation,
                            multichannelFailure)
                        .ConfigureAwait(false);
                    return;
                }
                catch (Exception exception)
                {
                    ProcessCaptureFallbackReason =
                        $"Multichannel formats were unavailable ({multichannelFailure}); " +
                        $"stereo process capture also failed ({exception.Message}).";
                    StopCore();
                    ApplyAnalysisSettings(settings);
                }
            }
            else if (gameProcessId is > 0)
            {
                ProcessCaptureFallbackReason =
                    "Direct game capture requires Windows 10 version 2004 (build 19041) or newer.";
            }

            StartEndpointCaptureWithFallback(
                settings.AudioDeviceId,
                endpointDeviceIdOverride,
                generation);
        }
        finally
        {
            _transitionGate.Release();
        }
    }

    public async Task<bool> RetryOpportunisticMultichannelAsync(
        int gameProcessId,
        string gameProcessName,
        AudioCaptureStatus expectedStatus,
        bool forceMultichannelSource = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(expectedStatus);

        if (gameProcessId <= 0
            || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            return false;
        }

        await _transitionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var current = CurrentStatus;
            if (_disposed
                || current is null
                || !Equals(current, expectedStatus)
                || current.IsProcessCapture
                || _primaryCapture is null
                || _multichannelCapture is not null)
            {
                return false;
            }

            var generation = Volatile.Read(ref _sessionGeneration);
            var failure = await TryStartMultichannelProcessCaptureAsync(
                    gameProcessId,
                    gameProcessName,
                    isProbe: !forceMultichannelSource,
                    forceSource: forceMultichannelSource,
                    generation)
                .ConfigureAwait(false);
            if (failure is not null)
            {
                SetEndpointMultichannelFallbackStatus(
                    MultichannelCaptureState.Unavailable,
                    failure,
                    gameProcessName,
                    forceMultichannelSource);
            }
            else if (forceMultichannelSource)
            {
                StopPrimaryCaptureCore();
            }

            return true;
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
        Interlocked.Increment(ref _sessionGeneration);
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
    private async Task<string?> TryStartMultichannelProcessCaptureAsync(
        int processId,
        string processName,
        bool isProbe,
        bool forceSource,
        int generation)
    {
        var failures = new List<string>();
        var candidates = ProcessLoopbackFormatSupport.CreateMultichannelCandidates();

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            try
            {
                var capture = await new WasapiRecorderBuilder()
                    .WithProcessLoopback((uint)processId, ProcessLoopbackMode.IncludeTargetProcessTree)
                    .WithFormat(candidate.WaveFormat)
                    .BuildAsync()
                    .ConfigureAwait(false);
                ConfigureMultichannelCapture(
                    capture,
                    processId,
                    processName,
                    candidate,
                    requestedLayout: index == 0 ? "7.1" : "7.1 -> 5.1",
                    isProbe,
                    forceSource,
                    generation);
                return null;
            }
            catch (Exception exception)
            {
                StopMultichannelCaptureCore();
                failures.Add($"{candidate.LayoutName}: {exception.Message}");
            }
        }

        return string.Join("; ", failures);
    }

    [SupportedOSPlatform("windows10.0.19041.0")]
    private async Task StartStereoProcessCaptureAsync(
        int processId,
        string processName,
        int generation,
        string multichannelFailure)
    {
        var capture = await new WasapiRecorderBuilder()
            .WithProcessLoopback((uint)processId, ProcessLoopbackMode.IncludeTargetProcessTree)
            .WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2))
            .BuildAsync()
            .ConfigureAwait(false);

        ConfigurePrimaryCapture(
            capture,
            $"Game: {processName}",
            processId,
            deviceId: null,
            generation,
            MultichannelCaptureState.Unavailable,
            requestedLayout: "7.1 -> 5.1",
            observedLayout: null,
            multichannelProcessName: processName,
            fallbackReason: multichannelFailure);
    }

    private void StartEndpointCaptureWithFallback(
        string? configuredDeviceId,
        string? endpointDeviceIdOverride,
        int generation)
    {
        try
        {
            StartEndpointCapture(endpointDeviceIdOverride ?? configuredDeviceId, generation);
        }
        catch (Exception exception) when (!string.IsNullOrWhiteSpace(endpointDeviceIdOverride))
        {
            EndpointCaptureFallbackReason = exception.Message;
            StopPrimaryCaptureCore();
            try
            {
                StartEndpointCapture(configuredDeviceId, generation);
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

    private void StartEndpointCapture(string? requestedDeviceId, int generation)
    {
        _enumerator = new MMDeviceEnumerator();
        _device = ResolveDevice(_enumerator, requestedDeviceId);
        var capture = new WasapiRecorderBuilder()
            .WithDevice(_device)
            .WithLoopbackCapture()
            .Build();

        ConfigurePrimaryCapture(
            capture,
            _device.FriendlyName,
            processId: null,
            deviceId: _device.ID,
            generation,
            MultichannelCaptureState.NotAttempted,
            requestedLayout: null,
            observedLayout: null,
            multichannelProcessName: null,
            fallbackReason: null);
    }

    private void ConfigurePrimaryCapture(
        WasapiRecorder capture,
        string sourceName,
        int? processId,
        string? deviceId,
        int generation,
        MultichannelCaptureState multichannelState,
        string? requestedLayout,
        string? observedLayout,
        string? multichannelProcessName,
        string? fallbackReason)
    {
        if (capture.WaveFormat.Channels != 2)
        {
            var channels = capture.WaveFormat.Channels;
            capture.Dispose();
            throw new NotSupportedException(
                $"Stereo fallback requires exactly two channels. The capture source reports {channels} channels.");
        }

        try
        {
            _primaryEncoding = ResolveEncoding(capture.WaveFormat);
        }
        catch
        {
            capture.Dispose();
            throw;
        }

        _primaryCapture = capture;
        ResetStereoAnalysis();
        _primaryDataAvailableHandler = (buffer, _, _, _) =>
            HandlePrimaryDataAvailable(buffer, generation);
        _primaryStoppedHandler = (_, eventArgs) =>
            HandlePrimaryRecordingStopped(eventArgs, generation);

        try
        {
            _primaryCapture.DataAvailable += _primaryDataAvailableHandler;
            _primaryCapture.RecordingStopped += _primaryStoppedHandler;
            _primaryCapture.StartRecording();
            SetStatus(new AudioCaptureStatus(
                sourceName,
                deviceId,
                processId,
                capture.WaveFormat.ToString(),
                AudioEstimatorMode.Stereo,
                multichannelState,
                requestedLayout,
                observedLayout,
                multichannelProcessName,
                fallbackReason));
        }
        catch
        {
            StopPrimaryCaptureCore();
            throw;
        }
    }

    private void ConfigureMultichannelCapture(
        WasapiRecorder capture,
        int processId,
        string processName,
        ProcessLoopbackFormatOption requestedFormat,
        string requestedLayout,
        bool isProbe,
        bool forceSource,
        int generation)
    {
        try
        {
            if (!ProcessLoopbackFormatSupport.TryResolveLayout(
                    capture.WaveFormat,
                    out var observedLayout,
                    out var layoutFailure)
                || observedLayout is null)
            {
                throw new NotSupportedException(layoutFailure);
            }

            if (!observedLayout.HasSamePositions(requestedFormat.Layout))
            {
                throw new NotSupportedException(
                    $"Requested {requestedFormat.LayoutName}, but the observed channel mask describes {observedLayout.Name}.");
            }

            _multichannelEncoding = ResolveEncoding(capture.WaveFormat);
            _multichannelLayout = observedLayout;
        }
        catch
        {
            capture.Dispose();
            throw;
        }

        _multichannelCapture = capture;
        _multichannelProcessId = processId;
        _multichannelProcessName = processName;
        _multichannelRequestedLayout = requestedLayout;
        _multichannelObservedLayout = _multichannelLayout.Name;
        _multichannelIsProbe = isProbe;
        _multichannelSourceForced = forceSource;
        _multichannelPromoted = false;
        ResetMultichannelAnalysis();
        _multichannelDataAvailableHandler = (buffer, _, _, _) =>
            HandleMultichannelDataAvailable(buffer, generation);
        _multichannelStoppedHandler = (_, eventArgs) =>
            HandleMultichannelRecordingStopped(eventArgs, generation);

        try
        {
            _multichannelCapture.DataAvailable += _multichannelDataAvailableHandler;
            _multichannelCapture.RecordingStopped += _multichannelStoppedHandler;
            _multichannelCapture.StartRecording();
            ScheduleMultichannelValidationTimeout(generation);

            var current = CurrentStatus;
            if (isProbe && current is not null)
            {
                SetStatus(current with
                {
                    MultichannelState = MultichannelCaptureState.Probing,
                    RequestedLayout = requestedLayout,
                    ObservedLayout = _multichannelObservedLayout,
                    MultichannelProcessName = processName,
                    FallbackReason = null,
                    IsMultichannelSourceForced = forceSource
                });
            }
            else
            {
                SetStatus(new AudioCaptureStatus(
                    $"Game: {processName}",
                    DeviceId: null,
                    ProcessId: processId,
                    FormatDescription: capture.WaveFormat.ToString(),
                    AudioEstimatorMode.Stereo,
                    MultichannelCaptureState.Probing,
                    requestedLayout,
                    _multichannelObservedLayout,
                    processName,
                    IsMultichannelSourceForced: forceSource));
            }
        }
        catch
        {
            StopMultichannelCaptureCore();
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
        ResetStereoAnalysis();
        ResetMultichannelAnalysis();
    }

    private void StopCore()
    {
        StopMultichannelCaptureCore();
        StopPrimaryCaptureCore();
        Volatile.Write(ref _currentStatus, null);
        ResetStereoAnalysis();
        ResetMultichannelAnalysis();
    }

    private void StopPrimaryCaptureCore()
    {
        if (_primaryCapture is not null)
        {
            if (_primaryDataAvailableHandler is not null)
            {
                _primaryCapture.DataAvailable -= _primaryDataAvailableHandler;
            }

            if (_primaryStoppedHandler is not null)
            {
                _primaryCapture.RecordingStopped -= _primaryStoppedHandler;
            }

            try
            {
                _primaryCapture.StopRecording();
            }
            catch
            {
            }

            _primaryCapture.Dispose();
            _primaryCapture = null;
            _primaryDataAvailableHandler = null;
            _primaryStoppedHandler = null;
        }

        _device?.Dispose();
        _device = null;
        _enumerator?.Dispose();
        _enumerator = null;
        ResetStereoAnalysis();
    }

    private void StopMultichannelCaptureCore()
    {
        if (_multichannelCapture is not null)
        {
            if (_multichannelDataAvailableHandler is not null)
            {
                _multichannelCapture.DataAvailable -= _multichannelDataAvailableHandler;
            }

            if (_multichannelStoppedHandler is not null)
            {
                _multichannelCapture.RecordingStopped -= _multichannelStoppedHandler;
            }

            try
            {
                _multichannelCapture.StopRecording();
            }
            catch
            {
            }

            _multichannelCapture.Dispose();
            _multichannelCapture = null;
            _multichannelDataAvailableHandler = null;
            _multichannelStoppedHandler = null;
        }

        _multichannelLayout = null;
        _multichannelProcessName = null;
        _multichannelProcessId = null;
        _multichannelRequestedLayout = null;
        _multichannelObservedLayout = null;
        _multichannelIsProbe = false;
        _multichannelSourceForced = false;
        _multichannelPromoted = false;
        ResetMultichannelAnalysis();
    }

    private void HandlePrimaryDataAvailable(ReadOnlySpan<byte> buffer, int generation)
    {
        if (generation != Volatile.Read(ref _sessionGeneration)
            || Volatile.Read(ref _multichannelPromoted))
        {
            return;
        }

        try
        {
            var levels = StereoRmsAnalyzer.Calculate(buffer, 2, _primaryEncoding);
            var smoothed = _stereoSmoother.Update(levels, _smoothingFactor);
            var calibration = _automaticCalibration
                ? _stereoCalibration.Update(
                    smoothed,
                    _silenceThreshold,
                    AdaptiveStereoCalibration.TheoreticalMaximumBalance)
                : new StereoCalibration(_silenceThreshold, _modelMaximumBalance);
            var estimate = StereoDirectionEstimator.Estimate(
                smoothed,
                calibration.SilenceRmsThreshold,
                calibration.ModelMaximumBalance);
            var loudness = ClassifyLoudness(
                _stereoLoudnessClassifier,
                smoothed,
                calibration.SilenceRmsThreshold);

            var timestamp = DateTimeOffset.UtcNow;
            var channelLevelsAvailable = ChannelLevelsAvailable;
            if (Volatile.Read(ref _channelMeterEnabled) && channelLevelsAvailable is not null)
            {
                channelLevelsAvailable.Invoke(
                    this,
                    AudioChannelMeterFrameFactory.FromStereo(
                        timestamp,
                        CurrentStatus?.SourceName ?? "Stereo capture source",
                        smoothed));
            }

            FrameAvailable?.Invoke(
                this,
                new DirectionFrame(timestamp, smoothed, estimate, loudness));
        }
        catch (Exception exception)
        {
            CaptureFailed?.Invoke(this, exception.Message);
        }
    }

    private void HandleMultichannelDataAvailable(ReadOnlySpan<byte> buffer, int generation)
    {
        if (generation != Volatile.Read(ref _sessionGeneration) || _multichannelLayout is null)
        {
            return;
        }

        try
        {
            var isProbe = _multichannelIsProbe;
            var timestamp = DateTimeOffset.UtcNow;
            var analysis = MultichannelSignalAnalyzer.Calculate(
                buffer,
                _multichannelLayout,
                _multichannelEncoding);
            var validationState = _multichannelValidator.Observe(timestamp, analysis);
            var smoothedChannels = _multichannelSmoother.Update(analysis.Levels, _smoothingFactor);
            var channelLevelsAvailable = ChannelLevelsAvailable;
            if (Volatile.Read(ref _channelMeterEnabled) && channelLevelsAvailable is not null)
            {
                channelLevelsAvailable.Invoke(
                    this,
                    AudioChannelMeterFrameFactory.FromMultichannel(
                        timestamp,
                        _multichannelProcessName is null
                            ? "Game process loopback"
                            : $"Game: {_multichannelProcessName}",
                        smoothedChannels));
            }
            var smoothedStereo = smoothedChannels.ToStereoFallback();
            var calibration = _automaticCalibration
                ? _multichannelCalibration.Update(
                    smoothedStereo,
                    _silenceThreshold,
                    AdaptiveStereoCalibration.TheoreticalMaximumBalance)
                : new StereoCalibration(_silenceThreshold, _modelMaximumBalance);
            var estimate = validationState == MultichannelValidationState.Verified
                ? MultichannelDirectionEstimator.Estimate(
                    smoothedChannels,
                    calibration.SilenceRmsThreshold)
                : StereoDirectionEstimator.Estimate(
                    smoothedStereo,
                    calibration.SilenceRmsThreshold,
                    calibration.ModelMaximumBalance);
            var loudness = ClassifyLoudness(
                _multichannelLoudnessClassifier,
                smoothedStereo,
                calibration.SilenceRmsThreshold);

            if (validationState == MultichannelValidationState.Verified)
            {
                PromoteMultichannelCapture(generation);
            }
            else if (validationState == MultichannelValidationState.Uninformative)
            {
                HandleUninformativeMultichannelCapture(generation);
                if (isProbe)
                {
                    return;
                }
            }

            if (!isProbe || validationState == MultichannelValidationState.Verified)
            {
                FrameAvailable?.Invoke(
                    this,
                    new DirectionFrame(timestamp, smoothedStereo, estimate, loudness));
            }
        }
        catch (Exception exception)
        {
            if (_multichannelIsProbe && !Volatile.Read(ref _multichannelPromoted))
            {
                SetEndpointMultichannelFallbackStatus(
                    MultichannelCaptureState.Unavailable,
                    exception.Message);
                ScheduleStopMultichannelCapture(generation);
            }
            else
            {
                CaptureFailed?.Invoke(this, exception.Message);
            }
        }
    }

    private SoundLoudness ClassifyLoudness(
        AdaptiveLoudnessClassifier classifier,
        StereoLevels levels,
        double silenceRmsThreshold)
    {
        return _loudSoundEmphasisEnabled
            ? classifier.Update(
                levels,
                silenceRmsThreshold,
                _loudSoundThresholdMultiplier)
            : SoundLoudness.Ambient;
    }

    private void PromoteMultichannelCapture(int generation)
    {
        if (Volatile.Read(ref _multichannelPromoted))
        {
            return;
        }

        Volatile.Write(ref _multichannelPromoted, true);
        SetStatus(new AudioCaptureStatus(
            $"Game: {_multichannelProcessName}",
            DeviceId: null,
            ProcessId: _multichannelProcessId,
            FormatDescription: _multichannelCapture?.WaveFormat.ToString() ?? string.Empty,
            AudioEstimatorMode.Multichannel,
            MultichannelCaptureState.Verified,
            _multichannelRequestedLayout,
            _multichannelObservedLayout,
            _multichannelProcessName,
            IsMultichannelSourceForced: _multichannelSourceForced));

        if (_multichannelIsProbe)
        {
            ScheduleStopPrimaryCapture(generation);
        }
    }

    private void HandleUninformativeMultichannelCapture(int generation)
    {
        var reason =
            $"{_multichannelObservedLayout} was negotiated, but no independent side or rear content " +
            "was observed during the validation window.";

        if (_multichannelIsProbe)
        {
            SetEndpointMultichannelFallbackStatus(
                MultichannelCaptureState.Uninformative,
                reason);
            ScheduleStopMultichannelCapture(generation);
            return;
        }

        var current = CurrentStatus;
        if (current is not null && current.MultichannelState != MultichannelCaptureState.Uninformative)
        {
            SetStatus(current with
            {
                EstimatorMode = AudioEstimatorMode.Stereo,
                MultichannelState = MultichannelCaptureState.Uninformative,
                FallbackReason = reason
            });
        }
    }

    private void SetEndpointMultichannelFallbackStatus(
        MultichannelCaptureState state,
        string reason,
        string? processName = null,
        bool? sourceForced = null)
    {
        var current = CurrentStatus;
        if (current is null || current.IsProcessCapture)
        {
            return;
        }

        SetStatus(current with
        {
            EstimatorMode = AudioEstimatorMode.Stereo,
            MultichannelState = state,
            RequestedLayout = _multichannelRequestedLayout ?? "7.1 -> 5.1",
            ObservedLayout = _multichannelObservedLayout,
            MultichannelProcessName = _multichannelProcessName ?? processName,
            FallbackReason = reason,
            IsMultichannelSourceForced = sourceForced ?? _multichannelSourceForced
        });
    }

    private void ScheduleStopPrimaryCapture(int generation)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _transitionGate.WaitAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                if (!_disposed
                    && generation == Volatile.Read(ref _sessionGeneration)
                    && Volatile.Read(ref _multichannelPromoted))
                {
                    StopPrimaryCaptureCore();
                }
            }
            finally
            {
                _transitionGate.Release();
            }
        });
    }

    private void ScheduleMultichannelValidationTimeout(int generation)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(MultichannelContentValidator.MaximumProbeDuration).ConfigureAwait(false);
            try
            {
                await _transitionGate.WaitAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                if (!_disposed
                    && generation == Volatile.Read(ref _sessionGeneration)
                    && _multichannelCapture is not null
                    && _multichannelValidator.State == MultichannelValidationState.Pending)
                {
                    _multichannelValidator.Expire();
                    HandleUninformativeMultichannelCapture(generation);
                }
            }
            finally
            {
                _transitionGate.Release();
            }
        });
    }

    private void ScheduleStopMultichannelCapture(int generation)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _transitionGate.WaitAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                if (!_disposed
                    && generation == Volatile.Read(ref _sessionGeneration)
                    && !Volatile.Read(ref _multichannelPromoted))
                {
                    StopMultichannelCaptureCore();
                }
            }
            finally
            {
                _transitionGate.Release();
            }
        });
    }

    private void HandlePrimaryRecordingStopped(StoppedEventArgs eventArgs, int generation)
    {
        if (generation == Volatile.Read(ref _sessionGeneration) && eventArgs.Exception is not null)
        {
            CaptureFailed?.Invoke(this, eventArgs.Exception.Message);
        }
    }

    private void HandleMultichannelRecordingStopped(StoppedEventArgs eventArgs, int generation)
    {
        if (generation != Volatile.Read(ref _sessionGeneration) || eventArgs.Exception is null)
        {
            return;
        }

        if (_multichannelIsProbe && !Volatile.Read(ref _multichannelPromoted))
        {
            SetEndpointMultichannelFallbackStatus(
                MultichannelCaptureState.Unavailable,
                eventArgs.Exception.Message);
            ScheduleStopMultichannelCapture(generation);
        }
        else
        {
            CaptureFailed?.Invoke(this, eventArgs.Exception.Message);
        }
    }

    private void SetStatus(AudioCaptureStatus status)
    {
        Volatile.Write(ref _currentStatus, status);
        CaptureStatusChanged?.Invoke(this, status);
    }

    private void ResetStereoAnalysis()
    {
        _stereoSmoother.Reset();
        _stereoCalibration.Reset();
        _stereoLoudnessClassifier.Reset();
    }

    private void ResetMultichannelAnalysis()
    {
        _multichannelSmoother.Reset();
        _multichannelCalibration.Reset();
        _multichannelLoudnessClassifier.Reset();
        _multichannelValidator.Reset();
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
