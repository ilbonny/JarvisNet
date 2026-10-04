using JarvisNet.Audio.Abstractions;
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
    private readonly AudioOptions _options;
    private readonly IAudioInputService _audioInput;
    private readonly ILogger<PiperTtsService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private PiperProvider? _provider;

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

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        _audioInput.SetSpeechToTextSuppressed(true);
        try
        {
            var pcm = await SynthesizePcmAsync(text, cancellationToken).ConfigureAwait(false);
            if (pcm.Length == 0)
            {
                return;
            }

            var sampleRate = _provider?.Configuration.Model.Audio?.SampleRate ?? (uint)_options.PiperSampleRate;
            var waveFormat = new WaveFormat((int)sampleRate, 16, 1);
            using var wavePlayer = AudioBackendFactory.CreateWavePlayer(_options);
            _logger.LogInformation(
                "Playing TTS ({ByteCount} bytes, {SampleRate} Hz, output device #{Device}).",
                pcm.Length,
                sampleRate,
                _options.OutputDeviceNumber);
            var outputPlayer = new NAudioOutputPlayer();
            await outputPlayer.PlayPcmAsync(wavePlayer, pcm, waveFormat, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _audioInput.SetSpeechToTextSuppressed(false);
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
            _provider = new PiperProvider(new PiperConfiguration
            {
                ExecutableLocation = executablePath,
                WorkingDirectory = workingDirectory,
                Model = model,
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
