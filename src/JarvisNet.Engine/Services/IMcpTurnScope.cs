namespace JarvisNet.Engine.Services;

/// <summary>Per-turn MCP counters used by the brain (retry when the model skips tools).</summary>
public interface IMcpTurnScope
{
    void BeginUserTurn();

    int ToolsInvokedThisTurn { get; }
}
