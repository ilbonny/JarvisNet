namespace JarvisNet.Plugins.Sdk.Options;

/// <summary>
/// Lingua delle risposte e dei risultati di ricerca web (BCP-47, es. <c>it-IT</c>).
/// Se <see cref="CultureName"/> è vuoto, viene derivata da <c>JarvisNet:Audio</c> (voce Piper).
/// </summary>
public sealed class JarvisLocaleOptions
{
    public const string SectionName = "JarvisNet:Locale";

    public string CultureName { get; set; } = string.Empty;
}
