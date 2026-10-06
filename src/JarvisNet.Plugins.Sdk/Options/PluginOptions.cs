namespace JarvisNet.Plugins.Sdk.Options;

public sealed class PluginOptions
{
    public const string SectionName = "JarvisNet:Plugins";

    /// <summary>Cartella con le DLL dei plugin (.dll). Se vuota, usa output/Plugins o repo/Plugins.</summary>
    public string? Folder { get; set; }
}
