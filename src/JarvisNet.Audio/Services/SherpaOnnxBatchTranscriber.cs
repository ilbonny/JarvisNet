using JarvisNet.Audio.Abstractions;
using JarvisNet.Audio.Internal;
using JarvisNet.Audio.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SherpaOnnx;

namespace JarvisNet.Audio.Services;

public sealed class SherpaOnnxBatchTranscriber : ISpeechToTextBatchTranscriber
{
    private readonly IOptionsMonitor<AudioOptions> _optionsMonitor;
    private readonly ILogger<SherpaOnnxBatchTranscriber> _logger;

    public SherpaOnnxBatchTranscriber(
        IOptionsMonitor<AudioOptions> optionsMonitor,
        ILogger<SherpaOnnxBatchTranscriber> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    private AudioOptions Options => _optionsMonitor.CurrentValue;

    public Task<string?> TranscribeOggOpusAsync(Stream oggOpusStream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (samples, sampleRate) = OggOpusAudioDecoder.DecodeToMonoFloat(oggOpusStream);
        return TranscribeMonoPcmAsync(samples, sampleRate, cancellationToken);
    }

    public Task<string?> TranscribeMonoPcmAsync(
        ReadOnlyMemory<float> samples,
        int sampleRate,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (samples.IsEmpty)
        {
            return Task.FromResult<string?>(null);
        }

        var targetRate = Options.SampleRate;
        var pcm = sampleRate == targetRate
            ? samples.ToArray()
            : AudioResampler.ResampleToRate(samples.Span, sampleRate, targetRate);

        if (pcm.Length == 0)
        {
            return Task.FromResult<string?>(null);
        }

        using var recognizer = CreateRecognizer();
        using var stream = recognizer.CreateStream();

        const int chunkSize = 1600;
        for (var offset = 0; offset < pcm.Length; offset += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var length = Math.Min(chunkSize, pcm.Length - offset);
            var chunk = pcm.AsSpan(offset, length).ToArray();
            stream.AcceptWaveform(targetRate, chunk);
        }

        stream.InputFinished();

        while (recognizer.IsReady(stream))
        {
            cancellationToken.ThrowIfCancellationRequested();
            recognizer.Decode(stream);
        }

        var text = recognizer.GetResult(stream).Text?.Trim() ?? string.Empty;
        if (!SpeechTranscriptFilter.IsMeaningfulFinalTranscript(text))
        {
            _logger.LogInformation("STT batch ignorato (troppo corto o senza testo): {Text}", text);
            return Task.FromResult<string?>(null);
        }

        _logger.LogInformation("STT batch: {Text}", text);
        return Task.FromResult<string?>(text);
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
            EnableEndpoint = 0,
        };

        return new OnlineRecognizer(config);
    }
}
