using System.Text.Json;
using System.Text.Json.Serialization;
using JarvisNet.Engine.Infrastructure;
using JarvisNet.UI.Models;
using JarvisNet.UI.Options;
using Microsoft.Extensions.Logging;

namespace JarvisNet.UI.Services;

public sealed class ScheduledPromptStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _configPath;
    private readonly ILogger<ScheduledPromptStore> _logger;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private ScheduledPromptsRootOptions _snapshot = new();

    public event EventHandler? Changed;

    public ScheduledPromptStore(string contentRootPath, ILogger<ScheduledPromptStore> logger)
    {
        var repositoryRoot = RepositoryRootLocator.Find(contentRootPath);
        _configPath = Path.Combine(repositoryRoot, "config", "jarvisnet.schedules.json");
        _logger = logger;
        _snapshot = LoadFromDisk();
    }

    public ScheduledPromptsRootOptions GetSnapshot() => Clone(_snapshot);

    public ScheduledPromptsResponse ToResponse(ScheduledPromptsRootOptions options) =>
        new(
            options.Enabled,
            options.Items.Select(ToItemDto).ToList());

    public async Task SaveAsync(ScheduledPromptsUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = new ScheduledPromptsRootOptions
        {
            Enabled = request.Enabled,
            Items = request.Items.Select(NormalizeItem).ToList(),
        };

        Validate(normalized);

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var payload = new SchedulesFileRoot
            {
                JarvisNet = new SchedulesFileJarvisNet
                {
                    Schedules = normalized,
                },
            };

            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(_configPath);
            await JsonSerializer.SerializeAsync(stream, payload, JsonOptions, cancellationToken).ConfigureAwait(false);
            _snapshot = Clone(normalized);
            _logger.LogInformation("Schedules salvati in {Path} ({Count} voci).", _configPath, _snapshot.Items.Count);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public void ReloadFromDisk()
    {
        _ioLock.Wait();
        try
        {
            _snapshot = LoadFromDisk();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private ScheduledPromptsRootOptions LoadFromDisk()
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogWarning("File schedules assente ({Path}); uso configurazione vuota.", _configPath);
            return new ScheduledPromptsRootOptions();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var root = JsonSerializer.Deserialize<SchedulesFileRoot>(json, JsonOptions);
            return Clone(root?.JarvisNet?.Schedules ?? new ScheduledPromptsRootOptions());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Impossibile leggere {Path}; uso configurazione vuota.", _configPath);
            return new ScheduledPromptsRootOptions();
        }
    }

    private static void Validate(ScheduledPromptsRootOptions options)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in options.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                throw new ArgumentException("Ogni schedule deve avere un Id.");
            }

            if (!ids.Add(item.Id.Trim()))
            {
                throw new ArgumentException($"Id duplicato: '{item.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(item.Prompt))
            {
                throw new ArgumentException($"Prompt obbligatorio per '{item.Id}'.");
            }

            if (!TimeOnly.TryParse(item.Time, out _))
            {
                throw new ArgumentException($"Orario non valido '{item.Time}' per '{item.Id}' (usa HH:mm).");
            }
        }
    }

    private static ScheduledPromptItemOptions NormalizeItem(ScheduledPromptItemDto dto) =>
        new()
        {
            Id = dto.Id.Trim(),
            Name = string.IsNullOrWhiteSpace(dto.Name) ? dto.Id.Trim() : dto.Name.Trim(),
            Enabled = dto.Enabled,
            Time = dto.Time.Trim(),
            DaysOfWeek = dto.DaysOfWeek
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            RunOnStartup = dto.RunOnStartup,
            SpeakResponse = dto.SpeakResponse,
            Prompt = dto.Prompt.Trim(),
        };

    private static ScheduledPromptItemDto ToItemDto(ScheduledPromptItemOptions item) =>
        new(
            item.Id,
            item.Name,
            item.Enabled,
            item.Time,
            item.DaysOfWeek,
            item.RunOnStartup,
            item.SpeakResponse,
            item.Prompt);

    private static ScheduledPromptsRootOptions Clone(ScheduledPromptsRootOptions source) =>
        new()
        {
            Enabled = source.Enabled,
            Items = source.Items.Select(item => new ScheduledPromptItemOptions
            {
                Id = item.Id,
                Name = item.Name,
                Enabled = item.Enabled,
                Time = item.Time,
                DaysOfWeek = item.DaysOfWeek.ToArray(),
                RunOnStartup = item.RunOnStartup,
                SpeakResponse = item.SpeakResponse,
                Prompt = item.Prompt,
            }).ToList(),
        };

    private sealed class SchedulesFileRoot
    {
        public SchedulesFileJarvisNet? JarvisNet { get; set; }
    }

    private sealed class SchedulesFileJarvisNet
    {
        public ScheduledPromptsRootOptions? Schedules { get; set; }
    }
}
