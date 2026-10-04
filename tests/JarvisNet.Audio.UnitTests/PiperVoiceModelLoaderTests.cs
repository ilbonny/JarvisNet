using JarvisNet.Audio.Internal;
using JarvisNet.Audio.Models;

namespace JarvisNet.Audio.UnitTests;

[TestFixture]
public sealed class PiperVoiceModelLoaderTests
{
    [Test]
    public async Task LoadFromOnnxPathAsync_BuildsModel_FromRepoLayout()
    {
        var repoRoot = RepositoryPaths.FindRepositoryRoot(AppContext.BaseDirectory);
        var options = new AudioOptions { ModelsRoot = RepositoryPaths.GetModelsDirectory(repoRoot) };
        var onnxPath = options.ResolvePath("tts/it_IT-paola-medium.onnx");
        if (!File.Exists(onnxPath))
        {
            Assert.Ignore($"Voice model not present at {onnxPath}");
        }

        var model = await PiperVoiceModelLoader.LoadFromOnnxPathAsync(onnxPath);

        Assert.That(model.ModelLocation, Is.EqualTo(Path.GetDirectoryName(Path.GetFullPath(onnxPath))));
        Assert.That(model.GetModelLocation(), Does.Contain("it_IT-paola-medium.onnx"));
        Assert.That(model.Audio?.SampleRate, Is.EqualTo(22050u));
    }
}
