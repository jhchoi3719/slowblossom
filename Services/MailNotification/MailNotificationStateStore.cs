using System.Text.Json;

namespace RotationDating.Web.Services.MailNotification;

public sealed class MailNotificationState
{
    public uint LastUid { get; set; }
    public uint? UidValidity { get; set; }
    public string Folder { get; set; } = "INBOX";
    public DateTime? LastCheckedAtUtc { get; set; }
    public DateTime? LastNotifiedAtUtc { get; set; }
    public string? LastNotifiedSubject { get; set; }
}

public sealed class MailNotificationStateStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public MailNotificationStateStore(IWebHostEnvironment environment)
    {
        var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");
        if (string.IsNullOrWhiteSpace(dataDir))
            dataDir = Directory.Exists("/data") ? "/data" : environment.ContentRootPath;

        _filePath = Path.Combine(dataDir, "mail-notification-state.json");
    }

    public async Task<MailNotificationState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
                return new MailNotificationState();

            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<MailNotificationState>(stream, cancellationToken: cancellationToken)
                ?? new MailNotificationState();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(MailNotificationState state, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, state, cancellationToken: cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }
}
