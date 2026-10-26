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

public sealed class PiperTtsService : ITextToSpeechService, IPiperVoiceService
{
    public event EventHandler? SpeakingStarted;

    public event EventHandler? SpeakingCompleted;

    public event EventHandler<AudioLevelChangedEventArgs>? PlaybackLevelChanged;

    private readonly IOptionsMonitor<AudioOptions> _optionsMonitor;
    private readonly IAudioInputService _audioInput;
    private readonly ILogger<PiperTtsService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly object _speakGate = new();
    private PiperProvider? _provider;
    private string? _loadedModelPath;
    private IWavePlayer? _activeWavePlayer;

    private AudioOptions Options => _optionsMonitor.CurrentValue;

    public PiperTtsService(
        IOptionsMonitor<AudioOptions> optionsMonitor,
        IAudioInputService audioInput,
        ILogger<PiperTtsService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _audioInput = audioInput;
        _logger = logger;
    }

    public string GetCurrentVoiceKey() => PiperVoiceCatalog.ResolveVoiceKey(Options);

    public IReadOnlyList<PiperVoiceOption> ListVoices()
    {
        var list = new List<PiperVoiceOption>();
        foreach (var (key, info) in PiperVoiceCatalog.AllVoices)
        {
            var resolved = Options.ResolvePath(info.ModelRelativePath);
            list.Add(new PiperVoiceOption(
                key,
                info.SpeakerName,
                info.Quality,
                info.Gender,
                info.Locale,
                File.Exists(resolved)));
        }

        return list
            .OrderBy(v => v.Locale, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.SpeakerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task SetVoiceAsync(string voiceKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(voiceKey))
        {
            throw new ArgumentException("Voice key is required.", nameof(voiceKey));
        }

        if (!PiperVoiceCatalog.TryGetVoice(voiceKey, out var voice))
        {
            throw new ArgumentException($"Unknown Piper voice '{voiceKey}'.", nameof(voiceKey));
        }

        var modelPath = Options.ResolvePath(voice.ModelRelativePath);
        if (!File.Exists(modelPath))
        {
            throw new ArgumentException(
                $"Voice model not found at '{modelPath}'. Download the ONNX file first.",
                nameof(voiceKey));
        }

        Options.PiperVoiceKey = voiceKey;
        Options.PiperModelPath = voice.ModelRelativePath;
        Options.PiperSpeakerId = voice.DefaultSpeakerId > 0 ? voice.DefaultSpeakerId : null;

        await ResetProviderAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Piper voice switched to {VoiceKey} ({ModelPath}).", voiceKey, voice.ModelRelativePath);
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
        if (Options.EnableBargeIn)
        {
            _audioInput.SetBargeInListening(true, Options.BargeInRmsThreshold);
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

            var sampleRate = _provider?.Configuration.Model.Audio?.SampleRate ?? (uint)Options.PiperSampleRate;
            var waveFormat = new WaveFormat((int)sampleRate, 16, 1);
            var wavePlayer = AudioBackendFactory.CreateWavePlayer(Options);
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
                    Options.OutputDeviceNumber,
                    Options.EnableBargeIn);
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

    private async Task ResetProviderAsync(CancellationToken cancellationToken)
    {
        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _provider = null;
            _loadedModelPath = null;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<PiperProvider> GetProviderAsync(CancellationToken cancellationToken)
    {
        var modelPath = Path.GetFullPath(Options.ResolvePath(Options.GetEffectivePiperModelRelativePath()));
        if (_provider is not null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
        {
            return _provider;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_provider is not null && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
            {
                return _provider;
            }

            var executablePath = AudioPathHelper.ResolvePiperExecutablePath(Options);
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException(
                    $"Piper executable not found at '{executablePath}'. Download Piper for your platform or set {nameof(AudioOptions.PiperExecutablePath)}.",
                    executablePath);
            }

            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Piper voice model not found at '{modelPath}'.", modelPath);
            }

            var workingDirectory = string.IsNullOrWhiteSpace(Options.PiperWorkingDirectory)
                ? RepositoryPaths.GetPiperDirectory(RepositoryPaths.FindRepositoryRoot(AppContext.BaseDirectory))
                : Path.GetFullPath(Options.PiperWorkingDirectory);

            var model = await PiperVoiceModelLoader.LoadFromOnnxPathAsync(modelPath, cancellationToken)
                .ConfigureAwait(false);
            var speakerId = Options.ResolvePiperSpeakerId();
            _provider = new PiperProvider(new PiperConfiguration
            {
                ExecutableLocation = executablePath,
                WorkingDirectory = workingDirectory,
                Model = model,
                SpeakerId = speakerId,
            });
            _loadedModelPath = modelPath;

            _logger.LogInformation("Piper TTS initialized with model {ModelPath}.", modelPath);
            return _provider;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
