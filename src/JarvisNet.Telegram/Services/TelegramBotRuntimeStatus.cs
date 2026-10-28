namespace JarvisNet.Telegram.Services;

public sealed class TelegramBotRuntimeStatus
{
    private readonly object _sync = new();
    private bool _isRunning;
    private string? _username;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _isRunning;
            }
        }
    }

    public string? Username
    {
        get
        {
            lock (_sync)
            {
                return _username;
            }
        }
    }

    public void SetRunning(string? username)
    {
        lock (_sync)
        {
            _isRunning = true;
            _username = string.IsNullOrWhiteSpace(username) ? null : username.Trim().TrimStart('@');
        }
    }

    public void SetStopped()
    {
        lock (_sync)
        {
            _isRunning = false;
            _username = null;
        }
    }
}
