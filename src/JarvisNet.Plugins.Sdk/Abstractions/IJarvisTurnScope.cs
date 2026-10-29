namespace JarvisNet.Plugins.Sdk.Abstractions;

/// <summary>
/// Contatore tool invocati nel turno utente (plugin kernel e MCP). Usato dal motore per retry e guard.
/// </summary>
public interface IJarvisTurnScope
{
    void BeginUserTurn(string sessionKey);

    void ResetToolsInvokedThisTurn();

    string CurrentSessionKey { get; }

    int ToolsInvokedThisTurn { get; }
}
