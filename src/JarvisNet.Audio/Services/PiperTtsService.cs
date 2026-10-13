using JarvisNet.Audio.Abstractions;
using JarvisNet.Audio.Events;
using JarvisNet.Audio.Internal;
using JarvisNet.Audio.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using PiperSharp;
using PiperSharp.Models;

namespace JarvisNet.Audio.Services;

public sealed class PiperTtsService : ITextToSpeechService
{
    public event EventHandler? SpeakingStarted;

    public event EventHandler? SpeakingCompleted;

    public event EventHandler<AudioLevelChangedEventArgs>? PlaybackLevelChanged;

    private readonly AudioOptions _options;
    private readonly IAudioInputService _audioInput;
    private readonly ILogger<PiperTtsService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly object _speakGate = new();
    private PiperProvider? _provider;
    private IWavePlayer? _activeWavePlayer;

    public PiperTtsService(
        IOptions<AudioOptions> options,
        IAudioInputService audioInput,
        ILogger<PiperTtsService> logger)
    {
        _options = options.Value;
        _audioInput = audioInput;
        _logger = logger;
    }

    public async Task<byte[]> SynthesizePcmAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<byte>();
        }

        var provider = await GetProviderAsync(cancellationToken).ConfigureAwait(false);
        return await provider.InferAsync(text, AudioOutputType.Raw).ConfigureAwait(false);
    }

    public void StopSpeaking()
    {
        lock (_speakGate)
        {
            _activeWavePlayer?.Stop();
        }
    }

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        _audioInput.SetSpeechToTextSuppressed(true);
        if (_options.EnableBargeIn)
        {
            _audioInput.SetBargeInListening(true, _options.BargeInRmsThreshold);
        }

        var speakingStarted = false;
        try
        {
            var pcm = await SynthesizePcmAsync(text, cancellationToken).ConfigureAwait(false);
            if (pcm.Length == 0)
            {
                return;
            }

            SpeakingStarted?.Invoke(this, EventArgs.Empty);
            speakingStarted = true;

            var sampleRate = _provider?.Configuration.Model.Audio?.SampleRate ?? (uint)_options.PiperSampleRate;
            var waveFormat = new WaveFormat((int)sampleRate, 16, 1);
            var wavePlayer = AudioBackendFactory.CreateWavePlayer(_options);
            lock (_speakGate)
            {
                _activeWavePlayer = wavePlayer;
            }

            try
            {
                _logger.LogInformation(
                    "Playing TTS ({ByteCount} bytes, {SampleRate} Hz, output device #{Device}, barge-in: {BargeIn}).",
                    pcm.Length,
                    sampleRate,
                    _options.OutputDeviceNumber,
                    _options.EnableBargeIn);
                var outputPlayer = new NAudioOutputPlayer();
                await outputPlayer.PlayPcmAsync(
                    wavePlayer,
                    pcm,
                    waveFormat,
                    level => PlaybackLevelChanged?.Invoke(this, new AudioLevelChangedEventArgs(level, DateTimeOffset.UtcNow)),
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                lock (_speakGate)
                {
                    if (ReferenceEquals(_activeWavePlayer, wavePlayer))
                    {
                        _activeWavePlayer = null;
                    }
                }

                wavePlayer.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Riproduzione TTS interrotta.");
            throw;
        }
        finally
        {
            _audioInput.SetBargeInListening(false);
            _audioInput.SetSpeechToTextSuppressed(false);

            if (speakingStarted)
            {
                SpeakingCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private async Task<PiperProvider> GetProviderAsync(CancellationToken cancellationToken)
    {
        if (_provider is not null)
        {
            return _provider;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_provider is not null)
            {
                return _provider;
            }

            var executablePath = AudioPathHelper.ResolvePiperExecutablePath(_options);
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException(
                    $"Piper executable not found at '{executablePath}'. Download Piper for your platform or set {nameof(AudioOptions.PiperExecutablePath)}.",
                    executablePath);
            }

            var modelPath = Path.GetFullPath(_options.ResolvePath(_options.GetEffectivePiperModelRelativePath()));

            var workingDirectory = string.IsNullOrWhiteSpace(_options.PiperWorkingDirectory)
                ? RepositoryPaths.GetPiperDirectory(RepositoryPaths.FindRepositoryRoot(AppContext.BaseDirectory))
                : Path.GetFullPath(_options.PiperWorkingDirectory);

            var model = await PiperVoiceModelLoader.LoadFromOnnxPathAsync(modelPath, cancellationToken)
                .ConfigureAwait(false);
            var speakerId = _options.ResolvePiperSpeakerId();
            _provider = new PiperProvider(new PiperConfiguration
            {
                ExecutableLocation = executablePath,
                WorkingDirectory = workingDirectory,
                Model = model,
                SpeakerId = speakerId,
            });

            _logger.LogInformation("Piper TTS initialized with model {ModelPath}.", modelPath);
            return _provider;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
