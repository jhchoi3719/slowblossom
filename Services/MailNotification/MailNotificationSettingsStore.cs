using System.Text.Json;

namespace RotationDating.Web.Services.MailNotification;

public sealed class MailNotificationStoredSettings
{
    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 60;
    public string Host { get; set; } = "imap.naver.com";
    public int Port { get; set; } = 993;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Folder { get; set; } = "INBOX";
    public string? FromFilter { get; set; }
    public string? SubjectContains { get; set; } = "네이버폼";
    public string TelegramBotToken { get; set; } = "";
    public string TelegramChatId { get; set; } = "";
}

public sealed class MailNotificationEffectiveSettings
{
    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 60;
    public NaverImapOptions NaverImap { get; set; } = new();
    public TelegramOptions Telegram { get; set; } = new();
    public bool EnabledLockedByEnv { get; set; }
    public bool UsernameLockedByEnv { get; set; }
    public bool PasswordLockedByEnv { get; set; }
    public bool SubjectLockedByEnv { get; set; }
    public bool FromFilterLockedByEnv { get; set; }
    public bool TelegramBotTokenLockedByEnv { get; set; }
    public bool TelegramChatIdLockedByEnv { get; set; }
}

public sealed class MailNotificationSettingsStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public MailNotificationSettingsStore(IWebHostEnvironment environment)
    {
        var dataDir = Environment.GetEnvironmentVariable("DATA_DIR");
        if (string.IsNullOrWhiteSpace(dataDir))
            dataDir = Directory.Exists("/data") ? "/data" : environment.ContentRootPath;

        _filePath = Path.Combine(dataDir, "mail-notification-settings.json");
    }

    public async Task<MailNotificationStoredSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_filePath))
                return null;

            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<MailNotificationStoredSettings>(stream, cancellationToken: cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(MailNotificationStoredSettings settings, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, settings, cancellationToken: cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }
}

public sealed class MailNotificationSettingsProvider
{
    private readonly IConfiguration _configuration;
    private readonly MailNotificationSettingsStore _store;

    public MailNotificationSettingsProvider(
        IConfiguration configuration,
        MailNotificationSettingsStore store)
    {
        _configuration = configuration;
        _store = store;
    }

    public async Task<MailNotificationStoredSettings> GetStoredSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await _store.LoadAsync(cancellationToken) ?? new MailNotificationStoredSettings();
    }

    public async Task<MailNotificationEffectiveSettings> GetEffectiveSettingsAsync(CancellationToken cancellationToken = default)
    {
        var options = new MailNotificationOptions();
        MailNotificationOptionsSetup.Configure(_configuration, options);
        var stored = await _store.LoadAsync(cancellationToken) ?? new MailNotificationStoredSettings();

        var effective = new MailNotificationEffectiveSettings
        {
            EnabledLockedByEnv = HasEnv("MAIL_NOTIFICATION_ENABLED"),
            UsernameLockedByEnv = HasEnv("NAVER_IMAP_USERNAME"),
            PasswordLockedByEnv = HasEnv("NAVER_IMAP_PASSWORD"),
            SubjectLockedByEnv = HasEnv("NAVER_IMAP_SUBJECT_CONTAINS"),
            FromFilterLockedByEnv = HasEnv("NAVER_IMAP_FROM_FILTER"),
            TelegramBotTokenLockedByEnv = HasEnv("TELEGRAM_BOT_TOKEN"),
            TelegramChatIdLockedByEnv = HasEnv("TELEGRAM_CHAT_ID")
        };

        if (!effective.EnabledLockedByEnv)
            options.Enabled = stored.Enabled;

        if (!effective.UsernameLockedByEnv && !string.IsNullOrWhiteSpace(stored.Username))
            options.NaverImap.Username = stored.Username;
        if (!effective.PasswordLockedByEnv && !string.IsNullOrWhiteSpace(stored.Password))
            options.NaverImap.Password = stored.Password;
        if (!effective.SubjectLockedByEnv && !string.IsNullOrWhiteSpace(stored.SubjectContains))
            options.NaverImap.SubjectContains = stored.SubjectContains;
        if (!effective.FromFilterLockedByEnv)
            options.NaverImap.FromFilter = stored.FromFilter;

        if (!string.IsNullOrWhiteSpace(stored.Host))
            options.NaverImap.Host = stored.Host;
        if (stored.Port > 0)
            options.NaverImap.Port = stored.Port;
        if (!string.IsNullOrWhiteSpace(stored.Folder))
            options.NaverImap.Folder = stored.Folder;

        options.PollIntervalSeconds = stored.PollIntervalSeconds > 0
            ? stored.PollIntervalSeconds
            : options.PollIntervalSeconds;

        if (!effective.TelegramBotTokenLockedByEnv && !string.IsNullOrWhiteSpace(stored.TelegramBotToken))
            options.Telegram.BotToken = stored.TelegramBotToken;
        if (!effective.TelegramChatIdLockedByEnv && !string.IsNullOrWhiteSpace(stored.TelegramChatId))
            options.Telegram.ChatId = stored.TelegramChatId;

        effective.Enabled = options.Enabled;
        effective.PollIntervalSeconds = Math.Clamp(options.PollIntervalSeconds, 30, 600);
        effective.NaverImap = options.NaverImap;
        effective.Telegram = options.Telegram;
        return effective;
    }

    public async Task SaveImapSettingsAsync(
        MailNotificationStoredSettings input,
        string? newPassword,
        CancellationToken cancellationToken = default)
    {
        var existing = await _store.LoadAsync(cancellationToken) ?? new MailNotificationStoredSettings();

        existing.Enabled = input.Enabled;
        existing.PollIntervalSeconds = Math.Clamp(input.PollIntervalSeconds, 30, 600);
        existing.Host = string.IsNullOrWhiteSpace(input.Host) ? "imap.naver.com" : input.Host.Trim();
        existing.Port = input.Port > 0 ? input.Port : 993;
        existing.Folder = string.IsNullOrWhiteSpace(input.Folder) ? "INBOX" : input.Folder.Trim();
        existing.Username = input.Username.Trim();
        existing.SubjectContains = string.IsNullOrWhiteSpace(input.SubjectContains)
            ? "네이버폼"
            : input.SubjectContains.Trim();
        existing.FromFilter = string.IsNullOrWhiteSpace(input.FromFilter) ? null : input.FromFilter.Trim();

        if (!string.IsNullOrWhiteSpace(newPassword))
            existing.Password = newPassword.Trim();

        await _store.SaveAsync(existing, cancellationToken);
    }

    public async Task SaveTelegramSettingsAsync(
        string botToken,
        string chatId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _store.LoadAsync(cancellationToken) ?? new MailNotificationStoredSettings();
        existing.TelegramBotToken = botToken.Trim();
        existing.TelegramChatId = chatId.Trim();
        await _store.SaveAsync(existing, cancellationToken);
    }

    private static bool HasEnv(string name) => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name));
}
