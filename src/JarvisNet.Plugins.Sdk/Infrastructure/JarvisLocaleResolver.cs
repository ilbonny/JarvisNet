using System.Globalization;
using JarvisNet.Plugins.Sdk.Options;
using Microsoft.Extensions.Configuration;

namespace JarvisNet.Plugins.Sdk.Infrastructure;

public static class JarvisLocaleResolver
{
    public static string ResolveCultureName(IConfiguration configuration)
    {
        var explicitName = configuration[$"{JarvisLocaleOptions.SectionName}:CultureName"];
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return CreateCulture(explicitName).Name;
        }

        var fromVoice = TryCultureFromPiperVoiceKey(configuration["JarvisNet:Audio:PiperVoiceKey"]);
        if (fromVoice is not null)
        {
            return fromVoice;
        }

        var fromModel = TryCultureFromPiperModelPath(configuration["JarvisNet:Audio:PiperModelPath"]);
        if (fromModel is not null)
        {
            return fromModel;
        }

        return "en-US";
    }

    public static CultureInfo ResolveCulture(IConfiguration configuration) =>
        CreateCulture(ResolveCultureName(configuration));

    public static CultureInfo CreateCulture(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return CultureInfo.GetCultureInfo("en-US");
        }

        try
        {
            return CultureInfo.GetCultureInfo(cultureName.Trim());
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en-US");
        }
    }

    internal static string? TryCultureFromPiperVoiceKey(string? voiceKey)
    {
        if (string.IsNullOrWhiteSpace(voiceKey))
        {
            return null;
        }

        var dash = voiceKey.IndexOf('-', StringComparison.Ordinal);
        var localeToken = dash > 0 ? voiceKey[..dash] : voiceKey;
        return LocaleTokenToCultureName(localeToken);
    }

    internal static string? TryCultureFromPiperModelPath(string? modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return null;
        }

        var fileName = Path.GetFileName(modelPath);
        var underscore = fileName.IndexOf('_', StringComparison.Ordinal);
        if (underscore <= 0)
        {
            return null;
        }

        var dash = fileName.IndexOf('-', underscore + 1);
        if (dash <= underscore)
        {
            return null;
        }

        return LocaleTokenToCultureName(fileName[..dash]);
    }

    private static string? LocaleTokenToCultureName(string localeToken)
    {
        var normalized = localeToken.Trim().Replace('_', '-');
        if (normalized.Length < 2)
        {
            return null;
        }

        try
        {
            _ = CultureInfo.GetCultureInfo(normalized);
            return normalized;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
