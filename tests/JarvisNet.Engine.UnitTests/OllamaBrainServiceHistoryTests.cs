using JarvisNet.Engine.Options;
using JarvisNet.Engine.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JarvisNet.Engine.UnitTests;

[TestFixture]
public sealed class OllamaBrainServiceHistoryTests
{
    [Test]
    public async Task SendAsync_AppendsUserAndAssistantMessages()
    {
        var chatCompletion = new FakeChatCompletionService(history =>
            new ChatMessageContent(AuthorRole.Assistant, $"Risposta a: {history.Last().Content}"));

        var options = new OllamaOptions { SystemPrompt = "system-test" };
        var service = new OllamaBrainService(
            chatCompletion,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<OllamaBrainService>.Instance);

        await service.SendAsync("Ciao").ConfigureAwait(false);
        await service.SendAsync("Come stai?").ConfigureAwait(false);

        var snapshot = service.GetHistorySnapshot();
        Assert.That(snapshot.Count, Is.EqualTo(5));
        Assert.That(snapshot[0].Role, Is.EqualTo(AuthorRole.System));
        Assert.That(snapshot[1].Role, Is.EqualTo(AuthorRole.User));
        Assert.That(snapshot[1].Content, Is.EqualTo("Ciao"));
        Assert.That(snapshot[2].Role, Is.EqualTo(AuthorRole.Assistant));
        Assert.That(snapshot[3].Role, Is.EqualTo(AuthorRole.User));
        Assert.That(snapshot[3].Content, Is.EqualTo("Come stai?"));
        Assert.That(snapshot[4].Role, Is.EqualTo(AuthorRole.Assistant));
    }

    [Test]
    public async Task ClearHistory_KeepsOnlySystemPrompt()
    {
        var chatCompletion = new FakeChatCompletionService(_ =>
            new ChatMessageContent(AuthorRole.Assistant, "ok"));

        var options = new OllamaOptions { SystemPrompt = "system-test" };
        var service = new OllamaBrainService(
            chatCompletion,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<OllamaBrainService>.Instance);

        await service.SendAsync("Ciao").ConfigureAwait(false);
        service.ClearHistory();

        var snapshot = service.GetHistorySnapshot();
        Assert.That(snapshot.Count, Is.EqualTo(1));
        Assert.That(snapshot[0].Role, Is.EqualTo(AuthorRole.System));
        Assert.That(snapshot[0].Content, Is.EqualTo("system-test"));
    }
}
