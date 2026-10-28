namespace JarvisNet.UI.Options;

public sealed class ScheduledPromptsRootOptions
{
    public const string SectionName = "JarvisNet:Schedules";

    public bool Enabled { get; set; } = true;

    public List<ScheduledPromptItemOptions> Items { get; set; } = [];
}

public sealed class ScheduledPromptItemOptions
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    /// <summary>Ora locale nel formato HH:mm (24h).</summary>
    public string Time { get; set; } = "08:00";

    public string[] DaysOfWeek { get; set; } = [];

    public bool RunOnStartup { get; set; }

    public bool SpeakResponse { get; set; }

    public string Prompt { get; set; } = string.Empty;
}
