using System.Globalization;

namespace JarvisNet.Plugins.Sdk.Infrastructure;

public static class JarvisSystemPromptComposer
{
    public static string Compose(string basePrompt, string? cultureName)
    {
        var prompt = string.IsNullOrWhiteSpace(basePrompt)
            ? "You are JarvisNet, a concise voice assistant."
            : basePrompt.Trim();

        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return prompt;
        }

        var culture = JarvisLocaleResolver.CreateCulture(cultureName);
        var languageLine = culture.Name.ToUpperInvariant() switch
        {
            "IT-IT" => "Rispondi sempre in italiano, anche se l'utente mescola altre lingue.",
            "ES-ES" => "Responde siempre en español (España), aunque el usuario mezcle otros idiomas.",
            "ES-AR" => "Respondé siempre en español (Argentina), aunque el usuario mezcle otros idiomas.",
            "EN-US" => "Always respond in English (United States), even if the user mixes other languages.",
            _ => FormattableString.Invariant(
                $"Always respond in {culture.NativeName} (culture {culture.Name})."),
        };

        return $"{prompt}{Environment.NewLine}{Environment.NewLine}{languageLine}";
    }
}
