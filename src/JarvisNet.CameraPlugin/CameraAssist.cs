using System.Text.RegularExpressions;

namespace JarvisNet.CameraPlugin;

internal static partial class CameraAssist
{
    internal enum CameraDirectAction
    {
        ListDevices,
        CapturePhoto,
        CaptureVideo,
        SetMotionOn,
        SetMotionOff,
    }

    internal static bool TryParseDirectAction(
        string userMessage,
        out CameraDirectAction action,
        out string deviceQuery,
        out int videoSeconds)
    {
        action = default;
        deviceQuery = string.Empty;
        videoSeconds = 5;

        var text = userMessage.Trim();
        if (text.Length < 3)
        {
            return false;
        }

        deviceQuery = ExtractDeviceQuery(text);

        if (IsMotionOffRequest(text))
        {
            action = CameraDirectAction.SetMotionOff;
            return true;
        }

        if (IsMotionOnRequest(text))
        {
            action = CameraDirectAction.SetMotionOn;
            return true;
        }

        if (IsVideoRequest(text))
        {
            action = CameraDirectAction.CaptureVideo;
            videoSeconds = ExtractVideoSeconds(text);
            return true;
        }

        if (IsPhotoRequest(text))
        {
            action = CameraDirectAction.CapturePhoto;
            return true;
        }

        if (IsListDevicesRequest(text))
        {
            action = CameraDirectAction.ListDevices;
            return true;
        }

        return false;
    }

    private static bool IsListDevicesRequest(string text)
    {
        if (ListDevicesPattern().IsMatch(text))
        {
            return true;
        }

        return ContainsAny(text, "dispositivi connessi", "telecamere connesse", "lista cam", "liste des cam");
    }

    private static bool IsPhotoRequest(string text) =>
        ContainsAny(text, "foto", "photo", "picture", "scatta", "scattare", "mandami una foto", "fammi una foto", "take a photo")
        && !IsVideoRequest(text);

    private static bool IsVideoRequest(string text) =>
        ContainsAny(text, "video", "filmato", "clip", "registra un video", "breve video");

    private static bool IsMotionOnRequest(string text) =>
        ContainsAny(text, "attiva il movimento", "attiva movimento", "enable motion", "motion on")
        || (ContainsAny(text, "movimento", "motion") && ContainsAny(text, "attiva", "enable", "accendi"));

    private static bool IsMotionOffRequest(string text) =>
        ContainsAny(text, "disattiva il movimento", "disattiva movimento", "disable motion", "motion off")
        || (ContainsAny(text, "movimento", "motion") && ContainsAny(text, "disattiva", "disable", "spegni"));

    private static int ExtractVideoSeconds(string text)
    {
        var match = VideoSecondsPattern().Match(text);
        if (match.Success
            && int.TryParse(match.Groups[1].Value, out var seconds))
        {
            return Math.Clamp(seconds, 1, 10);
        }

        return 5;
    }

    private static string ExtractDeviceQuery(string text)
    {
        var cam = CamLabelPattern().Match(text);
        if (cam.Success)
        {
            return cam.Groups[1].Value.ToUpperInvariant();
        }

        var cameraNumber = CameraNumberPattern().Match(text);
        if (cameraNumber.Success)
        {
            return FormattableString.Invariant($"CAM{cameraNumber.Groups[1].Value}");
        }

        return string.Empty;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(
        @"\b(lista|elenco|quali|mostra).{0,40}(cam|telecam|camera|dispositiv)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ListDevicesPattern();

    [GeneratedRegex(@"\bCAM\s*(\d+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CamLabelPattern();

    [GeneratedRegex(
        @"\bcamera?\s*(\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CameraNumberPattern();

    [GeneratedRegex(
        @"(\d{1,2})\s*(secondi|sec|s\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VideoSecondsPattern();
}
