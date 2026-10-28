using JarvisNet.Audio.Abstractions;
using JarvisNet.Engine;
using JarvisNet.Engine.Abstractions;
using JarvisNet.UI.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JarvisNet.UI.Services;

public sealed class ScheduledPromptHostedService : BackgroundService
{
    private readonly ScheduledPromptStore _store;
    private readonly IJarvisNetEngine _engine;
    private readonly ITextToSpeechService _tts;
    private readonly ILogger<ScheduledPromptHostedService> _logger;
    private readonly object _runStateLock = new();
    private readonly HashSet<string> _completedTodayKeys = new(StringComparer.OrdinalIgnoreCase);
    private DateOnly _trackedDay;
    private ScheduledPromptsRootOptions _options = new();
    public ScheduledPromptHostedService(
        ScheduledPromptStore store,
        IJarvisNetEngine engine,
        ITextToSpeechService tts,
        ILogger<ScheduledPromptHostedService> logger)
    {
        _store = store;
        _engine = engine;
        _tts = tts;
        _logger = logger;
        _options = store.GetSnapshot();
        _trackedDay = DateOnly.FromDateTime(DateTime.Now);
        _store.Changed += OnStoreChanged;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken).ConfigureAwait(false);
        await RunStartupCatchUpAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            ResetDayIfNeeded();
            await EvaluateDueSchedulesAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _store.Changed -= OnStoreChanged;
        return base.StopAsync(cancellationToken);
    }

    private void OnStoreChanged(object? sender, EventArgs e) =>
        _options = _store.GetSnapshot();

    private void ResetDayIfNeeded()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (today == _trackedDay)
        {
            return;
        }

        lock (_runStateLock)
        {
            if (today != _trackedDay)
            {
                _trackedDay = today;
                _completedTodayKeys.Clear();
            }
        }
    }

    private async Task RunStartupCatchUpAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var now = DateTime.Now;
        foreach (var item in _options.Items.Where(i => i.Enabled && i.RunOnStartup))
        {
            if (!IsDayAllowed(item, now))
            {
                continue;
            }

            if (!TimeOnly.TryParse(item.Time, out var scheduled))
            {
                continue;
            }

            var scheduledToday = now.Date.Add(scheduled.ToTimeSpan());
            if (now < scheduledToday)
            {
                continue;
            }

            await TryRunOnceAsync(item, scheduledToday, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EvaluateDueSchedulesAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var now = DateTime.Now;
        foreach (var item in _options.Items.Where(i => i.Enabled))
        {
            if (!IsDayAllowed(item, now))
            {
                continue;
            }

            if (!TimeOnly.TryParse(item.Time, out var scheduled))
            {
                continue;
            }

            if (now.Hour != scheduled.Hour || now.Minute != scheduled.Minute)
            {
                continue;
            }

            var slot = now.Date.Add(scheduled.ToTimeSpan());
            await TryRunOnceAsync(item, slot, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TryRunOnceAsync(
        ScheduledPromptItemOptions item,
        DateTime slotLocal,
        CancellationToken cancellationToken)
    {
        var key = $"{item.Id}:{DateOnly.FromDateTime(slotLocal):yyyy-MM-dd}";
        lock (_runStateLock)
        {
            if (!_completedTodayKeys.Add(key))
            {
                return;
            }
        }

        var label = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
        var userLine = $"[Schedule · {label}] {item.Prompt}";

        _logger.LogInformation("Esecuzione schedule {ScheduleId} ({Slot}).", item.Id, slotLocal);

        try
        {
            var reply = await _engine.RunPromptTurnAsync(userLine, cancellationToken).ConfigureAwait(false);

            if (item.SpeakResponse && !string.IsNullOrWhiteSpace(reply))
            {
                var speech = TtsSpeechFormatter.SanitizeForSpeech(reply);
                if (!string.IsNullOrWhiteSpace(speech))
                {
                    await _tts.SpeakAsync(speech, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante lo schedule {ScheduleId}.", item.Id);
            lock (_runStateLock)
            {
                _completedTodayKeys.Remove(key);
            }
        }
    }

    private static bool IsDayAllowed(ScheduledPromptItemOptions item, DateTime instant)
    {
        if (item.DaysOfWeek.Length == 0)
        {
            return true;
        }

        var todayName = instant.DayOfWeek.ToString();
        return item.DaysOfWeek.Any(d => string.Equals(d, todayName, StringComparison.OrdinalIgnoreCase));
    }
}
