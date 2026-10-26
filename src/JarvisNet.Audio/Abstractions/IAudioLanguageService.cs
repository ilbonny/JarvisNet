using JarvisNet.Audio.Models;

namespace JarvisNet.Audio.Abstractions;

public interface IAudioLanguageService
{
    string GetCurrentLanguageId();

    IReadOnlyList<AudioLanguageOption> ListLanguages();

    Task SetLanguageAsync(string languageId, CancellationToken cancellationToken = default);

    AudioSettingsSnapshot GetSettingsSnapshot();
}

public sealed record AudioLanguageOption(
    string Id,
    string Label,
    string CultureName,
    bool IsInstalled);

public sealed record AudioSettingsSnapshot(
    string CurrentLanguageId,
    string CurrentVoiceKey,
    IReadOnlyList<AudioLanguageOption> Languages,
    IReadOnlyList<PiperVoiceOption> Voices);
