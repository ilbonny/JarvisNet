using JarvisNet.Engine.Options;
using JarvisNet.Engine.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace JarvisNet.Engine.UnitTests;

[TestFixture]
public sealed class OllamaBrainServiceBrowserRetryTests
{
    [Test]
    public async Task SendAsync_RetriesOnce_WhenBrowserAnswerHasNoMcpTools()
    {
        var fake = new FakeChatCompletionService((history, _, _) =>
        {
            var last = history.Last().Content ?? string.Empty;
            if (last.Contains("Non inventare hotel", StringComparison.Ordinal))
            {
                return new ChatMessageContent(AuthorRole.Assistant, "Sto aprendo il browser.");
            }

            return new ChatMessageContent(
                AuthorRole.Assistant,
                "Il sito di Booking.com è ora aperto. Ecco i risultati: **Albergo 1**: [Dettagli]");
        });

        var kernel = Kernel.CreateBuilder().Build();
        var service = new OllamaBrainService(
            fake,
            kernel,
            new OllamaOptions { SystemPrompt = "system" },
            NullLogger<OllamaBrainService>.Instance,
            new FakeMcpTurnScope());

        await service.SendAsync("Fai una ricerca su booking.com").ConfigureAwait(false);

        Assert.That(fake.CallCount, Is.EqualTo(2));
    }

    private sealed class FakeMcpTurnScope : IMcpTurnScope
    {
        public int ToolsInvokedThisTurn { get; private set; }

        public void BeginUserTurn() => ToolsInvokedThisTurn = 0;
    }
}
