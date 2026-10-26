using JarvisNet.Audio;
using JarvisNet.Audio.Abstractions;
using JarvisNet.Audio.Models;
using JarvisNet.Engine.Abstractions;
using JarvisNet.Plugins.Sdk.Options;
using Microsoft.Extensions.Options;

namespace JarvisNet.UI.Services;

public sealed class AudioLanguageService : IAudioLanguageService
{
    private readonly IOptionsMonitor<AudioOptions> _audioOptions;
    private readonly IOptionsMonitor<JarvisLocaleOptions> _localeOptions;
    private readonly IPiperVoiceService _voices;
    private readonly ISpeechToTextRuntime _speechToTextRuntime;
    private readonly IJarvisNetEngine _engine;
    private readonly ILogger<AudioLanguageService> _logger;

    public AudioLanguageService(
        IOptionsMonitor<AudioOptions> audioOptions,
        IOptionsMonitor<JarvisLocaleOptions> localeOptions,
        IPiperVoiceService voices,
        ISpeechToTextRuntime speechToTextRuntime,
        IJarvisNetEngine engine,
        ILogger<AudioLanguageService> logger)
    {
        _audioOptions = audioOptions;
        _localeOptions = localeOptions;
        _voices = voices;
        _speechToTextRuntime = speechToTextRuntime;
        _engine = engine;
        _logger = logger;
    }

    private AudioOptions Audio => _audioOptions.CurrentValue;

    public string GetCurrentLanguageId() =>
        AudioLanguageCatalog.ResolveLanguageId(Audio, _localeOptions.CurrentValue.CultureName);

    public IReadOnlyList<AudioLanguageOption> ListLanguages()
    {
        return AudioLanguageCatalog.All
            .Select(p => new AudioLanguageOption(
                p.Id,
                p.Label,
                p.CultureName,
                AudioLanguageCatalog.IsSttInstalled(p, Audio)))
            .ToList();
    }

    public AudioSettingsSnapshot GetSettingsSnapshot() =>
        new(
            GetCurrentLanguageId(),
            _voices.GetCurrentVoiceKey(),
            ListLanguages(),
            _voices.ListVoices());

    public async Task SetLanguageAsync(string languageId, CancellationToken cancellationToken = default)
    {
        if (!AudioLanguageCatalog.TryGet(languageId, out var profile))
        {
            throw new ArgumentException($"Unknown language '{languageId}'.", nameof(languageId));
        }

        if (!AudioLanguageCatalog.IsSttInstalled(profile, Audio))
        {
            throw new ArgumentException(
                $"Speech models for '{profile.Label}' are not installed under models/stt/{profile.SttFolder}.",
                nameof(languageId));
        }

        AudioLanguageCatalog.ApplyToOptions(profile, Audio);
        _localeOptions.CurrentValue.CultureName = profile.CultureName;

        await _speechToTextRuntime.ReloadAfterProfileChangeAsync(cancellationToken).ConfigureAwait(false);

        var currentVoiceKey = _voices.GetCurrentVoiceKey();
        var voiceMatchesLanguage = PiperVoiceCatalog.TryGetVoice(currentVoiceKey, out var voiceInfo) &&
            string.Equals(voiceInfo.Locale, profile.Id, StringComparison.OrdinalIgnoreCase);
        if (!voiceMatchesLanguage)
        {
            var installedForLocale = _voices.ListVoices()
                .Where(v => v.IsInstalled && string.Equals(v.Locale, profile.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var nextKey = installedForLocale.FirstOrDefault(v =>
                    string.Equals(v.Key, profile.DefaultVoiceKey, StringComparison.OrdinalIgnoreCase))?.Key
                ?? installedForLocale.FirstOrDefault()?.Key;
            if (nextKey is not null)
            {
                await _voices.SetVoiceAsync(nextKey, cancellationToken).ConfigureAwait(false);
            }
        }

        _engine.ClearHistory();

        _logger.LogInformation(
            "Audio language set to {LanguageId} ({Culture}); conversation reset for new locale.",
            profile.Id,
            profile.CultureName);
    }
}
