namespace JarvisNet.Engine.Services;

internal static class BrowserResponseGuard
{
    internal const string ToolNudgeUserMessage =
        "Usa adesso i tool del browser registrati per navigare o leggere la pagina. "
        + "Non inventare hotel, prezzi, punteggi o liste: riporta solo ciò che vedi dopo i tool.";

    internal static bool LikelyWebUserIntent(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return false;
        }

        var text = userMessage.ToLowerInvariant();
        ReadOnlySpan<string> hints =
        [
            "browser", "sito", "pagina", "apri", "cerca", "ricerca", "booking", "hotel",
            "albergo", "amazon", "naviga", "internet", "clicca", "prenot", "destinazione",
            "snapshot", "scheda", "tab ",
        ];

        foreach (var hint in hints)
        {
            if (text.Contains(hint, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool LooksLikeFabricatedBrowserAnswer(string assistantText)
    {
        if (string.IsNullOrWhiteSpace(assistantText))
        {
            return false;
        }

        var text = assistantText.ToLowerInvariant();

        ReadOnlySpan<string> refusalOrGuide =
        [
            "non posso navigare",
            "non posso accedere",
            "ti guiderò",
            "passaggi che dovresti",
            "simulare questi passaggi",
            "simuli questo",
        ];

        foreach (var phrase in refusalOrGuide)
        {
            if (text.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        ReadOnlySpan<string> falseCompletion =
        [
            "è ora aperto",
            "è stato aperto",
            "è stata effettuata",
            "ricerca è stata",
            "browser è ora",
            "browser è stato",
            "ecco i risultati",
            "[dettagli]",
            "albergo 1",
            "**albergo",
        ];

        foreach (var phrase in falseCompletion)
        {
            if (text.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (text.Contains("punteggio", StringComparison.Ordinal)
            && text.Any(char.IsDigit))
        {
            return true;
        }

        return text.Contains('€') && text.Any(char.IsDigit);
    }

    internal static bool ShouldRetryWithoutTools(string userMessage, string assistantText, int toolsInvoked) =>
        toolsInvoked == 0
        && LikelyWebUserIntent(userMessage)
        && LooksLikeFabricatedBrowserAnswer(assistantText);
}
