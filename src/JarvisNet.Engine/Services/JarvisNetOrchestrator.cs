using JarvisNet.Audio.Abstractions;
using JarvisNet.Audio.Events;
using JarvisNet.Core.Enums;
using JarvisNet.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace JarvisNet.Engine.Services;

public sealed class JarvisNetOrchestrator
{
    private static readonly string[] ExitPhrases = ["esci", "exit", "quit"];

    private readonly IAudioInputService _audioInput;
    private readonly ISpeechToTextService _speechToText;
    private readonly ITextToSpeechService _textToSpeech;
    private readonly OllamaBrainService _brain;
    private readonly ILogger<JarvisNetOrchestrator> _logger;
    private readonly SemaphoreSlim _processingLock = new(1, 1);

    private CancellationTokenSource? _sessionCts;
    private Task? _sessionTask;
    private bool _handlersAttached;
    private JarvisState _currentState = JarvisState.Idle;
    private bool _sessionActive;

    public JarvisNetOrchestrator(
        IAudioInputService audioInput,
        ISpeechToTextService speechToText,
        ITextToSpeechService textToSpeech,
        OllamaBrainService brain,
        ILogger<JarvisNetOrchestrator> logger)
    {
        _audioInput = audioInput;
        _speechToText = speechToText;
        _textToSpeech = textToSpeech;
        _brain = brain;
        _logger = logger;
    }

    public event EventHandler<string>? UserTranscriptReceived;

    public event EventHandler<string>? AssistantResponseReceived;

    public event EventHandler<string>? AssistantResponseChunk;

    public event EventHandler<JarvisState>? StateChanged;

    public event EventHandler<float>? AudioLevelChanged;

    public event EventHandler? ExitRequested;

    public Task StartVoiceSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_sessionTask is { IsCompleted: false })
        {
            return _sessionTask;
        }

        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        AttachHandlers();

        _sessionTask = RunSessionAsync(_sessionCts.Token);
        return _sessionTask;
    }

    public async Task StopVoiceSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_sessionCts is not null)
        {
            await _sessionCts.CancelAsync().ConfigureAwait(false);
        }

        if (_sessionTask is not null)
        {
            try
            {
                await _sessionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }

        DetachHandlers();
        _sessionCts?.Dispose();
        _sessionCts = null;
        _sessionTask = null;
        SetState(JarvisState.Idle);
    }

    private async Task RunSessionAsync(CancellationToken cancellationToken)
    {
        await _speechToText.StartSessionAsync(cancellationToken).ConfigureAwait(false);
        await _audioInput.StartAsync(cancellationToken).ConfigureAwait(false);
        _sessionActive = true;
        SetState(JarvisState.Listening);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // session ended
        }
        finally
        {
            _sessionActive = false;
            await _audioInput.StopAsync().ConfigureAwait(false);
            await _speechToText.StopSessionAsync(CancellationToken.None).ConfigureAwait(false);
            SetState(JarvisState.Idle);
        }
    }

    private void AttachHandlers()
    {
        if (_handlersAttached)
        {
            return;
        }

        _speechToText.SpeechRecognized += OnSpeechRecognized;
        _audioInput.AudioFrameAvailable += OnAudioFrameAvailable;
        _audioInput.AudioLevelChanged += OnAudioLevelChanged;
        _textToSpeech.SpeakingStarted += OnSpeakingStarted;
        _textToSpeech.SpeakingCompleted += OnSpeakingCompleted;
        _textToSpeech.PlaybackLevelChanged += OnPlaybackLevelChanged;
        _handlersAttached = true;
    }

    private void DetachHandlers()
    {
        if (!_handlersAttached)
        {
            return;
        }

        _speechToText.SpeechRecognized -= OnSpeechRecognized;
        _audioInput.AudioFrameAvailable -= OnAudioFrameAvailable;
        _audioInput.AudioLevelChanged -= OnAudioLevelChanged;
        _textToSpeech.SpeakingStarted -= OnSpeakingStarted;
        _textToSpeech.SpeakingCompleted -= OnSpeakingCompleted;
        _textToSpeech.PlaybackLevelChanged -= OnPlaybackLevelChanged;
        _handlersAttached = false;
    }

    private void OnAudioLevelChanged(object? sender, AudioLevelChangedEventArgs e)
    {
        if (_currentState == JarvisState.Speaking)
        {
            return;
        }

        AudioLevelChanged?.Invoke(this, e.Level);
    }

    private void OnPlaybackLevelChanged(object? sender, AudioLevelChangedEventArgs e)
    {
        if (_currentState != JarvisState.Speaking)
        {
            return;
        }

        AudioLevelChanged?.Invoke(this, e.Level);
    }

    private void OnSpeakingStarted(object? sender, EventArgs e)
    {
        SetState(JarvisState.Speaking);
    }

    private void OnSpeakingCompleted(object? sender, EventArgs e)
    {
        if (_sessionActive)
        {
            SetState(JarvisState.Listening);
        }
        else
        {
            SetState(JarvisState.Idle);
        }
    }

    private void SetState(JarvisState state)
    {
        if (_currentState == state)
        {
            return;
        }

        _currentState = state;
        StateChanged?.Invoke(this, state);
    }

    private void OnAudioFrameAvailable(object? sender, AudioFrameEventArgs e)
    {
        _speechToText.ProcessAudio(e.Samples, e.SampleRate);
    }

    private async void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (!e.IsFinal || !IsMeaningfulFinalTranscript(e.Text))
        {
            return;
        }

        var transcript = e.Text.Trim();
        if (IsExitCommand(transcript))
        {
            _logger.LogInformation("Comando di uscita rilevato: {Transcript}", transcript);
            ExitRequested?.Invoke(this, EventArgs.Empty);
            if (_sessionCts is not null)
            {
                await _sessionCts.CancelAsync().ConfigureAwait(false);
            }

            return;
        }

        if (!await _processingLock.WaitAsync(0).ConfigureAwait(false))
        {
            _logger.LogDebug("Ignoro transcript mentre è in corso una risposta: {Transcript}", transcript);
            return;
        }

        try
        {
            UserTranscriptReceived?.Invoke(this, transcript);
            _logger.LogInformation("Utente: {Transcript}", transcript);

            SetState(JarvisState.Thinking);

            var reply = await _brain.SendStreamingAsync(
                transcript,
                chunk => AssistantResponseChunk?.Invoke(this, chunk),
                CancellationToken.None).ConfigureAwait(false);

            AssistantResponseReceived?.Invoke(this, reply);
            _logger.LogInformation("JarvisNet: {Reply}", reply);

            var speechText = TtsSpeechFormatter.SanitizeForSpeech(reply);
            if (!string.IsNullOrWhiteSpace(speechText))
            {
                await _textToSpeech.SpeakAsync(speechText, CancellationToken.None).ConfigureAwait(false);
            }
            else if (_sessionActive)
            {
                SetState(JarvisState.Listening);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante il ciclo conversazionale.");
            if (_sessionActive)
            {
                SetState(JarvisState.Listening);
            }
        }
        finally
        {
            _processingLock.Release();
        }
    }

    public static bool IsMeaningfulFinalTranscript(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length < 2)
        {
            return false;
        }

        return trimmed.Any(char.IsLetterOrDigit);
    }

    public static bool IsExitCommand(string text)
    {
        var normalized = text.Trim().TrimEnd('.', '!', '?').ToLowerInvariant();
        return ExitPhrases.Contains(normalized);
    }
}
