using System.Globalization;

namespace JarvisNet.SearchWeb;

internal static class WebSearchLocaleParameters
{
    internal static string ToDuckDuckGoKl(CultureInfo culture)
    {
        if (string.IsNullOrEmpty(culture.Name))
        {
            return string.Empty;
        }

        return culture.Name.ToLowerInvariant();
    }

    internal static GoogleNewsLocale ToGoogleNews(CultureInfo culture)
    {
        var name = culture.Name.ToUpperInvariant();
        return name switch
        {
            "IT-IT" or "IT" => new GoogleNewsLocale("it", "IT", "IT:it"),
            "ES-ES" or "ES" => new GoogleNewsLocale("es", "ES", "ES:es"),
            "ES-AR" => new GoogleNewsLocale("es", "AR", "AR:es"),
            "FR-FR" or "FR" => new GoogleNewsLocale("fr", "FR", "FR:fr"),
            "DE-DE" or "DE" => new GoogleNewsLocale("de", "DE", "DE:de"),
            "EN-GB" => new GoogleNewsLocale("en-GB", "GB", "GB:en"),
            _ => new GoogleNewsLocale("en-US", "US", "US:en"),
        };
    }

    internal readonly record struct GoogleNewsLocale(string Hl, string Gl, string Ceid)
    {
        internal string QueryString => $"hl={Hl}&gl={Gl}&ceid={Ceid}";
    }
}
