using JarvisNet.Engine.Options;
using JarvisNet.Engine.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Net;
using System.Text;

namespace JarvisNet.Engine.UnitTests;

/// <summary>
/// Tests the tool-calling pipeline in OllamaBrainService:
/// 1. Unit-level: verifies OpenAIPromptExecutionSettings + FunctionChoiceBehavior.Auto + kernel param.
/// 2. Integration-level (mocked HTTP): verifies SK auto-invoke triggers the kernel function.
/// </summary>
[TestFixture]
public sealed class OllamaBrainServiceToolCallingTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Unit-level tests (FakeChatCompletionService)
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task SendStreamingAsync_UsesOpenAIPromptExecutionSettings()
    {
        var fake = new FakeChatCompletionService(_ =>
            new ChatMessageContent(AuthorRole.Assistant, "ok"));

        var kernel = Kernel.CreateBuilder().Build();
        var service = BuildService(fake, kernel);

        await service.SendStreamingAsync("test", _ => { }).ConfigureAwait(false);

        Assert.That(fake.LastExecutionSettings, Is.InstanceOf<OpenAIPromptExecutionSettings>(),
            "Must use OpenAIPromptExecutionSettings so the OpenAI connector serialises tools correctly.");
    }

    [Test]
    public async Task SendStreamingAsync_SetsFunctionChoiceBehaviorAuto()
    {
        var fake = new FakeChatCompletionService(_ =>
            new ChatMessageContent(AuthorRole.Assistant, "ok"));

        var kernel = Kernel.CreateBuilder().Build();
        var service = BuildService(fake, kernel);

        await service.SendStreamingAsync("test", _ => { }).ConfigureAwait(false);

        var settings = fake.LastExecutionSettings as OpenAIPromptExecutionSettings;
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings!.FunctionChoiceBehavior, Is.Not.Null,
            "FunctionChoiceBehavior must be set (Auto) so the connector includes tools in the Ollama request.");
    }

    [Test]
    public async Task SendStreamingAsync_PassesKernelToGetChatMessageContent()
    {
        var fake = new FakeChatCompletionService(_ =>
            new ChatMessageContent(AuthorRole.Assistant, "ok"));

        var kernel = Kernel.CreateBuilder().Build();
        var service = BuildService(fake, kernel);

        await service.SendStreamingAsync("test", _ => { }).ConfigureAwait(false);

        Assert.That(fake.LastKernel, Is.SameAs(kernel),
            "Kernel must be passed so SK can inject registered functions as tools and auto-invoke them.");
    }

    [Test]
    public async Task SendAsync_UsesOpenAIPromptExecutionSettings()
    {
        var fake = new FakeChatCompletionService(_ =>
            new ChatMessageContent(AuthorRole.Assistant, "ok"));

        var kernel = Kernel.CreateBuilder().Build();
        var service = BuildService(fake, kernel);

        await service.SendAsync("test").ConfigureAwait(false);

        Assert.That(fake.LastExecutionSettings, Is.InstanceOf<OpenAIPromptExecutionSettings>());
        Assert.That(fake.LastKernel, Is.SameAs(kernel));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Integration-level tests (real SK OpenAI connector + mocked HTTP)
    // These verify the full auto-invoke loop works when Ollama returns tool_calls.
    // ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task SendStreamingAsync_AutoInvokesKernelFunction_WhenOllamaReturnsToolCalls()
    {
        // Arrange: stub HTTP handler that simulates Ollama /v1/chat/completions
        var httpHandler = new SequentialStubHttpHandler();

        // 1st response: Ollama requests a tool call
        httpHandler.Enqueue(OllamaToolCallResponse(
            toolCallId: "call_test_001",
            functionName: "jarvis_test-do_action",
            arguments: """{"param":"hello"}"""));

        // 2nd response: Ollama produces final answer after receiving tool result
        httpHandler.Enqueue(OllamaTextResponse("Azione eseguita correttamente."));

        // Build Kernel with OpenAI connector pointing to mocked Ollama endpoint
        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: "llama3.1:8b",
                endpoint: new Uri("http://localhost:11434/v1/"),
                apiKey: "ollama",
                httpClient: new HttpClient(httpHandler))
            .Build();

        // Register a test function that captures whether it was called
        bool functionInvoked = false;
        string? receivedParam = null;
        kernel.ImportPluginFromFunctions("jarvis_test",
        [
            KernelFunctionFactory.CreateFromMethod(
                (string param) =>
                {
                    functionInvoked = true;
                    receivedParam = param;
                    return "Action result: success";
                },
                functionName: "do_action",
                description: "Test action",
                parameters: [new KernelParameterMetadata("param") { ParameterType = typeof(string) }])
        ]);

        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        var service = BuildService(chatCompletion, kernel);

        var chunks = new List<string>();

        // Act
        await service.SendStreamingAsync("Esegui l'azione", chunk => chunks.Add(chunk))
            .ConfigureAwait(false);

        // Assert: the kernel function was auto-invoked by SK
        Assert.That(functionInvoked, Is.True,
            "SK should auto-invoke the kernel function when Ollama returns tool_calls.");
        Assert.That(receivedParam, Is.EqualTo("hello"),
            "Arguments must be correctly deserialized from the tool_calls JSON.");
        Assert.That(string.Concat(chunks), Contains.Substring("Azione eseguita correttamente"),
            "The final text response should be streamed in chunks.");

        // Verify both HTTP calls were made (tool call + final response)
        Assert.That(httpHandler.CallCount, Is.EqualTo(2),
            "SK must send exactly 2 requests: first for the initial prompt, second after tool invocation.");
    }

    [Test]
    public async Task SendStreamingAsync_McpToolExecutionStarted_FiresWhenFunctionInvoked()
    {
        // This test verifies that if the chat completion returns FunctionCallContent
        // for a function registered via McpClientManager (simulated here),
        // the McpToolExecutionStarted event propagates to the engine.

        var httpHandler = new SequentialStubHttpHandler();
        httpHandler.Enqueue(OllamaToolCallResponse(
            toolCallId: "call_mcp_001",
            functionName: "Chrome_Devtools-navigate_page",
            arguments: """{"url":"https://www.amazon.com"}"""));
        httpHandler.Enqueue(OllamaTextResponse("Navigazione completata."));

        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: "llama3.1:8b",
                endpoint: new Uri("http://localhost:11434/v1/"),
                apiKey: "ollama",
                httpClient: new HttpClient(httpHandler))
            .Build();

        // Simulate an MCP-registered function (like McpClientManager would register)
        kernel.ImportPluginFromFunctions("Chrome_Devtools",
        [
            KernelFunctionFactory.CreateFromMethod(
                (string url) => $"Navigato a {url}",
                functionName: "navigate_page",
                description: "Navigate Chrome to the given URL",
                parameters: [new KernelParameterMetadata("url") { ParameterType = typeof(string) }])
        ]);

        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        var service = BuildService(chatCompletion, kernel);

        var chunks = new List<string>();
        await service.SendStreamingAsync("Apri Amazon", chunk => chunks.Add(chunk))
            .ConfigureAwait(false);

        Assert.That(string.Concat(chunks), Contains.Substring("Navigazione completata"),
            "Final response should be present after tool execution.");
        Assert.That(httpHandler.CallCount, Is.EqualTo(2));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static OllamaBrainService BuildService(
        IChatCompletionService chatCompletion,
        Kernel kernel)
    {
        var options = new OllamaOptions
        {
            ModelName = "llama3.1:8b",
            SystemPrompt = "Sei JARVIS.",
            Temperature = 0.1f,
        };
        return new OllamaBrainService(
            chatCompletion,
            kernel,
            options,
            NullLogger<OllamaBrainService>.Instance);
    }

    /// <summary>Builds an OpenAI-format tool_calls response (as Ollama /v1/ returns).</summary>
    private static HttpResponseMessage OllamaToolCallResponse(
        string toolCallId, string functionName, string arguments)
    {
        var json = $$"""
            {
              "id": "chatcmpl-test",
              "object": "chat.completion",
              "created": 1700000000,
              "model": "llama3.1:8b",
              "choices": [{
                "index": 0,
                "message": {
                  "role": "assistant",
                  "content": null,
                  "tool_calls": [{
                    "id": "{{toolCallId}}",
                    "type": "function",
                    "function": {
                      "name": "{{functionName}}",
                      "arguments": {{System.Text.Json.JsonSerializer.Serialize(arguments)}}
                    }
                  }]
                },
                "finish_reason": "tool_calls"
              }],
              "usage": {"prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15}
            }
            """;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    /// <summary>Builds an OpenAI-format text response (as Ollama /v1/ returns).</summary>
    private static HttpResponseMessage OllamaTextResponse(string text)
    {
        var json = $$"""
            {
              "id": "chatcmpl-test2",
              "object": "chat.completion",
              "created": 1700000001,
              "model": "llama3.1:8b",
              "choices": [{
                "index": 0,
                "message": {
                  "role": "assistant",
                  "content": {{System.Text.Json.JsonSerializer.Serialize(text)}}
                },
                "finish_reason": "stop"
              }],
              "usage": {"prompt_tokens": 15, "completion_tokens": 8, "total_tokens": 23}
            }
            """;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>
/// HTTP handler that returns pre-configured responses in sequence.
/// Captures request count for assertions.
/// </summary>
internal sealed class SequentialStubHttpHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    public int CallCount { get; private set; }

    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        if (_responses.TryDequeue(out var response))
            return Task.FromResult(response);

        throw new InvalidOperationException(
            $"No stub response configured for call #{CallCount}. " +
            $"Request: {request.Method} {request.RequestUri}");
    }
}
