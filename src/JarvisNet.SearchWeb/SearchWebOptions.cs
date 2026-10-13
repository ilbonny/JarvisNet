namespace JarvisNet.SearchWeb;

public sealed class SearchWebOptions
{
    public const string SectionName = "JarvisNet:SearchWeb";

    /// <summary>Brave Search API subscription token. Prefer env <c>BRAVE_SEARCH_API_KEY</c> for secrets.</summary>
    public string? ApiKey { get; set; }

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. IT, US). Defaults from app locale.</summary>
    public string? Country { get; set; }

    /// <summary>Search language code (e.g. it, en). Defaults from app locale.</summary>
    public string? SearchLang { get; set; }
}
