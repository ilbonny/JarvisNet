using JarvisNet.Audio.Abstractions;
using JarvisNet.Audio.Events;
using JarvisNet.Audio.Internal;
using JarvisNet.Audio.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;

namespace JarvisNet.Audio.Services;

public sealed class NAudioInputService : IAudioInputService
{
    private readonly ILogger<NAudioInputService> _logger;
    private readonly AudioOptions _options;
    private readonly object _gate = new();
    private IWaveIn? _waveIn;
    private bool _pushToTalkActive;
    private bool _isCapturing;
    private volatile bool _sttSuppressed;

    public NAudioInputService(IOptions<AudioOptions> options, ILogger<NAudioInputService> logger)
    {
        _options = options.Value;
        _logger = logger;
        InputMode = _options.InputMode;
    }

    public AudioInputMode InputMode { get; }

    public event EventHandler<AudioFrameEventArgs>? AudioFrameAvailable;

    public event EventHandler<AudioLevelChangedEventArgs>? AudioLevelChanged;

    public Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(AudioDeviceLister.GetInputDevices());
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_isCapturing)
            {
                return Task.CompletedTask;
            }

            _waveIn = AudioBackendFactory.CreateWaveIn(_options);
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();
            _isCapturing = true;
            _logger.LogInformation("Audio capture started (mode: {Mode}, sample rate: {SampleRate}).", InputMode, _options.SampleRate);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_isCapturing || _waveIn is null)
            {
                return Task.CompletedTask;
            }

            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.StopRecording();
            _waveIn.Dispose();
            _waveIn = null;
            _isCapturing = false;
            _logger.LogInformation("Audio capture stopped.");
        }

        return Task.CompletedTask;
    }

    public void SetPushToTalkActive(bool active) => _pushToTalkActive = active;

    public void SetSpeechToTextSuppressed(bool suppressed) => _sttSuppressed = suppressed;

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            _logger.LogError(e.Exception, "Audio capture stopped with an error.");
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        var samples = AudioPcmConverter.BytesToFloatSamples(e.Buffer.AsSpan(0, e.BytesRecorded), e.BytesRecorded);
        var rms = AudioPcmConverter.ComputeRms(samples);
        AudioLevelChanged?.Invoke(this, new AudioLevelChangedEventArgs(rms, DateTimeOffset.UtcNow));

        // In VAD mode we forward ALL frames to STT so Sherpa can detect trailing silence
        // and fire its own endpoint detection. The local VAD gate only blocks audio in
        // PushToTalk mode (when the button is not pressed).
        if (_sttSuppressed)
        {
            return;
        }

        if (InputMode == AudioInputMode.PushToTalk && !_pushToTalkActive)
        {
            return;
        }

        AudioFrameAvailable?.Invoke(this, new AudioFrameEventArgs(samples, _options.SampleRate));
    }
}
