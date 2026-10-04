using Microsoft.AspNetCore.SignalR;

namespace JarvisNet.UI.Hubs;

public sealed class JarvisHub : Hub
{
    public Task SimulateState(string state) =>
        Clients.All.SendAsync("StateChanged", state);

    public Task SimulateAudioLevel(float level) =>
        Clients.All.SendAsync("AudioLevel", level);

    public Task SimulateUserSpeech(string text) =>
        Clients.All.SendAsync("UserSpeech", text);

    public Task SimulateJarvisChunk(string chunk) =>
        Clients.All.SendAsync("JarvisChunk", chunk);

    public Task SimulateJarvisChunkEnd() =>
        Clients.All.SendAsync("JarvisChunkEnd");
}
