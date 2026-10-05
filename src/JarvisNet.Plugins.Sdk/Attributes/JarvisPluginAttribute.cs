namespace JarvisNet.Plugins.Sdk.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class JarvisPluginAttribute : Attribute
{
    public JarvisPluginAttribute(string name, string version, string description)
    {
        Name = name;
        Version = version;
        Description = description;
    }

    public string Name { get; }

    public string Version { get; }

    public string Description { get; }
}
