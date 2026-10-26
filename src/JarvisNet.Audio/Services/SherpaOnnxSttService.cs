using JarvisNet.Audio.Abstractions;
using JarvisNet.Audio.Events;
using JarvisNet.Audio.Internal;
using JarvisNet.Audio.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SherpaOnnx;

namespace JarvisNet.Audio.Services;

public sealed class SherpaOnnxSttService : ISpeechToTextService, ISpeechToTextRuntime
{
    private readonly ILogger<SherpaOnnxSttService> _logger;
    private readonly IOptionsMonitor<AudioOptions> _optionsMonitor;
    private readonly object _sync = new();
    private OnlineRecognizer? _recognizer;
    private OnlineStream? _stream;
    private string _lastPartialText = string.Empty;
    private bool _sessionActive;

    private AudioOptions Options => _optionsMonitor.CurrentValue;

    public SherpaOnnxSttService(IOptionsMonitor<AudioOptions> optionsMonitor, ILogger<SherpaOnnxSttService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public event EventHandler<SpeechRecognizedEventArgs>? SpeechRecognized;

    public Task StartSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (_sessionActive)
            {
                return Task.CompletedTask;
            }

            _recognizer = CreateRecognizer();
            _stream = _recognizer.CreateStream();
            _lastPartialText = string.Empty;
            _sessionActive = true;
            _logger.LogInformation("Sherpa STT session started.");
        }

        return Task.CompletedTask;
    }

    public Task StopSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            DisposeRecognizer();
            _sessionActive = false;
            _lastPartialText = string.Empty;
            _logger.LogInformation("Sherpa STT session stopped.");
        }

        return Task.CompletedTask;
    }

    public async Task ReloadAfterProfileChangeAsync(CancellationToken cancellationToken = default)
    {
        var wasActive = false;
        lock (_sync)
        {
            wasActive = _sessionActive;
        }

        if (wasActive)
        {
            await StopSessionAsync(cancellationToken).ConfigureAwait(false);
            await StartSessionAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void ProcessAudio(ReadOnlyMemory<float> samples, int sampleRate)
    {
        if (!_sessionActive || samples.IsEmpty)
        {
            return;
        }

        lock (_sync)
        {
            if (_recognizer is null || _stream is null)
            {
                return;
            }

            _stream.AcceptWaveform(sampleRate, samples.ToArray());

            while (_recognizer.IsReady(_stream))
            {
                _recognizer.Decode(_stream);
            }

            var text = _recognizer.GetResult(_stream).Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text) && !string.Equals(text, _lastPartialText, StringComparison.Ordinal))
            {
                _lastPartialText = text;
                _logger.LogDebug("STT parziale: {Text}", text);
                RaiseSpeechRecognized(text, isFinal: false);
            }

            if (_recognizer.IsEndpoint(_stream))
            {
                if (SpeechTranscriptFilter.IsMeaningfulFinalTranscript(text))
                {
                    _logger.LogInformation("STT finale: {Text}", text);
                    RaiseSpeechRecognized(text, isFinal: true);
                }
                else if (!string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogInformation("STT finale ignorato (troppo corto o senza testo): {Text}", text);
                }

                _recognizer.Reset(_stream);
                _lastPartialText = string.Empty;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopSessionAsync().ConfigureAwait(false);
    }

    private OnlineRecognizer CreateRecognizer()
    {
        var config = new OnlineRecognizerConfig
        {
            FeatConfig =
            {
                SampleRate = Options.SampleRate,
                FeatureDim = 80,
            },
            ModelConfig =
            {
                Transducer =
                {
                    Encoder = Options.ResolvePath(Options.SherpaEncoderPath),
                    Decoder = Options.ResolvePath(Options.SherpaDecoderPath),
                    Joiner = Options.ResolvePath(Options.SherpaJoinerPath),
                },
                Tokens = Options.ResolvePath(Options.SherpaTokensPath),
                NumThreads = Options.SherpaNumThreads,
                Provider = Options.SherpaProvider,
                ModelType = string.IsNullOrWhiteSpace(Options.SherpaModelType)
                    ? "zipformer2"
                    : Options.SherpaModelType,
            },
            DecodingMethod = "greedy_search",
            EnableEndpoint = 1,
            Rule1MinTrailingSilence = 2.4f,
            Rule2MinTrailingSilence = 1.2f,
            Rule3MinUtteranceLength = 20.0f,
        };

        return new OnlineRecognizer(config);
    }

    private void DisposeRecognizer()
    {
        _stream?.Dispose();
        _stream = null;
        _recognizer?.Dispose();
        _recognizer = null;
    }

    private void RaiseSpeechRecognized(string text, bool isFinal)
    {
        SpeechRecognized?.Invoke(this, new SpeechRecognizedEventArgs(text, isFinal, DateTimeOffset.UtcNow));
    }
}
